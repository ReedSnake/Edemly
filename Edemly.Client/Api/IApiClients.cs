using Edemly.Client.Api.Calls;
using Edemly.Client.Api.ChatMember;
using Edemly.Client.Api.Chats;
using Edemly.Client.Api.Files;
using Edemly.Client.Api.Messages;
using Edemly.Client.Api.Notes;
using Edemly.Client.Api.Payments;
using Edemly.Client.Api.Remindings;
using Edemly.Client.Api.Users;

namespace Edemly.Client.Api;

public interface IApiClients
{
    IUserApiClient Users { get; }
    IChatApiClient Chats { get; }
    ICallApiClient Calls { get; }
    INoteApiClient Notes { get; }
    IFileApiClient Files { get; }
    IPaymentApiClient Payments { get; }
    IRemindingApiClient Remindings { get; }
    IChatMembersApiClient ChatMembers { get; }
    IMessageApiClient Messages { get; }
}