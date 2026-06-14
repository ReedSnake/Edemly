using Edemly.Contracts.Calls;
using Edemly.Contracts.Messages;
using Edemly.Contracts.Realtime;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Application.Common;
using Edemly.Server.Application.Common.Mappers;
using Edemly.Server.Application.Messages;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Infrastructure.Caching;
using Edemly.Server.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace Edemly.Server.Application.Calls;

public class CallService : TenantAwareServiceBase, ICallService
{
    public const string LineBusyMessage = "Line busy";

    private const string MetadataSchema = "edemly.call.v1";
    private const string AudioParticipantsCacheKeyPrefix = "calls:audio-participants:";
    private static readonly TimeSpan AudioParticipantsCacheDuration = TimeSpan.FromSeconds(10);

    private static readonly SemaphoreSlim CallLifecycleGate = new(1, 1);
    private static readonly JsonSerializerOptions MetadataJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ServerDbContext _serverDbContext;
    private readonly ILogger<CallService> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly ChatCacheRegistry _cacheRegistry;

    public CallService(
        ServerDbContext serverDbContext,
        ILogger<CallService> logger,
        IMemoryCache memoryCache,
        ChatCacheRegistry cacheRegistry,
        ITenantProvider tenantProvider,
        ITenantDbContextFactory tenantDbContextFactory)
        : base(serverDbContext, tenantProvider, tenantDbContextFactory)
    {
        _serverDbContext = serverDbContext;
        _logger = logger;
        _memoryCache = memoryCache;
        _cacheRegistry = cacheRegistry;
    }

    public async Task<ServiceResult<CallStartResult>> StartCallAsync(
        int initiatorId,
        int chatId,
        string callUid,
        string? metadata,
        CancellationToken cancellationToken = default)
    {
        var hasGate = false;

        try
        {
            await CallLifecycleGate.WaitAsync(cancellationToken);
            hasGate = true;

            var chatInfo = await GetCallChatInfoAsync(chatId, cancellationToken);
            if (chatInfo == null)
            {
                return ServiceResult<CallStartResult>.BadRequest("Chat not found");
            }

            if (chatInfo.MemberUserIds.Count == 0)
            {
                return ServiceResult<CallStartResult>.BadRequest("Chat has no members");
            }

            if (!chatInfo.MemberUserIds.Contains(initiatorId))
            {
                return ServiceResult<CallStartResult>.Forbidden("User is not a member of this chat");
            }

            var scope = chatInfo.Scope;
            if (scope == CallScopes.Group)
            {
                var hasActiveGroupCall = await _serverDbContext.Calls
                    .AsNoTracking()
                    .AnyAsync(
                        call => call.ChatId == chatId
                                && (call.Status == CallStatus.Pending || call.Status == CallStatus.InProgress),
                        cancellationToken);

                if (hasActiveGroupCall)
                {
                    return ServiceResult<CallStartResult>.Conflict("Call already active");
                }
            }

            var busyCandidates = scope == CallScopes.Group
                ? new[] { initiatorId }
                : chatInfo.MemberUserIds;

            var busyUserIds = await GetBusyUserIdsAsync(busyCandidates, excludedCallId: null, cancellationToken);
            if (busyUserIds.Count > 0)
            {
                _logger.LogInformation(
                    "StartCall rejected as busy. initiator={InitiatorId} chatId={ChatId} scope={Scope} busyUsers={BusyUsers}",
                    initiatorId,
                    chatId,
                    scope,
                    string.Join(",", busyUserIds));

                if (scope == CallScopes.Direct && !busyUserIds.Contains(initiatorId))
                {
                    return await CreateBusyDirectCallStartResultAsync(
                        initiatorId,
                        chatInfo,
                        callUid,
                        metadata,
                        busyUserIds,
                        cancellationToken);
                }

                return ServiceResult<CallStartResult>.Conflict(LineBusyMessage);
            }

            var startedAt = DateTime.UtcNow;
            var mediaKind = ResolveRequestedMediaKind(metadata);
            var envelope = CreateStartedMetadata(
                metadata,
                scope,
                mediaKind,
                chatInfo.MemberUserIds,
                initiatorId,
                startedAt);

            var call = new Call
            {
                ChatId = chatId,
                InitiatorId = initiatorId,
                CallUid = callUid,
                Metadata = SerializeMetadata(envelope),
                Scope = scope,
                MediaKind = mediaKind,
                AnsweredAt = scope == CallScopes.Group ? startedAt : null,
                StartedAt = startedAt,
                ActiveChatId = chatId,
                Status = scope == CallScopes.Group
                    ? CallStatus.InProgress
                    : CallStatus.Pending
            };

            var strategy = _serverDbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var callTransaction = await _serverDbContext.Database.BeginTransactionAsync(cancellationToken);

                try
                {
                    _serverDbContext.Calls.Add(call);
                    await _serverDbContext.SaveChangesAsync(cancellationToken);
                    await PersistNormalizedCallStateAsync(call, envelope, cancellationToken);
                    await _serverDbContext.SaveChangesAsync(cancellationToken);
                    await callTransaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await callTransaction.RollbackAsync(cancellationToken);
                    throw;
                }
            });

            var systemMessage = await CreateCallSystemMessageAsync(
                call,
                envelope,
                chatInfo,
                endedByUserId: null,
                reason: null,
                cancellationToken);

            if (systemMessage != null)
            {
                envelope.SystemMessageId = systemMessage.Id;
                call.SystemMessageId = systemMessage.Id;
                call.Metadata = SerializeMetadata(envelope);
                await _serverDbContext.SaveChangesAsync(cancellationToken);
            }

            var participants = ToParticipantDtos(envelope);
            var incomingCall = new IncomingCallEventDto
            {
                CallId = call.Id,
                CallUid = callUid,
                ChatId = chatId,
                InitiatorId = initiatorId,
                Metadata = envelope.ClientMetadata,
                StartedAt = startedAt,
                Scope = scope,
                MediaKind = mediaKind,
                Participants = participants
            };

            var calling = new CallingEventDto
            {
                CallId = call.Id,
                CallUid = callUid,
                ChatId = chatId,
                InitiatorId = initiatorId,
                Scope = scope,
                MediaKind = mediaKind,
                Participants = participants
            };

            IReadOnlyList<int> incomingRecipients = scope == CallScopes.Direct
                ? chatInfo.MemberUserIds.Where(userId => userId != initiatorId).ToList()
                : Array.Empty<int>();

