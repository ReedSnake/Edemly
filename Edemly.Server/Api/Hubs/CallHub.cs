using Edemly.Server.Api.Middleware; // ITenantProvider
using Edemly.Server.Api.Services;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace Edemly.Server.Api.Hubs
{
    [Authorize]
    public class CallHub : Hub
    {
        private readonly IMessageService _messageService;
        private readonly IRemindingService _remindingService;
        private readonly ServerDbContext _serverDb;
        private readonly Services.UserPresenceService _presenceService;
        private readonly ILogger<CallHub> _logger;
        private readonly ITenantProvider _tenantProvider;
        private readonly ITenantDbContextFactory _tenantDbFactory;

        public CallHub(
            IRemindingService remindingService,
            IMessageService messageService,
            ServerDbContext serverDb,
            Services.UserPresenceService presenceService,
            ILogger<CallHub> logger,
            ITenantProvider tenantProvider,
            ITenantDbContextFactory tenantDbFactory)
        {
            _messageService = messageService;
            _remindingService = remindingService;
            _serverDb = serverDb;
            _presenceService = presenceService;
            _logger = logger;
            _tenantProvider = tenantProvider;
            _tenantDbFactory = tenantDbFactory;
        }

        private DbContext ResolveDbContext(out bool isTenant)
        {
            var company = TenantRequestContext.GetCurrentCompany(Context?.GetHttpContext(), _tenantProvider);

            if (company != null)
            {
                isTenant = true;
                _logger.LogDebug("ResolveDbContext: using tenant DB for company '{Company}'", company.Name);
                return _tenantDbFactory.CreateCompanyDbContext(company);
            }

            isTenant = false;
            _logger.LogDebug("ResolveDbContext: using master DB (no tenant resolved)");
            return _serverDb;
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
                catch (Exception) { /* ignore here - log below */ }

                _logger.LogInformation("CallHub OnConnected: connectionId={ConnId} userId={UserId}", Context.ConnectionId, uid?.ToString() ?? "<unknown>");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "CallHub OnConnected logging failed");
            }

            await base.OnConnectedAsync();
        }

        [HubMethodName("StartCall")]
        public async Task StartCallAsync(int chatId, string callUid, string? metadata = null)
        {
            var initiatorId = GetUserId();
            _logger.LogInformation("StartCall: initiator={Initiator} chatId={ChatId} callUid={CallUid}", initiatorId, chatId, callUid);

            JsonElement? metadataElement = null;
            if (!string.IsNullOrWhiteSpace(metadata))
            {
                try
                {
                    using var doc = JsonDocument.Parse(metadata);
                    metadataElement = doc.RootElement.Clone();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "StartCall: failed to parse metadata JSON string");
                }
            }

            DbContext membersCtx = ResolveDbContext(out var isTenant);
            try
            {
                var memberIds = await membersCtx.Set<ChatMember>()
                    .Where(cm => cm.ChatId == chatId)
                    .Select(cm => cm.UserId)
                    .ToListAsync();

                if (memberIds == null || memberIds.Count == 0)
                    throw new HubException("Chat has no members");

                var call = new Call
                {
                    ChatId = chatId,
                    InitiatorId = initiatorId,
                    CallUid = callUid,
                    Metadata = metadataElement.HasValue ? metadataElement.Value.GetRawText() : metadata,
                    StartedAt = DateTime.UtcNow,
                    Status = CallStatus.Pending // Set to Pending initially
                };

                _serverDb.Calls.Add(call);
                await _serverDb.SaveChangesAsync();

                var payload = new
                {
                    CallId = call.Id,
                    CallUid = callUid,
                    ChatId = chatId,
                    InitiatorId = initiatorId,
                    Metadata = call.Metadata,
                    StartedAt = call.StartedAt
                };

                var userStrings = memberIds.Select(id => id.ToString()).ToList();
                await Clients.Users(userStrings).SendAsync("IncomingCall", payload);

                await Clients.User(initiatorId.ToString()).SendAsync("Calling", new { CallId = call.Id, CallUid = callUid });

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30));

                        var pendingCall = await _serverDb.Calls.FindAsync(call.Id);
                        if (pendingCall != null && pendingCall.Status == CallStatus.Pending)
                        {
                            pendingCall.Status = CallStatus.Missed;
                            pendingCall.EndedAt = DateTime.UtcNow;
                            await _serverDb.SaveChangesAsync();

                            try
                            {
                                await Clients.User(initiatorId.ToString()).SendAsync("CallRejected", new { CallId = call.Id, UserId = (int?)null, Reason = "No answer" });
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug(ex, "CallHub timeout: failed to notify initiator {InitiatorId}", initiatorId);
                            }

                            try
                            {
                                await Clients.Users(userStrings).SendAsync("CallEnded", new { CallId = call.Id, UserId = initiatorId });
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug(ex, "CallHub timeout: failed to notify members for CallId={CallId}", call.Id);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error handling call timeout");
                    }
                });
            }
            finally
            {
                if (isTenant) membersCtx.Dispose();
            }
        }

        [HubMethodName("AcceptCall")]
        public async Task AcceptCallAsync(int callId)
        {
            var userId = GetUserId();
            _logger.LogInformation("AcceptCall: user={User} callId={CallId}", userId, callId);

            var call = await _serverDb.Calls.FindAsync(callId);
            if (call == null) throw new HubException("Call not found");

            DbContext membersCtx = ResolveDbContext(out var isTenant);
            try
            {
                var memberIds = await membersCtx.Set<ChatMember>()
                    .Where(cm => cm.ChatId == call.ChatId)
                    .Select(cm => cm.UserId.ToString())
                    .ToListAsync();

                await Clients.Users(memberIds).SendAsync("CallAccepted", new { CallId = callId, UserId = userId });

                call.Status = CallStatus.InProgress;
                await _serverDb.SaveChangesAsync();
            }
            finally
            {
                if (isTenant) membersCtx.Dispose();
            }
        }

        [HubMethodName("RejectCall")]
        public async Task RejectCallAsync(int callId, string? reason = null)
        {
            var userId = GetUserId();
            _logger.LogInformation("RejectCall: user={User} callId={CallId} reason={Reason}", userId, callId, reason);

            var call = await _serverDb.Calls.FindAsync(callId);
            if (call == null) throw new HubException("Call not found");

            DbContext membersCtx = ResolveDbContext(out var isTenant);
            try
            {
                var memberIds = await membersCtx.Set<ChatMember>()
                    .Where(cm => cm.ChatId == call.ChatId)
                    .Select(cm => cm.UserId.ToString())
                    .ToListAsync();

                call.EndedAt = DateTime.UtcNow;
                call.Status = CallStatus.Ended;
                await _serverDb.SaveChangesAsync();

                await Clients.Users(memberIds).SendAsync("CallEnded", new { CallId = callId, UserId = userId, Reason = reason });

                try
                {
                    await Clients.User(call.InitiatorId.ToString()).SendAsync("CallRejected", new { CallId = callId, UserId = userId, Reason = reason });
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "CallHub RejectCall: failed to notify initiator {Initiator}", call.InitiatorId);
                }
            }
            finally
            {
                if (isTenant) membersCtx.Dispose();
            }
        }

        [HubMethodName("EndCall")]
        public async Task EndCallAsync(int callId)
        {
            var userId = GetUserId();
            _logger.LogInformation("EndCall: user={User} callId={CallId}", userId, callId);

            var call = await _serverDb.Calls.FindAsync(callId);
            if (call == null) throw new HubException("Call not found");

            call.EndedAt = DateTime.UtcNow;
            call.Status = CallStatus.Ended;
            await _serverDb.SaveChangesAsync();

            DbContext membersCtx = ResolveDbContext(out var isTenant);
            try
            {
                var memberIds = await membersCtx.Set<ChatMember>()
                    .Where(cm => cm.ChatId == call.ChatId)
                    .Select(cm => cm.UserId.ToString())
                    .ToListAsync();

                await Clients.Users(memberIds).SendAsync("CallEnded", new { CallId = callId, UserId = userId });
            }
            finally
            {
                if (isTenant) membersCtx.Dispose();
            }
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
            _logger.LogDebug("SendAudioChunk: from={From} to={To} callId={CallId} bytes={Len} seq={Seq} ts={Ts}", userId, targetUserId?.ToString() ?? "<all>", callId, chunk?.Length ?? 0, sequenceId, timestampMs);

            if (targetUserId.HasValue)
            {
                await Clients.User(targetUserId.Value.ToString()).SendAsync("AudioChunk", userId, chunk, callId, sequenceId, timestampMs);
                return;
            }

            var call = await _serverDb.Calls.FindAsync(callId);
            if (call == null)
            {
                _logger.LogDebug("SendAudioChunk: call not found for callId={CallId}", callId);
                return;
            }

            try
            {
                DbContext membersCtx = ResolveDbContext(out var isTenant);
                try
                {
                    var memberIds = await membersCtx.Set<ChatMember>()
                        .Where(cm => cm.ChatId == call.ChatId)
                        .Select(cm => cm.UserId.ToString())
                        .ToListAsync();

                    var recipients = memberIds.Where(id => id != userId.ToString()).ToList();
                    if (recipients.Count == 0) return;

                    await Clients.Users(recipients).SendAsync("AudioChunk", userId, chunk, callId, sequenceId, timestampMs);
                }
                finally
                {
                    if (isTenant) membersCtx.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "SendAudioChunk broadcast failed for callId={CallId}", callId);
            }
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
                throw new HubException("User not authenticated");

            return parsed;
        }
    }
}