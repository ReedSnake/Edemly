using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using uchat_server.Api.DTOs;
using uchat_server.Api.Services;
using uchat_server.Data;
using uchat_server.Data.Entities;
using uchat_server.Services;
using static uchat_server.Api.DTOs.MessageDtos;
using uchat_server.Api.Middleware; // ITenantProvider
using System.Text.Json;

namespace uchat_server.Hubs
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
            // Same resolution logic as in MainHub: prefer tenant if available, otherwise master
            Company? company = null;
            string source = "none";
            if (_tenantProvider != null && _tenantProvider.IsTenant && _tenantProvider.CurrentCompany != null)
            {
                company = _tenantProvider.CurrentCompany;
                source = "tenantProvider";
            }

            if (company == null)
            {
                try
                {
                    var http = this.Context?.GetHttpContext();
                    if (http != null)
                    {
                        if (http.Items.TryGetValue("TenantCompany", out var item) && item is Company c)
                        {
                            company = c;
                            source = "httpContext.Items";
                        }
                        else
                        {
                            var tenantQuery = http.Request.Query["tenant"].FirstOrDefault();
                            if (!string.IsNullOrWhiteSpace(tenantQuery))
                            {
                                try
                                {
                                    var found = _serverDb.Companies.AsNoTracking().FirstOrDefaultAsync(x => x.Name == tenantQuery).GetAwaiter().GetResult();
                                    if (found != null)
                                    {
                                        company = found;
                                        source = "query-tenant";
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogDebug(ex, "ResolveDbContext: error checking company by tenant query");
                                }
                            }

                            if (company == null)
                            {
                                var path = http.Request?.Path.Value ?? string.Empty;
                                if (!string.IsNullOrWhiteSpace(path))
                                {
                                    var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                                    if (segments.Length > 0)
                                    {
                                        var first = segments[0];
                                        if (!string.Equals(first, "api", StringComparison.OrdinalIgnoreCase) &&
                                            !string.Equals(first, "main", StringComparison.OrdinalIgnoreCase) &&
                                            !string.Equals(first, "hubs", StringComparison.OrdinalIgnoreCase) &&
                                            !string.Equals(first, "swagger", StringComparison.OrdinalIgnoreCase))
                                        {
                                            try
                                            {
                                                var found = _serverDb.Companies.AsNoTracking().FirstOrDefaultAsync(x => x.Name == first).GetAwaiter().GetResult();
                                                if (found != null)
                                                {
                                                    company = found;
                                                    source = "path-first-segment";
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                _logger.LogDebug(ex, "ResolveDbContext: error checking company by path segment");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "ResolveDbContext: error reading HttpContext.Items or Request.Path");
                }
            }

            if (company != null)
            {
                isTenant = true;
                _logger.LogDebug("ResolveDbContext: using tenant DB for company '{Company}' (source: {Source})", company.Name, source);
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
                // Attempt to read user id; if GetUserId throws for unauthenticated, catch and log
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

        // Start a call: read members from resolved DB (tenant preferred), persist call in master DB
        public async Task StartCall(int chatId, string callUid, string? metadata = null)
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
                    // treat as plain string if parsing fails by leaving metadataElement null and storing raw string
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

                // Persist call to master DB to avoid tenant schema mismatch
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

                // Notify the initiator that the call is ringing (calling)
                await Clients.User(initiatorId.ToString()).SendAsync("Calling", new { CallId = call.Id, CallUid = callUid });

                // Schedule a timeout to mark the call as missed if not accepted within 30 seconds
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30));

                        // Re-read call from master DB
                        var pendingCall = await _serverDb.Calls.FindAsync(call.Id);
                        if (pendingCall != null && pendingCall.Status == CallStatus.Pending)
                        {
                            // Update the call status to Missed
                            pendingCall.Status = CallStatus.Missed;
                            pendingCall.EndedAt = DateTime.UtcNow;
                            await _serverDb.SaveChangesAsync();

                            // Notify initiator that call was missed / rejected
                            try
                            {
                                await Clients.User(initiatorId.ToString()).SendAsync("CallRejected", new { CallId = call.Id, UserId = (int?)null, Reason = "No answer" });
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug(ex, "CallHub timeout: failed to notify initiator {InitiatorId}", initiatorId);
                            }

                            // Notify other members that call ended
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

        public async Task AcceptCall(int callId)
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

                // Notify all members that the call has been accepted
                await Clients.Users(memberIds).SendAsync("CallAccepted", new { CallId = callId, UserId = userId });

                call.Status = CallStatus.InProgress;
                await _serverDb.SaveChangesAsync();
            }
            finally
            {
                if (isTenant) membersCtx.Dispose();
            }
        }

        public async Task RejectCall(int callId, string? reason = null)
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

                // Persist call Missed/Ended state
                call.EndedAt = DateTime.UtcNow;
                call.Status = CallStatus.Ended;
                await _serverDb.SaveChangesAsync();

                // Notify all members that the call has ended
                await Clients.Users(memberIds).SendAsync("CallEnded", new { CallId = callId, UserId = userId, Reason = reason });

                // Also send CallRejected to the initiator so they can stop ringing and show message
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

        public async Task EndCall(int callId)
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

        // WebRTC signaling passthrough
        public async Task SendOffer(int targetUserId, string sdp, string callUid)
        {
            var userId = GetUserId();
            await Clients.User(targetUserId.ToString()).SendAsync("Offer", new { CallUid = callUid, From = userId, Sdp = sdp });
        }

        public async Task SendAnswer(int targetUserId, string sdp, string callUid)
        {
            var userId = GetUserId();
            await Clients.User(targetUserId.ToString()).SendAsync("Answer", new { CallUid = callUid, From = userId, Sdp = sdp });
        }

        public async Task SendIceCandidate(int targetUserId, string candidate, string? sdpMid, int? sdpMLineIndex, string callUid)
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

        public async Task SendAudioChunk(int? targetUserId, byte[] chunk, int callId, long sequenceId, long timestampMs)
        {
            var userId = GetUserId();
            _logger.LogDebug("SendAudioChunk: from={From} to={To} callId={CallId} bytes={Len} seq={Seq} ts={Ts}", userId, targetUserId?.ToString() ?? "<all>", callId, chunk?.Length ?? 0, sequenceId, timestampMs);

            // If a specific target is provided, send only to that user
            if (targetUserId.HasValue)
            {
                await Clients.User(targetUserId.Value.ToString()).SendAsync("AudioChunk", userId, chunk, callId, sequenceId, timestampMs);
                return;
            }

            // Otherwise broadcast to all members of the call's chat except the sender
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

                    // exclude sender
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
            // Try common claim types: NameIdentifier (preferred), then custom 'userId', then 'sub'
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
