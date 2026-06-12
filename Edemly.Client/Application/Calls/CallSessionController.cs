using Edemly.Client.Infrastructure.Realtime;
using Edemly.Contracts.Calls;
using Edemly.Contracts.Realtime;
using System.Diagnostics;

namespace Edemly.Client.Application.Calls;

public sealed class CallSessionController
{
    private readonly Func<IHubService> _hubServiceProvider;
    private readonly Func<int?> _currentUserIdProvider;
    private IHubService? _registeredHub;

    public CallSessionController(
        CallSessionState state,
        Func<IHubService> hubServiceProvider,
        Func<int?> currentUserIdProvider)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        _hubServiceProvider = hubServiceProvider ?? throw new ArgumentNullException(nameof(hubServiceProvider));
        _currentUserIdProvider = currentUserIdProvider ?? throw new ArgumentNullException(nameof(currentUserIdProvider));
    }

    public CallSessionState State { get; }

    public event Action<CallSessionSnapshot>? CallingReceived;
    public event Action<int, int>? CallAcceptedReceived;
    public event Action<int, int, string?>? CallRejectedReceived;
    public event Action<int, int>? CallEndedReceived;
    public event Action<CallParticipantUpdatedEventDto>? CallParticipantUpdatedReceived;
    public event Action<int, byte[], int, long, long>? AudioChunkReceived;
    public event Action<CallSessionSnapshot>? SessionChanged;

    public void RegisterHubHandlers()
    {
        var hub = _hubServiceProvider();
        if (ReferenceEquals(_registeredHub, hub))
        {
            return;
        }

        UnregisterHubHandlers();

        _registeredHub = hub;
        hub.CallingReceived += OnCallingReceived;
        hub.CallAcceptedDetailsReceived += OnCallAcceptedDetails;
        hub.CallAcceptedReceived += OnCallAccepted;
        hub.CallRejectedReceived += OnCallRejected;
        hub.CallEndedReceived += OnCallEnded;
        hub.CallParticipantUpdatedReceived += OnCallParticipantUpdated;
        hub.AudioChunkReceived += OnAudioChunkReceived;
    }

    public void UnregisterHubHandlers()
    {
        if (_registeredHub == null)
        {
            return;
        }

        try { _registeredHub.CallingReceived -= OnCallingReceived; } catch (Exception ex) { Debug.WriteLine($"[CALL SESSION] Unregister Calling failed: {ex}"); }
        try { _registeredHub.CallAcceptedDetailsReceived -= OnCallAcceptedDetails; } catch (Exception ex) { Debug.WriteLine($"[CALL SESSION] Unregister Accepted details failed: {ex}"); }
        try { _registeredHub.CallAcceptedReceived -= OnCallAccepted; } catch (Exception ex) { Debug.WriteLine($"[CALL SESSION] Unregister Accepted failed: {ex}"); }
        try { _registeredHub.CallRejectedReceived -= OnCallRejected; } catch (Exception ex) { Debug.WriteLine($"[CALL SESSION] Unregister Rejected failed: {ex}"); }
        try { _registeredHub.CallEndedReceived -= OnCallEnded; } catch (Exception ex) { Debug.WriteLine($"[CALL SESSION] Unregister Ended failed: {ex}"); }
        try { _registeredHub.CallParticipantUpdatedReceived -= OnCallParticipantUpdated; } catch (Exception ex) { Debug.WriteLine($"[CALL SESSION] Unregister Participant updated failed: {ex}"); }
        try { _registeredHub.AudioChunkReceived -= OnAudioChunkReceived; } catch (Exception ex) { Debug.WriteLine($"[CALL SESSION] Unregister Audio failed: {ex}"); }

        _registeredHub = null;
    }

    public bool ShouldIgnoreIncoming(IncomingCallEventDto incomingCall)
    {
        ArgumentNullException.ThrowIfNull(incomingCall);

        var currentUserId = _currentUserIdProvider();
        if (currentUserId.HasValue && incomingCall.InitiatorId == currentUserId.Value)
        {
            return true;
        }

        var current = State.Current;
        return current.Phase != CallSessionPhase.Idle
            && current.CallId.HasValue
            && current.CallId.Value != incomingCall.CallId;
    }

    public CallSessionSnapshot BeginIncoming(IncomingCallEventDto incomingCall)
    {
        var snapshot = State.SetIncoming(incomingCall);
        SessionChanged?.Invoke(snapshot);
        return snapshot;
    }

    public async Task<CallSessionSnapshot?> AcceptCurrentAsync()
    {
        var current = State.Current;
        if (current.CallId == null)
        {
            return null;
        }

        await _hubServiceProvider().AcceptCallAsync(current.CallId.Value);

        var snapshot = State.SetInCall(
            current.CallId.Value,
            current.CallUid,
            current.ChatId,
            current.InitiatorId,
            current.PeerUserId,
            current.Scope,
            current.MediaKind,
            current.Participants);

        SessionChanged?.Invoke(snapshot);
        return snapshot;
    }

    public async Task RejectCurrentAsync(string? reason)
    {
        var current = State.Current;
        if (current.CallId == null)
        {
            return;
        }

        await _hubServiceProvider().RejectCallAsync(current.CallId.Value, reason);
        var snapshot = State.Clear(reason);
        SessionChanged?.Invoke(snapshot);
    }

    public async Task EndCurrentAsync(string? reason)
    {
        var current = State.Current;
        if (current.CallId == null)
        {
            return;
        }

        State.MarkEnding(reason);
        await _hubServiceProvider().EndCallAsync(current.CallId.Value);

        var snapshot = State.Clear(reason);
        SessionChanged?.Invoke(snapshot);
    }

    public Task CloseCurrentAsync(string? reason)
    {
        var current = State.Current;
        return current.Phase == CallSessionPhase.IncomingRinging
            ? RejectCurrentAsync(reason)
            : EndCurrentAsync(reason);
    }

    public Task SendAudioChunkAsync(int? targetUserId, byte[] chunk, int callId, long sequenceId, long timestampMs)
    {
        return _hubServiceProvider().SendAudioChunkAsync(targetUserId, chunk, callId, sequenceId, timestampMs);
    }

    public async Task SetMutedCurrentAsync(bool isMuted)
    {
        var current = State.Current;
        if (current.CallId == null)
        {
            return;
        }

        var currentUserId = _currentUserIdProvider();
        if (currentUserId.HasValue)
        {
            var snapshot = State.SetParticipantMuted(current.CallId.Value, currentUserId.Value, isMuted);
            SessionChanged?.Invoke(snapshot);
        }

        await _hubServiceProvider().SetCallMutedAsync(current.CallId.Value, isMuted);
    }

    public async Task<CallSessionSnapshot?> JoinGroupCallAsync(GroupCallEventDto groupCall)
    {
        ArgumentNullException.ThrowIfNull(groupCall);

        var current = State.Current;
        if (current.Phase != CallSessionPhase.Idle
            && current.CallId.HasValue
            && current.CallId.Value != groupCall.CallId)
        {
            throw new InvalidOperationException("Line busy");
        }

        if (current.CallId == groupCall.CallId && current.Phase == CallSessionPhase.InCall)
        {
            return current;
        }

        await _hubServiceProvider().AcceptCallAsync(groupCall.CallId);
        var participants = EnsureCurrentUserJoined(groupCall.Participants);

        var snapshot = State.SetInCall(
            groupCall.CallId,
            groupCall.CallUid,
            groupCall.ChatId,
            groupCall.InitiatorId,
            null,
            groupCall.Scope,
            groupCall.MediaKind,
            participants);

        SessionChanged?.Invoke(snapshot);
        return snapshot;
    }

    private IReadOnlyList<CallParticipantDto> EnsureCurrentUserJoined(IReadOnlyList<CallParticipantDto> participants)
    {
        var currentUserId = _currentUserIdProvider();
        if (!currentUserId.HasValue)
        {
            return participants;
        }

        var now = DateTime.UtcNow;
        var found = false;
        var next = participants
            .Select(participant =>
            {
                if (participant.UserId != currentUserId.Value)
                {
                    return participant;
                }

                found = true;
                return new CallParticipantDto
                {
                    UserId = participant.UserId,
                    Status = Edemly.Contracts.Calls.CallParticipantStatuses.Joined,
                    InvitedAt = participant.InvitedAt ?? now,
                    JoinedAt = participant.JoinedAt ?? now,
                    LeftAt = null,
                    IsMuted = participant.IsMuted
                };
            })
            .ToList();

        if (!found)
        {
            next.Add(new CallParticipantDto
            {
                UserId = currentUserId.Value,
                Status = Edemly.Contracts.Calls.CallParticipantStatuses.Joined,
                InvitedAt = now,
                JoinedAt = now,
                IsMuted = false
            });
        }

        return next;
    }

    private void OnCallingReceived(CallingEventDto calling)
    {
        var snapshot = State.SetOutgoing(calling);
        SessionChanged?.Invoke(snapshot);
        CallingReceived?.Invoke(snapshot);
    }

    private void OnCallAccepted(int callId, int userId)
    {
        var current = State.Current;
        if (current.CallId != callId)
        {
            return;
        }

        if (current.CallId == callId)
        {
            var snapshot = State.MarkAccepted(callId, userId, ResolvePeerUserId(current, userId));
            SessionChanged?.Invoke(snapshot);
        }

        CallAcceptedReceived?.Invoke(callId, userId);
    }

    private void OnCallAcceptedDetails(CallAcceptedEventDto accepted)
    {
        var current = State.Current;
        if (current.CallId != accepted.CallId)
        {
            return;
        }

        var snapshot = State.SetInCall(
            accepted.CallId,
            current.CallUid,
            current.ChatId,
            current.InitiatorId,
            ResolvePeerUserId(current, accepted.UserId),
            accepted.Scope,
            accepted.MediaKind,
            accepted.Participants);

        SessionChanged?.Invoke(snapshot);
    }

    private int? ResolvePeerUserId(CallSessionSnapshot current, int acceptedUserId)
    {
        if (current.IsGroupCall)
        {
            return null;
        }

        var currentUserId = _currentUserIdProvider();
        if (current.PeerUserId.HasValue
            && (!currentUserId.HasValue || current.PeerUserId.Value != currentUserId.Value))
        {
            return current.PeerUserId;
        }

        if (current.InitiatorId.HasValue
            && (!currentUserId.HasValue || current.InitiatorId.Value != currentUserId.Value))
        {
            return current.InitiatorId;
        }

        return acceptedUserId;
    }

    private void OnCallRejected(int callId, int userId, string? reason)
    {
        var current = State.Current;
        if (current.CallId != callId)
        {
            return;
        }

        var snapshot = State.Clear(reason);
        SessionChanged?.Invoke(snapshot);
        CallRejectedReceived?.Invoke(callId, userId, reason);
    }

    private void OnCallEnded(int callId, int userId)
    {
        var current = State.Current;
        if (current.CallId != callId)
        {
            return;
        }

        var snapshot = State.Clear();
        SessionChanged?.Invoke(snapshot);
        CallEndedReceived?.Invoke(callId, userId);
    }

    private void OnCallParticipantUpdated(CallParticipantUpdatedEventDto updated)
    {
        var current = State.Current;
        if (current.CallId != updated.CallId)
        {
            return;
        }

        var snapshot = State.SetParticipantMuted(
            updated.CallId,
            updated.UserId,
            updated.IsMuted,
            updated.Participants);

        SessionChanged?.Invoke(snapshot);
        CallParticipantUpdatedReceived?.Invoke(updated);
    }

    private void OnAudioChunkReceived(int fromUserId, byte[] chunk, int callId, long sequenceId, long timestampMs)
    {
        AudioChunkReceived?.Invoke(fromUserId, chunk, callId, sequenceId, timestampMs);
    }
}
