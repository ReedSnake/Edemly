namespace Edemly.Client.Application.Calls;

public enum CallSessionPhase
{
    Idle,
    OutgoingRinging,
    IncomingRinging,
    InCall,
    Ending
}
