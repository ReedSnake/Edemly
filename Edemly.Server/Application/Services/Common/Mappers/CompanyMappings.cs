using Edemly.Server.Data.Entities;

namespace Edemly.Server.Api.Services
{
    public static class CompanyMappings
    {
        public static CompanyListItemDto ToListItemDto(Company company)
        {
            return new CompanyListItemDto
            {
                Id = company.Id,
                Name = company.Name ?? string.Empty
            };
        }
    }
}