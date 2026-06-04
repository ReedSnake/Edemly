using Microsoft.AspNetCore.SignalR.Client;
using System.Diagnostics;

namespace Edemly.Client.Realtime
{
    public partial class HubService
    {
        public async Task StartCallAsync(int chatId, string callUid, object? metadata = null)
        {
            var conn = await GetReadyCallConnectionAsync();
            if (conn == null) return;
            try
            {
                var cts = new CancellationTokenSource(HubSettings.ShortOperationTimeout);

                if (metadata is not string)
                {
                    await conn.InvokeAsync(HubMethods.StartCall, chatId, callUid, metadata, cts.Token);
                }
                else
                {
                    var jsonString = (string)metadata;
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(jsonString);

                        await conn.InvokeAsync(HubMethods.StartCall, chatId, callUid, doc.RootElement, cts.Token);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HUB] Failed to parse metadata JSON for StartCall: {ex}");

                        await conn.InvokeAsync(HubMethods.StartCall, chatId, callUid, jsonString, cts.Token);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HUB] StartCall failed: {ex.Message}");
            }
        }

        public async Task AcceptCallAsync(int callId)
        {
            var conn = await GetReadyCallConnectionAsync();
            if (conn == null) return;

            try
            {
                var cts = new CancellationTokenSource(HubSettings.ShortOperationTimeout);
                await conn.InvokeAsync(HubMethods.AcceptCall, callId, cts.Token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HUB] AcceptCall failed: {ex.Message}");
            }
        }

        public async Task RejectCallAsync(int callId, string? reason = null)
        {
            var conn = await GetReadyCallConnectionAsync();
            if (conn == null) return;

            try
            {
                var cts = new CancellationTokenSource(HubSettings.ShortOperationTimeout);
                await conn.InvokeAsync(HubMethods.RejectCall, callId, reason, cts.Token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HUB] RejectCall failed: {ex.Message}");
            }
        }

        public async Task EndCallAsync(int callId)
        {
            var conn = await GetReadyCallConnectionAsync();
            if (conn == null) return;

            try
            {
                await conn.SendAsync(HubMethods.EndCall, callId);
            }
            catch (TaskCanceledException tce)
            {
                Debug.WriteLine($"[HUB] EndCall canceled: {tce.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HUB] EndCall failed: {ex.Message}");
            }
        }

        public async Task SendOfferAsync(int targetUserId, string sdp, string callUid)
        {
            var conn = await GetReadyCallConnectionAsync();
            if (conn == null) return;

            try
            {
                var cts = new CancellationTokenSource(HubSettings.ShortOperationTimeout);
                await conn.InvokeAsync(HubMethods.SendOffer, targetUserId, sdp, callUid, cts.Token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HUB] SendOffer failed: {ex.Message}");
            }
        }

        public async Task SendAnswerAsync(int targetUserId, string sdp, string callUid)
        {
            var conn = await GetReadyCallConnectionAsync();
            if (conn == null) return;

            try
            {
                var cts = new CancellationTokenSource(HubSettings.ShortOperationTimeout);
                await conn.InvokeAsync(HubMethods.SendAnswer, targetUserId, sdp, callUid, cts.Token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HUB] SendAnswer failed: {ex.Message}");
            }
        }

        public async Task SendIceCandidateAsync(int targetUserId, string candidate, string? sdpMid, int? sdpMLineIndex, string callUid)
        {
            var conn = await GetReadyCallConnectionAsync();
            if (conn == null) return;

            try
            {
                var cts = new CancellationTokenSource(HubSettings.ShortOperationTimeout);
                await conn.InvokeAsync(HubMethods.SendIceCandidate, targetUserId, candidate, sdpMid, sdpMLineIndex, callUid, cts.Token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HUB] SendIceCandidate failed: {ex.Message}");
            }
        }

        public async Task SendAudioChunkAsync(int? targetUserId, byte[] chunk, int callId, long sequenceId, long timestampMs)
        {
            var conn = await GetReadyCallConnectionAsync();
            if (conn == null) return;

            _ = SendAudioChunkCoreAsync(conn, targetUserId, chunk, callId, sequenceId, timestampMs);
        }

        private static async Task SendAudioChunkCoreAsync(
            HubConnection conn,
            int? targetUserId,
            byte[] chunk,
            int callId,
            long sequenceId,
            long timestampMs)
        {
            try
            {
                await conn.SendAsync(HubMethods.SendAudioChunk, targetUserId, chunk, callId, sequenceId, timestampMs);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HUB] SendAudioChunk failed: {ex.Message}");
            }
        }

        private async Task<HubConnection?> GetReadyCallConnectionAsync()
        {
            if (!await EnsureCallConnectionAsync())
            {
                System.Diagnostics.Debug.WriteLine("[HUB] Call connection unavailable.");
                return null;
            }

            var conn = _callConnection;

            return conn?.State == HubConnectionState.Connected
                ? conn
                : null;
        }
    }
}