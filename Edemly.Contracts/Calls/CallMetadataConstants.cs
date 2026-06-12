namespace Edemly.Contracts.Calls;

public static class CallScopes
{
    public const string Direct = "Direct";
    public const string Group = "Group";
}

public static class CallMediaKinds
{
    public const string Audio = "Audio";
    public const string Video = "Video";
}

public static class CallParticipantStatuses
{
    public const string Invited = "Invited";
    public const string Ringing = "Ringing";
    public const string Joined = "Joined";
    public const string Left = "Left";
    public const string Rejected = "Rejected";
    public const string Missed = "Missed";
}

public static class CallLifecycleStatuses
{
    public const string Pending = "Pending";
    public const string Active = "Active";
    public const string Ended = "Ended";
    public const string Rejected = "Rejected";
    public const string Missed = "Missed";
}
