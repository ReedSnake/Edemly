using Edemly.Contracts.Calls;
using Edemly.Contracts.Realtime;

namespace Edemly.Client.Application.Calls;

public sealed class CallSessionState
{
    private readonly object _sync = new();
    private CallSessionSnapshot _current = CallSessionSnapshot.Idle;

    public CallSessionSnapshot Current
    {
        get
        {
            lock (_sync)
            {
                return _current;
            }
        }
    }

    public bool HasActiveSession => Current.Phase != CallSessionPhase.Idle;

    public CallSessionSnapshot SetOutgoing(CallingEventDto calling)
    {
        ArgumentNullException.ThrowIfNull(calling);

        var phase = calling.Scope == CallScopes.Group
            ? CallSessionPhase.InCall
            : CallSessionPhase.OutgoingRinging;

        return Set(new CallSessionSnapshot(
            phase,
            calling.CallId,
            calling.CallUid,
            calling.ChatId,
            calling.InitiatorId,
            null,
            calling.Scope,
            calling.MediaKind,
            calling.Participants,
            null));
    }

    public CallSessionSnapshot SetIncoming(IncomingCallEventDto incomingCall)
    {
        ArgumentNullException.ThrowIfNull(incomingCall);

        return Set(new CallSessionSnapshot(
            CallSessionPhase.IncomingRinging,
            incomingCall.CallId,
            incomingCall.CallUid,
            incomingCall.ChatId,
            incomingCall.InitiatorId,
            incomingCall.InitiatorId,
            incomingCall.Scope,
            incomingCall.MediaKind,
            incomingCall.Participants,
            null));
    }

    public CallSessionSnapshot SetInCall(
        int callId,
        string? callUid,
        int? chatId,
        int? initiatorId,
        int? peerUserId = null,
        string? scope = null,
        string? mediaKind = null,
        IReadOnlyList<CallParticipantDto>? participants = null)
    {
        var current = Current;

        return Set(new CallSessionSnapshot(
            CallSessionPhase.InCall,
            callId,
            callUid,
            chatId,
            initiatorId,
            peerUserId,
            scope ?? current.Scope,
            mediaKind ?? current.MediaKind,
            participants ?? current.Participants,
            null));
    }

    public CallSessionSnapshot MarkAccepted(int callId, int acceptedUserId, int? peerUserId = null)
    {
        lock (_sync)
        {
            if (_current.CallId != callId)
            {
                return _current;
            }

            _current = _current with
            {
                Phase = CallSessionPhase.InCall,
                PeerUserId = _current.IsGroupCall ? null : peerUserId ?? acceptedUserId,
                Participants = MarkParticipantsJoined(
                    _current.Participants,
                    acceptedUserId,
                    _current.InitiatorId),
                Reason = null
            };

            return _current;
        }
    }

    public CallSessionSnapshot MarkEnding(string? reason = null)
    {
        lock (_sync)
        {
            if (_current.Phase == CallSessionPhase.Idle)
            {
                return _current;
            }

            _current = _current with
            {
                Phase = CallSessionPhase.Ending,
                Reason = reason
            };

            return _current;
        }
    }

    public CallSessionSnapshot Clear(string? reason = null)
    {
        lock (_sync)
        {
            _current = reason is null
                ? CallSessionSnapshot.Idle
                : CallSessionSnapshot.Idle with { Reason = reason };

            return _current;
        }
    }

    private CallSessionSnapshot Set(CallSessionSnapshot snapshot)
    {
        lock (_sync)
        {
            _current = snapshot;
            return _current;
        }
    }

    private static IReadOnlyList<CallParticipantDto> MarkParticipantsJoined(
        IReadOnlyList<CallParticipantDto> participants,
        params int?[] userIds)
    {
        var joinedUserIds = userIds
            .Where(userId => userId.HasValue)
            .Select(userId => userId!.Value)
            .Distinct()
            .ToHashSet();

        if (joinedUserIds.Count == 0)
        {
            return participants;
        }

        var now = DateTime.UtcNow;
        var next = participants
            .Select(participant => new CallParticipantDto
            {
                UserId = participant.UserId,
                Status = joinedUserIds.Contains(participant.UserId)
                    ? CallParticipantStatuses.Joined
                    : participant.Status,
                InvitedAt = participant.InvitedAt,
                JoinedAt = joinedUserIds.Contains(participant.UserId)
                    ? participant.JoinedAt ?? now
                    : participant.JoinedAt,
                LeftAt = joinedUserIds.Contains(participant.UserId)
                    ? null
                    : participant.LeftAt,
                IsMuted = participant.IsMuted
            })
            .ToList();

        foreach (var userId in joinedUserIds.Where(userId => next.All(participant => participant.UserId != userId)))
        {
            next.Add(new CallParticipantDto
            {
                UserId = userId,
                Status = CallParticipantStatuses.Joined,
                InvitedAt = now,
                JoinedAt = now,
                IsMuted = false
            });
        }

        return next;
    }

    public CallSessionSnapshot SetParticipantMuted(
        int callId,
        int userId,
        bool isMuted,
        IReadOnlyList<CallParticipantDto>? participants = null)
    {
        lock (_sync)
        {
            if (_current.CallId != callId)
            {
                return _current;
            }

            var nextParticipants = participants is { Count: > 0 }
                ? participants
                : _current.Participants
                    .Select(participant => new CallParticipantDto
                    {
                        UserId = participant.UserId,
                        Status = participant.Status,
                        InvitedAt = participant.InvitedAt,
                        JoinedAt = participant.JoinedAt,
                        LeftAt = participant.LeftAt,
                        IsMuted = participant.UserId == userId ? isMuted : participant.IsMuted
                    })
                    .ToList();

            _current = _current with
            {
                Participants = nextParticipants,
                Reason = null
            };

            return _current;
        }
    }
}
