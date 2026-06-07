using Edemly.Client.Api.Calls;
using Edemly.Client.Api.Chats;
using Edemly.Client.Api.Core;
using Edemly.Client.Api.Files;
using Edemly.Client.Api.Notes;
using Edemly.Client.Api.Payments;
using Edemly.Client.Api.Remindings;
using Edemly.Client.Api.Users;

namespace Edemly.Client.Api;

public sealed class ApiClients : IApiClients
{
    public ApiClients(ApiClientContext context)
    {
        Users = new UserApiClient(context);
        Chats = new ChatApiClient(context);
        Calls = new CallApiClient(context);
        Notes = new NoteApiClient(context, () => null);
        Files = new FileApiClient(context);
        Payments = new PaymentApiClient(context);
        Remindings = new RemindingApiClient(context);
    }

    public IUserApiClient Users { get; }
    public IChatApiClient Chats { get; }
    public ICallApiClient Calls { get; }
    public INoteApiClient Notes { get; }
    public IFileApiClient Files { get; }
    public IPaymentApiClient Payments { get; }
    public IRemindingApiClient Remindings { get; }
}