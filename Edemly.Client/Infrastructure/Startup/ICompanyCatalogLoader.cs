#nullable enable

namespace Edemly.Client.Infrastructure.Startup
{
    public interface ICompanyCatalogLoader
    {
        Task<IReadOnlyList<CompanyCatalogEntry>> LoadAsync(string baseUrl, CancellationToken cancellationToken = default);
    }
}
