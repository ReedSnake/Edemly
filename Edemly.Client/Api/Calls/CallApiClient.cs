using Edemly.Client.Api.Core;

namespace Edemly.Client.Api.Calls;

public sealed class CallApiClient : ApiClientBase, ICallApiClient
{
    public CallApiClient(ApiClientContext context) : base(context)
    {

    }

    public async Task<List<CallDto>> GetActiveCallsAsync()
    {
        return await GetAsync<List<CallDto>>("api/calls/active") ?? new();
    }
}

