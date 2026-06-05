#nullable enable

namespace Edemly.Client.Application.Profiles
{
    public sealed record UserProfileUpdateRequest(
        string Username,
        string FirstName,
        string LastName,
        string PhoneNumber,
        string Description,
        string PfpUrl);
}
