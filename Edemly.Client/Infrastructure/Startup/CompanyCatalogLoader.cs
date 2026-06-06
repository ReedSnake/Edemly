#nullable enable

using System.Net.Http;
using System.Text.Json;

using Edemly.Client.Infrastructure.Realtime;

namespace Edemly.Client.Infrastructure.Startup
{
    public sealed class CompanyCatalogLoader : ICompanyCatalogLoader
    {
        public async Task<IReadOnlyList<CompanyCatalogEntry>> LoadAsync(string baseUrl, CancellationToken cancellationToken = default)
        {
            using var httpClient = new HttpClient
            {
                Timeout = HubSettings.ShortOperationTimeout
            };

            var response = await httpClient.GetAsync($"{baseUrl.TrimEnd('/')}/api/admin/companies", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Array.Empty<CompanyCatalogEntry>();
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var companies = JsonSerializer.Deserialize<List<CompanyResponse>>(
                                json,
                                new JsonSerializerOptions
                                {
                                    PropertyNameCaseInsensitive = true
                                })
                            ?? new List<CompanyResponse>();

            return companies
                .Where(company => !string.IsNullOrWhiteSpace(company.Name))
                .Select(company => new CompanyCatalogEntry(company.Id, company.Name!))
                .OrderBy(company => company.Name)
                .ToList();
        }

        private sealed class CompanyResponse
        {
            public int Id { get; set; }

            public string? Name { get; set; }
        }
    }
}
