#nullable enable

namespace Edemly.Client.Infrastructure.Legal
{
    public interface ILegalDocumentLoader
    {
        Task<string> LoadPoliciesAsync();
    }
}
