using Edemly.Server.Application.Calls;
using Edemly.Server.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Edemly.Server.Api.Hubs
{
    [Authorize]
    public class CallHub : Hub
    {
        private static readonly TimeSpan PendingCallTimeout = TimeSpan.FromSeconds(30);

        private readonly ICallService _callService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<CallHub> _hubContext;
        private readonly IHubContext<MainHub> _mainHubContext;
        private readonly ILogger<CallHub> _logger;

        public CallHub(
            ICallService callService,
            IServiceScopeFactory scopeFactory,
            IHubContext<CallHub> hubContext,
            IHubContext<MainHub> mainHubContext,
            ILogger<CallHub> logger)
        {
            _callService = callService;
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
            _mainHubContext = mainHubContext;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            try
            {
                int? uid = null;
                try
                {
                    uid = GetUserId();
                }
                catch (Exception)
                {
                    // Authentication failures are surfaced by hub methods.
                }

                _logger.LogInformation(
                    "CallHub OnConnected: connectionId={ConnId} userId={UserId}",
                    Context.ConnectionId,
                    uid?.ToString() ?? "<unknown>");

                if (uid.HasValue)
                {
                    var activeGroupCalls = await _callService.GetActiveGroupCallsForUserAsync(uid.Value);
                    if (activeGroupCalls.Success && activeGroupCalls.Data != null)
                    {
                        foreach (var groupCall in activeGroupCalls.Data)
                        {
                            await Clients.Caller.SendAsync("GroupCallUpdated", groupCall);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "CallHub OnConnected logging failed");
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                var userId = GetUserId();
                _logger.LogInformation(
                    "CallHub OnDisconnected: connectionId={ConnId} userId={UserId}",
                    Context.ConnectionId,
                    userId);

                var result = await _callService.EndActiveCallsForUserAsync(userId, "Disconnected");
                if (result.Success && result.Data != null)
                {
                    foreach (var endedCall in result.Data)
                    {
                        if (endedCall.GroupCall != null)
                        {
                            await Clients.Users(ToSignalRUserIds(endedCall.MemberUserIds)).SendAsync(
                                "GroupCallUpdated",
                                endedCall.GroupCall);
                        }

                        if (endedCall.SystemMessageUpdate != null)
                        {
                            await _mainHubContext.Clients.Users(ToSignalRUserIds(endedCall.MemberUserIds)).SendAsync(
                                "ReceiveMessageUpdate",
                                endedCall.SystemMessageUpdate);
                        }

                        if (endedCall.CallEnded)
                        {
                            await Clients.Users(ToSignalRUserIds(endedCall.MemberUserIds)).SendAsync(
                                "CallEnded",
                                endedCall.Ended);
                        }
                    }
                }
                else if (!result.Success)
                {
                    _logger.LogDebug(
                        "CallHub OnDisconnected: active call cleanup skipped for user {UserId}: {Reason}",
                        userId,
                        result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "CallHub OnDisconnected cleanup failed");
            }

            await base.OnDisconnectedAsync(exception);
        }

        [HubMethodName("StartCall")]
        public async Task StartCallAsync(int chatId, string callUid, string? metadata = null)
        {
            var initiatorId = GetUserId();
            _logger.LogInformation(
                "StartCall: initiator={Initiator} chatId={ChatId} callUid={CallUid}",
                initiatorId,
                chatId,
                callUid);

            var result = await _callService.StartCallAsync(initiatorId, chatId, callUid, metadata);
            var call = GetDataOrThrow(result);

            if (call.SystemMessage != null)
            {
                await _mainHubContext.Clients.Users(ToSignalRUserIds(call.MemberUserIds)).SendAsync(
                    "ReceiveMessage",
                    call.SystemMessage);
            }

            if (call.RejectedAsBusy)
            {
                throw new HubException(CallService.LineBusyMessage);
            }

            if (call.IncomingRecipientUserIds.Count > 0)
            {
                await Clients.Users(ToSignalRUserIds(call.IncomingRecipientUserIds)).SendAsync("IncomingCall", call.IncomingCall);
            }

            if (call.GroupCall != null)
            {
                await Clients.Users(ToSignalRUserIds(call.MemberUserIds)).SendAsync(
                    "GroupCallUpdated",
                    call.GroupCall);
            }

            await Clients.User(call.InitiatorId.ToString()).SendAsync("Calling", call.Calling);

            if (call.ExpiresWhenPending)
            {
                SchedulePendingCallTimeout(
                    call.IncomingCall.CallId,
                    call.InitiatorId,
                    call.MemberUserIds);
            }
        }

        [HubMethodName("AcceptCall")]
        public async Task AcceptCallAsync(int callId)
        {
            var userId = GetUserId();
            _logger.LogInformation("AcceptCall: user={User} callId={CallId}", userId, callId);

            var result = await _callService.AcceptCallAsync(userId, callId);
            var accepted = GetDataOrThrow(result);

            await Clients.Users(ToSignalRUserIds(accepted.MemberUserIds)).SendAsync("CallAccepted", accepted.Accepted);

            if (accepted.SystemMessageUpdate != null)
            {
                await _mainHubContext.Clients.Users(ToSignalRUserIds(accepted.GroupCallRecipientUserIds)).SendAsync(
                    "ReceiveMessageUpdate",
                    accepted.SystemMessageUpdate);
            }

            if (accepted.GroupCall != null)
            {
                await Clients.Users(ToSignalRUserIds(accepted.GroupCallRecipientUserIds)).SendAsync(
                    "GroupCallUpdated",
                    accepted.GroupCall);
            }
        }

        [HubMethodName("RejectCall")]
        public async Task RejectCallAsync(int callId, string? reason = null)
        {
            var userId = GetUserId();
            _logger.LogInformation(
                "RejectCall: user={User} callId={CallId} reason={Reason}",
                userId,
                callId,
                reason);

            var result = await _callService.RejectCallAsync(userId, callId, reason);
            var rejected = GetDataOrThrow(result);

            if (rejected.SystemMessageUpdate != null)
            {
                await _mainHubContext.Clients.Users(ToSignalRUserIds(rejected.MemberUserIds)).SendAsync(
                    "ReceiveMessageUpdate",
                    rejected.SystemMessageUpdate);
            }

            if (rejected.GroupCall != null)
            {
                await Clients.Users(ToSignalRUserIds(rejected.MemberUserIds)).SendAsync(
                    "GroupCallUpdated",
                    rejected.GroupCall);
            }

            if (rejected.CallEnded && rejected.Ended != null)
            {
                await Clients.Users(ToSignalRUserIds(rejected.MemberUserIds)).SendAsync("CallEnded", rejected.Ended);
            }

            foreach (var recipientUserId in rejected.RejectedRecipientUserIds)
            {
                try
                {
                    await Clients.User(recipientUserId.ToString()).SendAsync("CallRejected", rejected.Rejected);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(
                        ex,
                        "CallHub RejectCall: failed to notify user {UserId}",
                        recipientUserId);
                }
            }
        }

        [HubMethodName("EndCall")]
        public async Task EndCallAsync(int callId)
        {
            var userId = GetUserId();
            _logger.LogInformation("EndCall: user={User} callId={CallId}", userId, callId);

            var result = await _callService.EndCallAsync(userId, callId);
            var ended = GetDataOrThrow(result);

            if (ended.SystemMessageUpdate != null)
            {
                await _mainHubContext.Clients.Users(ToSignalRUserIds(ended.MemberUserIds)).SendAsync(
                    "ReceiveMessageUpdate",
                    ended.SystemMessageUpdate);
            }

            if (ended.GroupCall != null)
            {
                await Clients.Users(ToSignalRUserIds(ended.MemberUserIds)).SendAsync(
                    "GroupCallUpdated",
                    ended.GroupCall);
            }

            if (ended.CallEnded)
            {
                await Clients.Users(ToSignalRUserIds(ended.MemberUserIds)).SendAsync("CallEnded", ended.Ended);
            }
        }

        [HubMethodName("SetCallMuted")]
        public async Task SetCallMutedAsync(int callId, bool isMuted)
        {
            var userId = GetUserId();
            _logger.LogDebug(
                "SetCallMuted: user={User} callId={CallId} isMuted={IsMuted}",
                userId,
                callId,
                isMuted);

            var result = await _callService.SetParticipantMutedAsync(userId, callId, isMuted);
            var updated = GetDataOrThrow(result);

            await Clients.Users(ToSignalRUserIds(updated.RecipientUserIds)).SendAsync(
                "CallParticipantUpdated",
                updated.Updated);
        }

        [HubMethodName("SendOffer")]
        public async Task SendOfferAsync(int targetUserId, string sdp, string callUid)
        {
            var userId = GetUserId();
            await Clients.User(targetUserId.ToString()).SendAsync("Offer", new { CallUid = callUid, From = userId, Sdp = sdp });
        }

        [HubMethodName("SendAnswer")]
        public async Task SendAnswerAsync(int targetUserId, string sdp, string callUid)
        {
            var userId = GetUserId();
            await Clients.User(targetUserId.ToString()).SendAsync("Answer", new { CallUid = callUid, From = userId, Sdp = sdp });
        }

        [HubMethodName("SendIceCandidate")]
        public async Task SendIceCandidateAsync(int targetUserId, string candidate, string? sdpMid, int? sdpMLineIndex, string callUid)
        {
            var userId = GetUserId();
            await Clients.User(targetUserId.ToString()).SendAsync("IceCandidate", new
            {
                CallUid = callUid,
                From = userId,
                Candidate = candidate,
                SdpMid = sdpMid,
                SdpMLineIndex = sdpMLineIndex
            });
        }

        [HubMethodName("SendAudioChunk")]
        public async Task SendAudioChunkAsync(int? targetUserId, byte[] chunk, int callId, long sequenceId, long timestampMs)
        {
            var userId = GetUserId();
            _logger.LogDebug(
                "SendAudioChunk: from={From} to={To} callId={CallId} bytes={Len} seq={Seq} ts={Ts}",
                userId,
                targetUserId?.ToString() ?? "<all>",
                callId,
                chunk?.Length ?? 0,
                sequenceId,
                timestampMs);

            if (targetUserId.HasValue)
            {
                await Clients.User(targetUserId.Value.ToString()).SendAsync(
                    "AudioChunk",
                    userId,
                    chunk,
                    callId,
                    sequenceId,
                    timestampMs);
                return;
            }

            try
            {
                var recipientsResult = await _callService.GetAudioBroadcastRecipientsAsync(userId, callId);
                if (!recipientsResult.Success)
                {
                    _logger.LogDebug(
                        "SendAudioChunk: failed to resolve recipients for callId={CallId}: {Reason}",
                        callId,
                        recipientsResult.Message);
                    return;
                }

                var recipients = recipientsResult.Data?.RecipientUserIds ?? Array.Empty<int>();
                if (recipients.Count == 0)
                {
                    return;
                }

                await Clients.Users(ToSignalRUserIds(recipients)).SendAsync(
                    "AudioChunk",
                    userId,
                    chunk,
                    callId,
                    sequenceId,
                    timestampMs);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "SendAudioChunk broadcast failed for callId={CallId}", callId);
            }
        }

        private void SchedulePendingCallTimeout(
            int callId,
            int initiatorId,
            IReadOnlyList<int> memberUserIds)
        {
            var recipients = ToSignalRUserIds(memberUserIds);

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(PendingCallTimeout);

                    using var scope = _scopeFactory.CreateScope();
                    var callService = scope.ServiceProvider.GetRequiredService<ICallService>();

                    var result = await callService.MarkPendingCallMissedAsync(callId);
                    if (!result.Success)
                    {
                        _logger.LogDebug(
                            "Call timeout skipped for callId={CallId}: {Reason}",
                            callId,
                            result.Message);
                        return;
                    }

                    if (result.Data?.Missed != true)
                    {
                        return;
                    }

                    await _hubContext.Clients.User(initiatorId.ToString()).SendAsync(
                        "CallRejected",
                        new CallRejectedNotification(callId, null, "No answer"));

                    if (result.Data.SystemMessageUpdate != null)
                    {
                        await _mainHubContext.Clients.Users(recipients).SendAsync(
                            "ReceiveMessageUpdate",
                            result.Data.SystemMessageUpdate);
                    }

                    await _hubContext.Clients.Users(recipients).SendAsync(
                        "CallEnded",
                        new CallEndedNotification(callId, initiatorId));
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error handling call timeout");
                }
            });
        }

        private static T GetDataOrThrow<T>(ServiceResult<T> result)
        {
            if (result.Success && result.Data is not null)
            {
                return result.Data;
            }

            throw new HubException(result.Message ?? "Call operation failed");
        }

        private static IReadOnlyList<string> ToSignalRUserIds(IEnumerable<int> userIds)
        {
            return userIds
                .Select(userId => userId.ToString())
                .ToList();
        }

        private int GetUserId()
        {
            string? userIdClaim = null;

            try
            {
                userIdClaim = Context?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? Context?.User?.Claims?.FirstOrDefault(c => c.Type == "userId")?.Value
                              ?? Context?.User?.Claims?.FirstOrDefault(c => c.Type == "sub")?.Value;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "GetUserId: failed to read claims");
            }

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int parsed))
            {
                throw new HubException("User not authenticated");
            }

            return parsed;
        }
    }
}
