using Edemly.Contracts.Calls;

namespace Edemly.Client.Api.Calls;

public interface ICallApiClient
{
    Task<List<CallDto>> GetActiveCallsAsync();
}