            return ServiceResult<CallStartResult>.Ok(
                new CallStartResult(
                    incomingCall,
                    calling,
                    chatInfo.MemberUserIds,
                    incomingRecipients,
                    initiatorId,
                    ExpiresWhenPending: scope == CallScopes.Direct,
                    GroupCall: envelope.Scope == CallScopes.Group ? CreateGroupCallEvent(call, envelope) : null,
                    SystemMessage: systemMessage));
        }
        catch (DbUpdateException ex) when (IsCallConcurrencyConflict(ex))
        {
            _logger.LogInformation(
                ex,
                "StartCall rejected by call concurrency constraint. initiator={InitiatorId} chatId={ChatId}",
                initiatorId,
                chatId);

            return ServiceResult<CallStartResult>.Conflict(LineBusyMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start call for chat {ChatId} by user {UserId}", chatId, initiatorId);
            return ServiceResult<CallStartResult>.Unexpected("Failed to start call");
        }
        finally
        {
            if (hasGate)
            {
                CallLifecycleGate.Release();
            }
        }
    }

    private async Task<ServiceResult<CallStartResult>> CreateBusyDirectCallStartResultAsync(
        int initiatorId,
        CallChatInfo chatInfo,
        string callUid,
        string? metadata,
        IReadOnlyList<int> busyUserIds,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        var mediaKind = ResolveRequestedMediaKind(metadata);
        var envelope = CreateStartedMetadata(
            metadata,
            CallScopes.Direct,
            mediaKind,
            chatInfo.MemberUserIds,
            initiatorId,
            startedAt);

        SetParticipantStatus(envelope, initiatorId, CallParticipantStatuses.Left, startedAt);
        foreach (var busyUserId in busyUserIds.Where(userId => userId != initiatorId))
        {
            SetParticipantStatus(envelope, busyUserId, CallParticipantStatuses.Missed, startedAt);
        }

        envelope.Status = CallLifecycleStatuses.Missed;
        envelope.AnsweredAt = null;

        var call = new Call
        {
            ChatId = chatInfo.ChatId,
            InitiatorId = initiatorId,
            CallUid = callUid,
            Metadata = SerializeMetadata(envelope),
            Scope = CallScopes.Direct,
            MediaKind = mediaKind,
            StartedAt = startedAt,
            EndedAt = startedAt,
            EndedByUserId = initiatorId,
            EndReason = LineBusyMessage,
            ActiveChatId = null,
            Status = CallStatus.Missed
        };

        _serverDbContext.Calls.Add(call);
        await _serverDbContext.SaveChangesAsync(cancellationToken);
        await PersistNormalizedCallStateAsync(call, envelope, cancellationToken);
        await _serverDbContext.SaveChangesAsync(cancellationToken);

        var systemMessage = await CreateCallSystemMessageAsync(
            call,
            envelope,
            chatInfo,
            initiatorId,
            LineBusyMessage,
            cancellationToken);

        if (systemMessage != null)
        {
            envelope.SystemMessageId = systemMessage.Id;
            call.SystemMessageId = systemMessage.Id;
            call.Metadata = SerializeMetadata(envelope);
            await _serverDbContext.SaveChangesAsync(cancellationToken);
        }

        var participants = ToParticipantDtos(envelope);
        var incomingCall = new IncomingCallEventDto
        {
            CallId = call.Id,
            CallUid = callUid,
            ChatId = chatInfo.ChatId,
            InitiatorId = initiatorId,
            Metadata = envelope.ClientMetadata,
            StartedAt = startedAt,
            Scope = CallScopes.Direct,
            MediaKind = mediaKind,
            Participants = participants
        };

        var calling = new CallingEventDto
        {
            CallId = call.Id,
            CallUid = callUid,
            ChatId = chatInfo.ChatId,
            InitiatorId = initiatorId,
            Scope = CallScopes.Direct,
            MediaKind = mediaKind,
            Participants = participants
        };

        return ServiceResult<CallStartResult>.Ok(
            new CallStartResult(
                incomingCall,
                calling,
                chatInfo.MemberUserIds,
                Array.Empty<int>(),
                initiatorId,
                ExpiresWhenPending: false,
                GroupCall: null,
                SystemMessage: systemMessage,
                RejectedAsBusy: true));
    }

    public async Task<ServiceResult<CallAcceptedResult>> AcceptCallAsync(
        int userId,
        int callId,
        CancellationToken cancellationToken = default)
    {
        var hasGate = false;

        try
        {
            await CallLifecycleGate.WaitAsync(cancellationToken);
            hasGate = true;

            var call = await _serverDbContext.Calls.FindAsync(new object[] { callId }, cancellationToken);
            if (call == null)
            {
                return ServiceResult<CallAcceptedResult>.NotFound("Call not found");
            }

            var chatInfo = await GetCallChatInfoAsync(call.ChatId, cancellationToken);
            if (chatInfo == null)
            {
                return ServiceResult<CallAcceptedResult>.BadRequest("Chat not found");
            }

            if (!chatInfo.MemberUserIds.Contains(userId))
            {
                return ServiceResult<CallAcceptedResult>.Forbidden("User is not a member of this chat");
            }

            if (!IsActive(call.Status))
            {
                return ServiceResult<CallAcceptedResult>.Conflict("Call is not active");
            }

            var busyUserIds = await GetBusyUserIdsAsync(new[] { userId }, call.Id, cancellationToken);
            if (busyUserIds.Count > 0)
            {
                _logger.LogInformation(
                    "AcceptCall rejected as busy. user={UserId} callId={CallId} busyUsers={BusyUsers}",
                    userId,
                    callId,
                    string.Join(",", busyUserIds));

                return ServiceResult<CallAcceptedResult>.Conflict(LineBusyMessage);
            }

            var now = DateTime.UtcNow;
            var envelope = await ReadMetadataAsync(call, chatInfo, cancellationToken);

            if (envelope.Scope == CallScopes.Direct)
            {
                SetParticipantStatus(envelope, call.InitiatorId, CallParticipantStatuses.Joined, now);
                SetParticipantStatus(envelope, userId, CallParticipantStatuses.Joined, now);
            }
            else
            {
                SetParticipantStatus(envelope, userId, CallParticipantStatuses.Joined, now);
            }

            envelope.Status = CallLifecycleStatuses.Active;
            envelope.AnsweredAt ??= now;
            call.Status = CallStatus.InProgress;
            call.Metadata = SerializeMetadata(envelope);
            await PersistNormalizedCallStateAsync(call, envelope, cancellationToken);
            await _serverDbContext.SaveChangesAsync(cancellationToken);
            ClearCallAudioParticipantsCache(call.Id);
            var systemMessageUpdate = await UpdateCallSystemMessageAsync(
                call,
                envelope,
                chatInfo,
                endedByUserId: null,
                reason: null,
                cancellationToken);

            var accepted = new CallAcceptedEventDto
            {
                CallId = callId,
                UserId = userId,
                Scope = envelope.Scope,
                MediaKind = envelope.MediaKind,
                Participants = ToParticipantDtos(envelope)
            };

            var recipients = envelope.Scope == CallScopes.Group
                ? GetCurrentParticipantUserIds(envelope)
                : chatInfo.MemberUserIds;

            return ServiceResult<CallAcceptedResult>.Ok(
                new CallAcceptedResult(
                    accepted,
                    recipients,
                    chatInfo.MemberUserIds,
                    envelope.Scope == CallScopes.Group ? CreateGroupCallEvent(call, envelope) : null,
                    systemMessageUpdate));
        }
        catch (DbUpdateException ex) when (IsCallConcurrencyConflict(ex))
        {
            _logger.LogInformation(
                ex,
                "AcceptCall rejected by call concurrency constraint. user={UserId} callId={CallId}",
                userId,
                callId);

            return ServiceResult<CallAcceptedResult>.Conflict(LineBusyMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to accept call {CallId} by user {UserId}", callId, userId);
            return ServiceResult<CallAcceptedResult>.Unexpected("Failed to accept call");
        }
        finally
        {
            if (hasGate)
            {
                CallLifecycleGate.Release();
            }
        }
    }

    public async Task<ServiceResult<CallRejectedResult>> RejectCallAsync(
        int userId,
        int callId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var hasGate = false;

        try
        {
            await CallLifecycleGate.WaitAsync(cancellationToken);
            hasGate = true;

            var call = await _serverDbContext.Calls.FindAsync(new object[] { callId }, cancellationToken);
            if (call == null)
            {
                return ServiceResult<CallRejectedResult>.NotFound("Call not found");
            }

            var chatInfo = await GetCallChatInfoAsync(call.ChatId, cancellationToken);
            if (chatInfo == null)
            {
                return ServiceResult<CallRejectedResult>.BadRequest("Chat not found");
            }

            if (!chatInfo.MemberUserIds.Contains(userId))
            {
                return ServiceResult<CallRejectedResult>.Forbidden("User is not a member of this chat");
            }

            if (!IsActive(call.Status))
            {
                return ServiceResult<CallRejectedResult>.Conflict("Call is not active");
            }

            var now = DateTime.UtcNow;
            var envelope = await ReadMetadataAsync(call, chatInfo, cancellationToken);
            SetParticipantStatus(envelope, userId, CallParticipantStatuses.Rejected, now);

            var rejected = new CallRejectedNotification(callId, userId, reason);
            CallEndedNotification? ended = null;
            IReadOnlyList<int> rejectedRecipients = Array.Empty<int>();
            var callEnded = false;

            if (envelope.Scope == CallScopes.Direct)
            {
                envelope.Status = CallLifecycleStatuses.Rejected;
                EndCallEntity(call, now, CallStatus.Rejected, userId, reason);
                ended = new CallEndedNotification(callId, userId, reason);
                rejectedRecipients = new[] { call.InitiatorId };
                callEnded = true;
            }

            call.Metadata = SerializeMetadata(envelope);
            await PersistNormalizedCallStateAsync(call, envelope, cancellationToken);
            await _serverDbContext.SaveChangesAsync(cancellationToken);
            ClearCallAudioParticipantsCache(call.Id);
            var systemMessageUpdate = callEnded
                ? await UpdateCallSystemMessageAsync(call, envelope, chatInfo, userId, reason, cancellationToken)
                : null;

            return ServiceResult<CallRejectedResult>.Ok(
                new CallRejectedResult(
                    rejected,
                    ended,
                    chatInfo.MemberUserIds,
                    rejectedRecipients,
                    callEnded,
                    envelope.Scope == CallScopes.Group ? CreateGroupCallEvent(call, envelope) : null,
                    systemMessageUpdate));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reject call {CallId} by user {UserId}", callId, userId);
            return ServiceResult<CallRejectedResult>.Unexpected("Failed to reject call");
        }
        finally
        {
            if (hasGate)
            {
                CallLifecycleGate.Release();
            }
        }
    }

    public async Task<ServiceResult<CallEndedResult>> EndCallAsync(
        int userId,
        int callId,
        CancellationToken cancellationToken = default)
    {
        var hasGate = false;

        try
        {
            await CallLifecycleGate.WaitAsync(cancellationToken);
            hasGate = true;

            var call = await _serverDbContext.Calls.FindAsync(new object[] { callId }, cancellationToken);
            if (call == null)
            {
                return ServiceResult<CallEndedResult>.NotFound("Call not found");
            }

            var chatInfo = await GetCallChatInfoAsync(call.ChatId, cancellationToken);
            if (chatInfo == null)
            {
                return ServiceResult<CallEndedResult>.BadRequest("Chat not found");
            }

            if (!chatInfo.MemberUserIds.Contains(userId))
            {
                return ServiceResult<CallEndedResult>.Forbidden("User is not a member of this chat");
            }

            var ended = new CallEndedNotification(callId, userId);
            var callEnded = false;
            GroupCallEventDto? groupCall = null;
            MessageDto? systemMessageUpdate = null;

            if (IsActive(call.Status))
            {
                var envelope = await ReadMetadataAsync(call, chatInfo, cancellationToken);
                if (envelope.Scope == CallScopes.Group && !IsCurrentParticipant(envelope, userId))
                {
                    return ServiceResult<CallEndedResult>.Conflict("User is not in this call");
                }

                var now = DateTime.UtcNow;
                callEnded = LeaveCall(call, envelope, userId, now);
                if (callEnded)
                {
                    envelope.Status = CallLifecycleStatuses.Ended;
                }

                call.Metadata = SerializeMetadata(envelope);
                if (callEnded)
                {
                    call.EndedByUserId = userId;
                }

                await PersistNormalizedCallStateAsync(call, envelope, cancellationToken);
                await _serverDbContext.SaveChangesAsync(cancellationToken);
                ClearCallAudioParticipantsCache(call.Id);

                groupCall = envelope.Scope == CallScopes.Group
                    ? CreateGroupCallEvent(call, envelope)
                    : null;

                if (callEnded)
                {
                    systemMessageUpdate = await UpdateCallSystemMessageAsync(
                        call,
                        envelope,
                        chatInfo,
                        userId,
                        null,
                        cancellationToken);
                }
            }

            return ServiceResult<CallEndedResult>.Ok(
                new CallEndedResult(ended, chatInfo.MemberUserIds, callEnded, groupCall, systemMessageUpdate));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to end call {CallId} by user {UserId}", callId, userId);
            return ServiceResult<CallEndedResult>.Unexpected("Failed to end call");
        }
        finally
        {
            if (hasGate)
            {
                CallLifecycleGate.Release();
            }
        }
    }

    public async Task<ServiceResult<CallParticipantUpdatedResult>> SetParticipantMutedAsync(
        int userId,
        int callId,
        bool isMuted,
        CancellationToken cancellationToken = default)
    {
        var hasGate = false;

        try
        {
            await CallLifecycleGate.WaitAsync(cancellationToken);
            hasGate = true;

            var call = await _serverDbContext.Calls.FindAsync(new object[] { callId }, cancellationToken);
            if (call == null)
            {
                return ServiceResult<CallParticipantUpdatedResult>.NotFound("Call not found");
            }

            var chatInfo = await GetCallChatInfoAsync(call.ChatId, cancellationToken);
            if (chatInfo == null)
            {
                return ServiceResult<CallParticipantUpdatedResult>.BadRequest("Chat not found");
            }

            if (!chatInfo.MemberUserIds.Contains(userId))
            {
                return ServiceResult<CallParticipantUpdatedResult>.Forbidden("User is not a member of this chat");
            }

            if (!IsActive(call.Status))
            {
                return ServiceResult<CallParticipantUpdatedResult>.Conflict("Call is not active");
            }

            var envelope = await ReadMetadataAsync(call, chatInfo, cancellationToken);
            var participant = envelope.Participants.FirstOrDefault(participant => participant.UserId == userId);
            if (participant == null || !IsCurrentParticipant(participant))
            {
                return ServiceResult<CallParticipantUpdatedResult>.Conflict("User is not in this call");
            }

            participant.IsMuted = isMuted;
            call.Metadata = SerializeMetadata(envelope);

            await PersistNormalizedCallStateAsync(call, envelope, cancellationToken);
            await _serverDbContext.SaveChangesAsync(cancellationToken);

            var updated = new CallParticipantUpdatedEventDto
            {
                CallId = callId,
                UserId = userId,
                IsMuted = isMuted,
                Participants = ToParticipantDtos(envelope)
            };

            return ServiceResult<CallParticipantUpdatedResult>.Ok(
                new CallParticipantUpdatedResult(
                    updated,
                    GetCurrentParticipantUserIds(envelope)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update mute state for call {CallId} by user {UserId}", callId, userId);
            return ServiceResult<CallParticipantUpdatedResult>.Unexpected("Failed to update mute state");
        }
        finally
        {
            if (hasGate)
            {
                CallLifecycleGate.Release();
            }
        }
    }

    public async Task<ServiceResult<CallMissedResult>> MarkPendingCallMissedAsync(
        int callId,
        CancellationToken cancellationToken = default)
    {
        var hasGate = false;

        try
        {
            await CallLifecycleGate.WaitAsync(cancellationToken);
            hasGate = true;

            var call = await _serverDbContext.Calls.FindAsync(new object[] { callId }, cancellationToken);
            if (call == null)
            {
                return ServiceResult<CallMissedResult>.NotFound("Call not found");
            }

            if (call.Status != CallStatus.Pending)
            {
                return ServiceResult<CallMissedResult>.Ok(new CallMissedResult(callId, Missed: false, null));
            }

            MessageDto? systemMessageUpdate = null;
            var chatInfo = await GetCallChatInfoAsync(call.ChatId, cancellationToken);
            if (chatInfo != null)
            {
                var now = DateTime.UtcNow;
                var envelope = await ReadMetadataAsync(call, chatInfo, cancellationToken);
                foreach (var participant in envelope.Participants)
                {
                    if (participant.Status == CallParticipantStatuses.Ringing)
                    {
                        SetParticipantStatus(envelope, participant.UserId, CallParticipantStatuses.Missed, now);
                    }
                }

                envelope.Status = CallLifecycleStatuses.Missed;
                call.Metadata = SerializeMetadata(envelope);
                call.Status = CallStatus.Missed;
                call.EndedAt = now;
                call.EndedByUserId = call.InitiatorId;
                call.EndReason = "No answer";
                call.ActiveChatId = null;
                await PersistNormalizedCallStateAsync(call, envelope, cancellationToken);
                await _serverDbContext.SaveChangesAsync(cancellationToken);
                ClearCallAudioParticipantsCache(call.Id);

                systemMessageUpdate = await UpdateCallSystemMessageAsync(
                    call,
                    envelope,
                    chatInfo,
                    endedByUserId: call.InitiatorId,
                    reason: "No answer",
                    cancellationToken);
            }
            else
            {
                call.Status = CallStatus.Missed;
                call.EndedAt = DateTime.UtcNow;
                call.EndedByUserId = call.InitiatorId;
                call.EndReason = "No answer";
                call.ActiveChatId = null;
                var participants = await _serverDbContext.CallParticipants
                    .Where(participant => participant.CallId == call.Id)
                    .ToListAsync(cancellationToken);

                foreach (var participant in participants)
                {
                    participant.Status = CallParticipantStatus.Missed;
                    participant.LeftAt ??= call.EndedAt;
                    participant.CurrentLockUserId = null;
                }

                await _serverDbContext.SaveChangesAsync(cancellationToken);
                ClearCallAudioParticipantsCache(call.Id);
            }

            return ServiceResult<CallMissedResult>.Ok(new CallMissedResult(callId, Missed: true, systemMessageUpdate));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark pending call {CallId} as missed", callId);
            return ServiceResult<CallMissedResult>.Unexpected("Failed to mark call as missed");
        }
        finally
        {
            if (hasGate)
            {
                CallLifecycleGate.Release();
            }
        }
    }

    public async Task<ServiceResult<CallAudioBroadcastResult>> GetAudioBroadcastRecipientsAsync(
        int senderId,
        int callId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = GetCallAudioParticipantsCacheKey(callId);
            if (_memoryCache.TryGetValue<IReadOnlyList<int>>(cacheKey, out var cachedParticipantUserIds)
                && cachedParticipantUserIds != null
                && cachedParticipantUserIds.Contains(senderId))
            {
                return ServiceResult<CallAudioBroadcastResult>.Ok(
                    new CallAudioBroadcastResult(
                        cachedParticipantUserIds
                            .Where(userId => userId != senderId)
                            .ToList()));
            }

            var call = await _serverDbContext.Calls.FindAsync(new object[] { callId }, cancellationToken);
            if (call == null)
            {
                return ServiceResult<CallAudioBroadcastResult>.NotFound("Call not found");
            }

            if (call.Status != CallStatus.InProgress)
            {
                return ServiceResult<CallAudioBroadcastResult>.Conflict("Call is not in progress");
            }

            var chatInfo = await GetCallChatInfoAsync(call.ChatId, cancellationToken);
            if (chatInfo == null)
            {
                return ServiceResult<CallAudioBroadcastResult>.BadRequest("Chat not found");
            }

            if (!chatInfo.MemberUserIds.Contains(senderId))
            {
                return ServiceResult<CallAudioBroadcastResult>.Forbidden("User is not a member of this chat");
            }

            var envelope = await ReadMetadataAsync(call, chatInfo, cancellationToken);
            if (!IsCurrentParticipant(envelope, senderId))
            {
                return ServiceResult<CallAudioBroadcastResult>.Forbidden("User is not in this call");
            }

            var recipientUserIds = GetCurrentParticipantUserIds(envelope)
                .ToList();

            _memoryCache.Set(cacheKey, recipientUserIds, AudioParticipantsCacheDuration);
            recipientUserIds = recipientUserIds
                .Where(userId => userId != senderId)
                .ToList();

            return ServiceResult<CallAudioBroadcastResult>.Ok(
                new CallAudioBroadcastResult(recipientUserIds));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve audio recipients for call {CallId}", callId);
            return ServiceResult<CallAudioBroadcastResult>.Unexpected("Failed to resolve audio recipients");
        }
    }

    public async Task<ServiceResult<IReadOnlyList<CallEndedResult>>> EndActiveCallsForUserAsync(
        int userId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var hasGate = false;

        try
        {
            await CallLifecycleGate.WaitAsync(cancellationToken);
            hasGate = true;

            var activeCalls = await GetActiveCallsForUserAsync(userId, cancellationToken);
            if (activeCalls.Count == 0)
            {
                return ServiceResult<IReadOnlyList<CallEndedResult>>.Ok(Array.Empty<CallEndedResult>());
            }

            var results = new List<CallEndedResult>();
            foreach (var call in activeCalls)
            {
                var chatInfo = await GetCallChatInfoAsync(call.ChatId, cancellationToken);
                if (chatInfo == null)
                {
                    continue;
                }

                var envelope = await ReadMetadataAsync(call, chatInfo, cancellationToken);
                var now = DateTime.UtcNow;
                var callEnded = LeaveCall(call, envelope, userId, now);
                if (callEnded)
                {
                    envelope.Status = CallLifecycleStatuses.Ended;
                }

                call.Metadata = SerializeMetadata(envelope);
                if (callEnded)
                {
                    call.EndedByUserId = userId;
                    call.EndReason = reason;
                }

                await PersistNormalizedCallStateAsync(call, envelope, cancellationToken);
                await _serverDbContext.SaveChangesAsync(cancellationToken);
                ClearCallAudioParticipantsCache(call.Id);

                var systemMessageUpdate = callEnded
                    ? await UpdateCallSystemMessageAsync(call, envelope, chatInfo, userId, reason, cancellationToken)
                    : null;

                results.Add(new CallEndedResult(
                    new CallEndedNotification(call.Id, userId, reason),
                    chatInfo.MemberUserIds,
                    callEnded,
                    envelope.Scope == CallScopes.Group ? CreateGroupCallEvent(call, envelope) : null,
                    systemMessageUpdate));
            }

            return ServiceResult<IReadOnlyList<CallEndedResult>>.Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to end active calls for disconnected user {UserId}", userId);
            return ServiceResult<IReadOnlyList<CallEndedResult>>.Unexpected("Failed to end active calls");
        }
        finally
        {
            if (hasGate)
            {
                CallLifecycleGate.Release();
            }
        }
    }

    public async Task<ServiceResult<IReadOnlyList<GroupCallEventDto>>> GetActiveGroupCallsForUserAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var activeCalls = await _serverDbContext.Calls
                .AsNoTracking()
                .Where(call => call.Status == CallStatus.Pending || call.Status == CallStatus.InProgress)
                .ToListAsync(cancellationToken);

            if (activeCalls.Count == 0)
            {
                return ServiceResult<IReadOnlyList<GroupCallEventDto>>.Ok(Array.Empty<GroupCallEventDto>());
            }

            var groupCalls = new List<GroupCallEventDto>();
            foreach (var call in activeCalls)
            {
                var chatInfo = await GetCallChatInfoAsync(call.ChatId, cancellationToken);
                if (chatInfo == null
                    || chatInfo.Scope != CallScopes.Group
                    || !chatInfo.MemberUserIds.Contains(userId))
                {
                    continue;
                }

                var envelope = await ReadMetadataAsync(call, chatInfo, cancellationToken);
                if (envelope.Scope != CallScopes.Group)
                {
                    continue;
                }

                groupCalls.Add(CreateGroupCallEvent(call, envelope));
            }

            return ServiceResult<IReadOnlyList<GroupCallEventDto>>.Ok(groupCalls);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get active group calls for user {UserId}", userId);
            return ServiceResult<IReadOnlyList<GroupCallEventDto>>.Unexpected("Failed to get active group calls");
        }
    }

    private async Task<CallChatInfo?> GetCallChatInfoAsync(
        int chatId,
        CancellationToken cancellationToken)
    {
        await using var dbContextLease = ResolveDbContext();

        var chat = await dbContextLease.Context.Set<Chat>()
            .AsNoTracking()
            .Where(chat => chat.Id == chatId)
            .Select(chat => new { chat.Id, chat.Type })
            .FirstOrDefaultAsync(cancellationToken);

        if (chat == null)
        {
            return null;
        }

        var memberUserIds = await dbContextLease.Context.Set<ChatMember>()
            .AsNoTracking()
            .Where(chatMember => chatMember.ChatId == chatId)
            .Select(chatMember => chatMember.UserId)
            .ToListAsync(cancellationToken);

        return new CallChatInfo(chat.Id, chat.Type, memberUserIds);
    }

    private async Task<IReadOnlyList<int>> GetBusyUserIdsAsync(
        IReadOnlyCollection<int> candidateUserIds,
        int? excludedCallId,
        CancellationToken cancellationToken)
    {
        if (candidateUserIds.Count == 0)
        {
            return Array.Empty<int>();
        }

        var activeCallQuery = _serverDbContext.Calls
            .AsNoTracking()
            .Where(call => call.Status == CallStatus.Pending || call.Status == CallStatus.InProgress);

        if (excludedCallId.HasValue)
        {
            activeCallQuery = activeCallQuery.Where(call => call.Id != excludedCallId.Value);
        }

        var activeCalls = await activeCallQuery.ToListAsync(cancellationToken);
        if (activeCalls.Count == 0)
        {
            return Array.Empty<int>();
        }

        var candidateSet = candidateUserIds.ToHashSet();
        var busyUserIds = new HashSet<int>();

        foreach (var call in activeCalls)
        {
            var chatInfo = await GetCallChatInfoAsync(call.ChatId, cancellationToken);
            if (chatInfo == null)
            {
                continue;
            }

            var envelope = await ReadMetadataAsync(call, chatInfo, cancellationToken);
            foreach (var participant in envelope.Participants)
            {
                if (candidateSet.Contains(participant.UserId) && IsBusyParticipant(envelope, participant))
                {
                    busyUserIds.Add(participant.UserId);
                }
            }
        }

        return busyUserIds.ToList();
    }

    private async Task<List<Call>> GetActiveCallsForUserAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        var activeCalls = await _serverDbContext.Calls
            .Where(call => call.Status == CallStatus.Pending || call.Status == CallStatus.InProgress)
            .ToListAsync(cancellationToken);

        if (activeCalls.Count == 0)
        {
            return new List<Call>();
        }

        var userActiveCalls = new List<Call>();
        foreach (var call in activeCalls)
        {
            var chatInfo = await GetCallChatInfoAsync(call.ChatId, cancellationToken);
            if (chatInfo == null || !chatInfo.MemberUserIds.Contains(userId))
            {
                continue;
            }

            var envelope = await ReadMetadataAsync(call, chatInfo, cancellationToken);
            if (IsCurrentParticipant(envelope, userId))
            {
                userActiveCalls.Add(call);
            }
        }

        return userActiveCalls;
    }

    private async Task<MessageDto?> CreateCallSystemMessageAsync(
        Call call,
        CallMetadataEnvelope envelope,
        CallChatInfo chatInfo,
        int? endedByUserId,
        string? reason,
        CancellationToken cancellationToken)
    {
        await using var dbContextLease = ResolveDbContext();
        var ctx = dbContextLease.Context;

        var message = new Message
        {
            ChatId = chatInfo.ChatId,
            SenderId = call.InitiatorId,
            Text = SerializeCallMessagePayload(call, envelope, endedByUserId, reason),
            SentAt = call.StartedAt,
            Type = MessageType.Call
        };

        ctx.Set<Message>().Add(message);
        await ctx.SaveChangesAsync(cancellationToken);

        await ChatLastMessageSnapshot.ApplyAsync(ctx, message, cancellationToken);
        await ctx.SaveChangesAsync(cancellationToken);

        ClearChatCache(chatInfo.ChatId);

        return MessageMappings.ToDto(message);
    }

    private async Task<MessageDto?> UpdateCallSystemMessageAsync(
        Call call,
        CallMetadataEnvelope envelope,
        CallChatInfo chatInfo,
        int? endedByUserId,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (!envelope.SystemMessageId.HasValue)
        {
            var created = await CreateCallSystemMessageAsync(call, envelope, chatInfo, endedByUserId, reason, cancellationToken);
            if (created != null)
            {
                envelope.SystemMessageId = created.Id;
                call.SystemMessageId = created.Id;
                call.Metadata = SerializeMetadata(envelope);
                await PersistNormalizedCallStateAsync(call, envelope, cancellationToken);
                await _serverDbContext.SaveChangesAsync(cancellationToken);
            }

            return created;
        }

        await using var dbContextLease = ResolveDbContext();
        var ctx = dbContextLease.Context;

        var message = await ctx.Set<Message>()
            .FirstOrDefaultAsync(
                message => message.Id == envelope.SystemMessageId.Value
                           && message.ChatId == chatInfo.ChatId,
                cancellationToken);

        if (message == null)
        {
            envelope.SystemMessageId = null;
            return await UpdateCallSystemMessageAsync(
                call,
                envelope,
                chatInfo,
                endedByUserId,
                reason,
                cancellationToken);
        }

        message.Text = SerializeCallMessagePayload(call, envelope, endedByUserId, reason);
        message.Type = MessageType.Call;
        call.SystemMessageId = envelope.SystemMessageId;

        await ChatLastMessageSnapshot.ApplyIfCurrentAsync(ctx, message, cancellationToken);

        await ctx.SaveChangesAsync(cancellationToken);
        ClearChatCache(message.ChatId);

        return MessageMappings.ToDto(message);
    }

    private string SerializeCallMessagePayload(
        Call call,
        CallMetadataEnvelope envelope,
        int? endedByUserId,
        string? reason)
    {
        return JsonSerializer.Serialize(
            CreateCallMessagePayload(call, envelope, endedByUserId, reason),
            MetadataJsonOptions);
    }

    private static CallMessagePayloadDto CreateCallMessagePayload(
        Call call,
        CallMetadataEnvelope envelope,
        int? endedByUserId,
        string? reason)
    {
        var endedAt = call.EndedAt;
        long? durationSeconds = endedAt.HasValue && envelope.AnsweredAt.HasValue
            ? Math.Max(0, (long)Math.Round((endedAt.Value - envelope.AnsweredAt.Value).TotalSeconds))
            : null;

        return new CallMessagePayloadDto
        {
            CallId = call.Id,
            CallUid = call.CallUid,
            ChatId = call.ChatId,
            InitiatorId = call.InitiatorId,
            Scope = envelope.Scope,
            MediaKind = envelope.MediaKind,
            Status = envelope.Status,
            StartedAt = call.StartedAt,
            AnsweredAt = envelope.AnsweredAt,
            EndedAt = endedAt,
            DurationSeconds = durationSeconds,
            EndedByUserId = endedByUserId,
            Reason = reason,
            Participants = ToParticipantDtos(envelope)
        };
    }

    private static GroupCallEventDto CreateGroupCallEvent(Call call, CallMetadataEnvelope envelope)
    {
        return new GroupCallEventDto
        {
            CallId = call.Id,
            CallUid = call.CallUid,
            ChatId = call.ChatId,
            InitiatorId = call.InitiatorId,
            Scope = CallScopes.Group,
            MediaKind = envelope.MediaKind,
            Status = IsActive(call.Status) ? envelope.Status : CallLifecycleStatuses.Ended,
            StartedAt = call.StartedAt,
            EndedAt = call.EndedAt,
            Participants = ToParticipantDtos(envelope)
        };
    }

    private void ClearChatCache(int chatId)
    {
        _cacheRegistry.ClearChat(chatId, _memoryCache);
    }

    private static string GetCallAudioParticipantsCacheKey(int callId)
    {
        return AudioParticipantsCacheKeyPrefix + callId;
    }

    private void ClearCallAudioParticipantsCache(int callId)
    {
        _memoryCache.Remove(GetCallAudioParticipantsCacheKey(callId));
    }

    private CallMetadataEnvelope CreateStartedMetadata(
        string? metadata,
        string scope,
        string mediaKind,
        IReadOnlyList<int> memberUserIds,
        int initiatorId,
        DateTime startedAt)
    {
        var participants = scope == CallScopes.Group
            ? new List<CallParticipantState>
            {
                new()
                {
                    UserId = initiatorId,
                    Status = CallParticipantStatuses.Joined,
                    InvitedAt = startedAt,
                    JoinedAt = startedAt,
                    IsMuted = false
                }
            }
            : memberUserIds
                .Select(userId => new CallParticipantState
                {
                    UserId = userId,
                    Status = CallParticipantStatuses.Ringing,
                    InvitedAt = startedAt,
                    IsMuted = false
                })
                .ToList();

        return new CallMetadataEnvelope
        {
            Schema = MetadataSchema,
            ClientMetadata = NormalizeMetadata(metadata),
            Scope = scope,
            MediaKind = mediaKind,
            Status = scope == CallScopes.Group
                ? CallLifecycleStatuses.Active
                : CallLifecycleStatuses.Pending,
            AnsweredAt = scope == CallScopes.Group ? startedAt : null,
            Participants = participants
        };
    }

    private CallMetadataEnvelope ReadMetadata(Call call, CallChatInfo chatInfo)
    {
        if (!string.IsNullOrWhiteSpace(call.Metadata))
        {
            try
            {
                var envelope = JsonSerializer.Deserialize<CallMetadataEnvelope>(
                    call.Metadata,
                    MetadataJsonOptions);

                if (envelope != null && envelope.Schema == MetadataSchema)
                {
                    envelope.Scope = NormalizeScope(envelope.Scope, chatInfo.Scope);
                    envelope.MediaKind = NormalizeMediaKind(envelope.MediaKind);
                    envelope.Status = NormalizeLifecycleStatus(envelope.Status, call.Status);
                    envelope.Participants ??= new List<CallParticipantState>();

                    if (envelope.Participants.Count == 0)
                    {
                        envelope.Participants = CreateLegacyParticipants(call, chatInfo);
                    }

                    return envelope;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse call metadata envelope");
            }
        }

        return new CallMetadataEnvelope
        {
            Schema = MetadataSchema,
            ClientMetadata = NormalizeMetadata(call.Metadata),
            Scope = chatInfo.Scope,
            MediaKind = CallMediaKinds.Audio,
            Status = ResolveLifecycleStatus(call.Status),
            AnsweredAt = call.Status == CallStatus.InProgress ? call.StartedAt : null,
            Participants = CreateLegacyParticipants(call, chatInfo)
        };
    }

    private async Task<CallMetadataEnvelope> ReadMetadataAsync(
        Call call,
        CallChatInfo chatInfo,
        CancellationToken cancellationToken)
    {
        var envelope = ReadMetadata(call, chatInfo);

        envelope.Scope = NormalizeScope(call.Scope, envelope.Scope);
        envelope.MediaKind = NormalizeMediaKind(call.MediaKind);
        envelope.Status = NormalizeLifecycleStatus(envelope.Status, call.Status);
        envelope.AnsweredAt = call.AnsweredAt ?? envelope.AnsweredAt;
        envelope.SystemMessageId = call.SystemMessageId ?? envelope.SystemMessageId;

        if (call.Id <= 0)
        {
            return envelope;
        }

        var participants = await _serverDbContext.CallParticipants
            .AsNoTracking()
            .Where(participant => participant.CallId == call.Id)
            .OrderBy(participant => participant.Id)
            .ToListAsync(cancellationToken);

        if (participants.Count == 0)
        {
            return envelope;
        }

        envelope.Participants = participants
            .Select(participant => new CallParticipantState
            {
                UserId = participant.UserId,
                Status = ToContractParticipantStatus(participant.Status),
                InvitedAt = participant.InvitedAt,
                JoinedAt = participant.JoinedAt,
                LeftAt = participant.LeftAt,
                IsMuted = participant.IsMuted
            })
            .ToList();

        return envelope;
    }

    private async Task PersistNormalizedCallStateAsync(
        Call call,
        CallMetadataEnvelope envelope,
        CancellationToken cancellationToken)
    {
        call.Scope = NormalizeScope(envelope.Scope, call.Scope);
        call.MediaKind = NormalizeMediaKind(envelope.MediaKind);
        call.AnsweredAt = envelope.AnsweredAt;
        call.SystemMessageId = envelope.SystemMessageId;
        var callIsActive = IsActive(call.Status);
        call.ActiveChatId = callIsActive ? call.ChatId : null;

        if (call.Id <= 0)
        {
            return;
        }

        var existingParticipants = await _serverDbContext.CallParticipants
            .Where(participant => participant.CallId == call.Id)
            .ToListAsync(cancellationToken);

        var existingByUserId = existingParticipants.ToDictionary(participant => participant.UserId);
        var persistedUserIds = new HashSet<int>();
        foreach (var participantState in envelope.Participants
                     .Where(participant => participant.UserId > 0)
                     .GroupBy(participant => participant.UserId)
                     .Select(group => group.Last()))
        {
            persistedUserIds.Add(participantState.UserId);

            if (!existingByUserId.TryGetValue(participantState.UserId, out var participant))
            {
                participant = new CallParticipant
                {
                    CallId = call.Id,
                    UserId = participantState.UserId
                };

                _serverDbContext.CallParticipants.Add(participant);
            }

            participant.Status = ToEntityParticipantStatus(participantState.Status);
            participant.InvitedAt = participantState.InvitedAt;
            participant.JoinedAt = participantState.JoinedAt;
            participant.LeftAt = participantState.LeftAt;
            participant.IsMuted = participantState.IsMuted;
            participant.CurrentLockUserId = callIsActive && IsBusyParticipant(envelope, participantState)
                ? participantState.UserId
                : null;
        }

        foreach (var participant in existingParticipants.Where(participant => !persistedUserIds.Contains(participant.UserId)))
        {
            participant.CurrentLockUserId = null;
        }
    }

    private List<CallParticipantState> CreateLegacyParticipants(Call call, CallChatInfo chatInfo)
    {
        var status = call.Status switch
        {
            CallStatus.Pending => CallParticipantStatuses.Ringing,
            CallStatus.InProgress => CallParticipantStatuses.Joined,
            CallStatus.Missed => CallParticipantStatuses.Missed,
            CallStatus.Rejected => CallParticipantStatuses.Rejected,
            _ => CallParticipantStatuses.Left
        };

        var participantIds = chatInfo.Scope == CallScopes.Group
            ? new[] { call.InitiatorId }
            : chatInfo.MemberUserIds;

        return participantIds
            .Distinct()
            .Select(userId => new CallParticipantState
            {
                UserId = userId,
                Status = status,
                InvitedAt = call.StartedAt,
                JoinedAt = status == CallParticipantStatuses.Joined ? call.StartedAt : null,
                LeftAt = status is CallParticipantStatuses.Left or CallParticipantStatuses.Missed
                    ? call.EndedAt
                    : null,
                IsMuted = false
            })
            .ToList();
    }

    private string SerializeMetadata(CallMetadataEnvelope envelope)
    {
        envelope.Schema = MetadataSchema;
        envelope.Scope = NormalizeScope(envelope.Scope, CallScopes.Direct);
        envelope.MediaKind = NormalizeMediaKind(envelope.MediaKind);
        envelope.Status = NormalizeLifecycleStatus(envelope.Status, null);
        envelope.Participants ??= new List<CallParticipantState>();

        return JsonSerializer.Serialize(envelope, MetadataJsonOptions);
    }

    private string? NormalizeMetadata(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return metadata;
        }

        try
        {
            using var doc = JsonDocument.Parse(metadata);
            return doc.RootElement.GetRawText();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse call metadata JSON string");
            return metadata;
        }
    }

    private static IReadOnlyList<CallParticipantDto> ToParticipantDtos(CallMetadataEnvelope envelope)
    {
        return envelope.Participants
            .Select(participant => new CallParticipantDto
            {
                UserId = participant.UserId,
                Status = participant.Status,
                InvitedAt = participant.InvitedAt,
                JoinedAt = participant.JoinedAt,
                LeftAt = participant.LeftAt,
                IsMuted = participant.IsMuted
            })
            .ToList();
    }

    private static IReadOnlyList<int> GetCurrentParticipantUserIds(CallMetadataEnvelope envelope)
    {
        return envelope.Participants
            .Where(IsCurrentParticipant)
            .Select(participant => participant.UserId)
            .Distinct()
            .ToList();
    }

    private static bool LeaveCall(Call call, CallMetadataEnvelope envelope, int userId, DateTime now)
    {
        SetParticipantStatus(envelope, userId, CallParticipantStatuses.Left, now);

        if (envelope.Scope == CallScopes.Direct)
        {
            EndCallEntity(call, now);
            return true;
        }

        var remainingJoined = envelope.Participants.Count(participant => participant.Status == CallParticipantStatuses.Joined);
        if (remainingJoined > 1)
        {
            return false;
        }

        foreach (var participant in envelope.Participants.Where(participant => participant.Status == CallParticipantStatuses.Joined))
        {
            SetParticipantStatus(envelope, participant.UserId, CallParticipantStatuses.Left, now);
        }

        EndCallEntity(call, now);
        return true;
    }

    private static void SetParticipantStatus(
        CallMetadataEnvelope envelope,
        int userId,
        string status,
        DateTime now)
    {
        var participant = envelope.Participants.FirstOrDefault(participant => participant.UserId == userId);
        if (participant == null)
        {
            participant = new CallParticipantState
            {
                UserId = userId,
                InvitedAt = now
            };

            envelope.Participants.Add(participant);
        }

        participant.Status = status;
        participant.InvitedAt ??= now;

        if (status == CallParticipantStatuses.Joined)
        {
            participant.JoinedAt ??= now;
            participant.LeftAt = null;
            participant.IsMuted = false;
        }
        else if (status is CallParticipantStatuses.Left or CallParticipantStatuses.Rejected or CallParticipantStatuses.Missed)
        {
            participant.LeftAt ??= now;
        }
    }

    private static bool IsCurrentParticipant(CallMetadataEnvelope envelope, int userId)
    {
        return envelope.Participants.Any(participant =>
            participant.UserId == userId
            && IsCurrentParticipant(participant));
    }

    private static bool IsCurrentParticipant(CallParticipantState participant)
    {
        return participant.Status == CallParticipantStatuses.Ringing
            || participant.Status == CallParticipantStatuses.Joined;
    }

    private static bool IsBusyParticipant(CallMetadataEnvelope envelope, CallParticipantState participant)
    {
        if (string.Equals(envelope.Scope, CallScopes.Group, StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(participant.Status, CallParticipantStatuses.Joined, StringComparison.OrdinalIgnoreCase);
        }

        return IsCurrentParticipant(participant);
    }

    private static bool IsActive(CallStatus status)
    {
        return status is CallStatus.Pending or CallStatus.InProgress;
    }

    private static bool IsCallConcurrencyConflict(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("IX_call_ActiveChatId", StringComparison.OrdinalIgnoreCase)
            || message.Contains("IX_call_participant_CurrentLockUserId", StringComparison.OrdinalIgnoreCase);
    }

    private static void EndCallEntity(
        Call call,
        DateTime? endedAt = null,
        CallStatus status = CallStatus.Ended,
        int? endedByUserId = null,
        string? reason = null)
    {
        call.Status = status;
        call.EndedAt ??= endedAt ?? DateTime.UtcNow;
        call.EndedByUserId ??= endedByUserId;
        call.EndReason ??= reason;
        call.ActiveChatId = null;
    }

    private static string ResolveRequestedMediaKind(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return CallMediaKinds.Audio;
        }

        try
        {
            using var doc = JsonDocument.Parse(metadata);
            if (doc.RootElement.ValueKind == JsonValueKind.String)
            {
                return NormalizeMediaKind(doc.RootElement.GetString());
            }

            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return CallMediaKinds.Audio;
            }

            foreach (var propertyName in new[] { "mediaKind", "callType", "type" })
            {
                if (doc.RootElement.TryGetProperty(propertyName, out var property)
                    && property.ValueKind == JsonValueKind.String)
                {
                    return NormalizeMediaKind(property.GetString());
                }
            }
        }
        catch
        {
            return CallMediaKinds.Audio;
        }

        return CallMediaKinds.Audio;
    }

    private static string NormalizeScope(string? scope, string fallback)
    {
        return string.Equals(scope, CallScopes.Group, StringComparison.OrdinalIgnoreCase)
            ? CallScopes.Group
            : string.Equals(fallback, CallScopes.Group, StringComparison.OrdinalIgnoreCase)
                ? CallScopes.Group
                : CallScopes.Direct;
    }

    private static string NormalizeMediaKind(string? mediaKind)
    {
        return string.Equals(mediaKind, CallMediaKinds.Video, StringComparison.OrdinalIgnoreCase)
            ? CallMediaKinds.Video
            : CallMediaKinds.Audio;
    }

    private static string ResolveLifecycleStatus(CallStatus status)
    {
        return status switch
        {
            CallStatus.Pending => CallLifecycleStatuses.Pending,
            CallStatus.InProgress => CallLifecycleStatuses.Active,
            CallStatus.Missed => CallLifecycleStatuses.Missed,
            CallStatus.Rejected => CallLifecycleStatuses.Rejected,
            _ => CallLifecycleStatuses.Ended
        };
    }

    private static CallParticipantStatus ToEntityParticipantStatus(string? status)
    {
        if (string.Equals(status, CallParticipantStatuses.Ringing, StringComparison.OrdinalIgnoreCase))
        {
            return CallParticipantStatus.Ringing;
        }

        if (string.Equals(status, CallParticipantStatuses.Joined, StringComparison.OrdinalIgnoreCase))
        {
            return CallParticipantStatus.Joined;
        }

        if (string.Equals(status, CallParticipantStatuses.Left, StringComparison.OrdinalIgnoreCase))
        {
            return CallParticipantStatus.Left;
        }

        if (string.Equals(status, CallParticipantStatuses.Rejected, StringComparison.OrdinalIgnoreCase))
        {
            return CallParticipantStatus.Rejected;
        }

        if (string.Equals(status, CallParticipantStatuses.Missed, StringComparison.OrdinalIgnoreCase))
        {
            return CallParticipantStatus.Missed;
        }

        return CallParticipantStatus.Invited;
    }

    private static string ToContractParticipantStatus(CallParticipantStatus status)
    {
        return status switch
        {
            CallParticipantStatus.Ringing => CallParticipantStatuses.Ringing,
            CallParticipantStatus.Joined => CallParticipantStatuses.Joined,
            CallParticipantStatus.Left => CallParticipantStatuses.Left,
            CallParticipantStatus.Rejected => CallParticipantStatuses.Rejected,
            CallParticipantStatus.Missed => CallParticipantStatuses.Missed,
            _ => CallParticipantStatuses.Invited
        };
    }

    private static string NormalizeLifecycleStatus(string? status, CallStatus? fallbackStatus)
    {
        if (string.Equals(status, CallLifecycleStatuses.Active, StringComparison.OrdinalIgnoreCase))
        {
            return CallLifecycleStatuses.Active;
        }

        if (string.Equals(status, CallLifecycleStatuses.Ended, StringComparison.OrdinalIgnoreCase))
        {
            return CallLifecycleStatuses.Ended;
        }

        if (string.Equals(status, CallLifecycleStatuses.Rejected, StringComparison.OrdinalIgnoreCase))
        {
            return CallLifecycleStatuses.Rejected;
        }

        if (string.Equals(status, CallLifecycleStatuses.Missed, StringComparison.OrdinalIgnoreCase))
        {
            return CallLifecycleStatuses.Missed;
        }

        if (string.Equals(status, CallLifecycleStatuses.Pending, StringComparison.OrdinalIgnoreCase))
        {
            return CallLifecycleStatuses.Pending;
        }

        return fallbackStatus.HasValue
            ? ResolveLifecycleStatus(fallbackStatus.Value)
            : CallLifecycleStatuses.Pending;
    }

    private sealed record CallChatInfo(
        int ChatId,
        ChatType Type,
        IReadOnlyList<int> MemberUserIds)
    {
        public string Scope => Type == ChatType.Group
            ? CallScopes.Group
            : CallScopes.Direct;
    }

    private sealed class CallMetadataEnvelope
    {
        public string Schema { get; set; } = MetadataSchema;

        public string? ClientMetadata { get; set; }

        public string Scope { get; set; } = CallScopes.Direct;

        public string MediaKind { get; set; } = CallMediaKinds.Audio;

        public string Status { get; set; } = CallLifecycleStatuses.Pending;

        public DateTime? AnsweredAt { get; set; }

        public int? SystemMessageId { get; set; }

        public List<CallParticipantState> Participants { get; set; } = new();
    }

    private sealed class CallParticipantState
    {
        public int UserId { get; set; }

        public string Status { get; set; } = CallParticipantStatuses.Invited;

        public DateTime? InvitedAt { get; set; }

        public DateTime? JoinedAt { get; set; }

        public DateTime? LeftAt { get; set; }

        public bool IsMuted { get; set; }
    }
}
