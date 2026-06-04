using Edemly.Contracts.Realtime;
using Microsoft.AspNetCore.SignalR.Client;
using System.Diagnostics;

namespace Edemly.Client.Realtime
{
    public partial class HubService
    {
        private void RegisterCallHandlers(HubConnection conn)
        {
            if (conn == null) return;

            Debug.WriteLine($"[HUB] RegisterCallHandlers called for connection (State={conn.State})");

            lock (_stateLock)
            {
                if (_callHandlersRegisteredSet.Contains(conn))
                {
                    Debug.WriteLine("[HUB] Call handlers already registered for this connection instance");
                    return;
                }

                _callHandlersRegisteredSet.Add(conn);
            }

            conn.On<object>(HubMethods.IncomingCall, data =>
            {
                var incoming = HubPayloadParser.Deserialize<IncomingCallEventDto>(
                    data,
                    HubMethods.IncomingCall);

                if (incoming != null)
                {
                    Debug.WriteLine($"[HUB] Parsed IncomingCall -> CallId={incoming.CallId}, ChatId={incoming.ChatId}, Initiator={incoming.InitiatorId}");

                    HubEventDispatcher.Dispatch(() =>
                        IncomingCallReceived?.Invoke(incoming));
                }
            });

            conn.On<object>(HubMethods.Calling, data =>
            {
                if (!HubPayloadParser.TryDeserialize<CallingEventDto>(
                        data,
                        HubMethods.Calling,
                        out var payload) ||
                    payload == null ||
                    payload.CallId == 0)
                {
                    return;
                }

                HubEventDispatcher.Dispatch(() =>
                    CallingReceived?.Invoke(payload.CallId, payload.CallUid));
            });

            conn.On<object>(HubMethods.CallAccepted, data =>
            {
                var d = HubPayloadParser.Deserialize<CallSimpleEventDto>(
                    data,
                    HubMethods.CallAccepted);

                if (d != null)
                {
                    HubEventDispatcher.Dispatch(() =>
                        CallAcceptedReceived?.Invoke(d.CallId, d.UserId));
                }
            });

            conn.On<object>(HubMethods.CallRejected, data =>
            {
                var d = HubPayloadParser.Deserialize<CallRejectedEventDto>(
                    data,
                    HubMethods.CallRejected);

                if (d != null)
                {
                    HubEventDispatcher.Dispatch(() =>
                        CallRejectedReceived?.Invoke(d.CallId, d.UserId, d.Reason));
                }
            });

            conn.On<object>(HubMethods.CallEnded, data =>
            {
                var d = HubPayloadParser.Deserialize<CallSimpleEventDto>(
                    data,
                    HubMethods.CallEnded);

                if (d != null)
                {
                    HubEventDispatcher.Dispatch(() =>
                        CallEndedReceived?.Invoke(d.CallId, d.UserId));
                }
            });

            conn.On<object>(HubMethods.Offer, data =>
            {
                var d = HubPayloadParser.Deserialize<SignalDataDto>(
                    data,
                    HubMethods.Offer);

                if (d != null)
                {
                    HubEventDispatcher.Dispatch(() =>
                        OfferReceived?.Invoke(d));
                }
            });

            conn.On<object>(HubMethods.Answer, data =>
            {
                var d = HubPayloadParser.Deserialize<SignalDataDto>(
                    data,
                    HubMethods.Answer);

                if (d != null)
                {
                    HubEventDispatcher.Dispatch(() =>
                        AnswerReceived?.Invoke(d));
                }
            });

            conn.On<object>(HubMethods.IceCandidate, data =>
            {
                var d = HubPayloadParser.Deserialize<SignalIceDto>(
                    data,
                    HubMethods.IceCandidate);

                if (d != null)
                {
                    HubEventDispatcher.Dispatch(() =>
                        IceCandidateReceived?.Invoke(d));
                }
            });

            conn.On<int, byte[], int, long, long>(
                HubMethods.AudioChunk,
                (fromUserId, chunk, callId, sequenceId, timestampMs) =>
                {
                    try
                    {
                        HubEventDispatcher.Dispatch(() =>
                            AudioChunkReceived?.Invoke(
                                fromUserId,
                                chunk,
                                callId,
                                sequenceId,
                                timestampMs));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[HUB SERVICE] Failed handling AudioChunk: {ex.Message}");
                    }
                });

            conn.Closed += async ex => await OnConnectionClosedInternalAsync(conn, ex);
            conn.Reconnecting += ex => OnReconnectingInternal(conn, ex);
            conn.Reconnected += id => OnReconnectedInternal(conn, id);
        }

        private void UnregisterCallHandlers(HubConnection? conn)
        {
            if (conn == null) return;

            try
            {
                conn.Remove(HubMethods.IncomingCall);
                conn.Remove(HubMethods.Calling);
                conn.Remove(HubMethods.CallAccepted);
                conn.Remove(HubMethods.CallRejected);
                conn.Remove(HubMethods.CallEnded);
                conn.Remove(HubMethods.Offer);
                conn.Remove(HubMethods.Answer);
                conn.Remove(HubMethods.IceCandidate);
                conn.Remove(HubMethods.AudioChunk);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HUB] Failed to unregister call handlers: {ex}");
            }

            lock (_stateLock)
            {
                try
                {
                    _callHandlersRegisteredSet.Remove(conn);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HUB] Failed to remove connection from registered call handlers: {ex}");
                }
            }
        }
    }
}