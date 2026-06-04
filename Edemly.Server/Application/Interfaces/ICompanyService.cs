using Edemly.Contracts.Companies;

namespace Edemly.Server.Api.Services
{
    public interface ICompanyService
    {
        Task<ServiceResult<List<CompanyListItemDto>>> GetCompaniesAsync();

        Task<ServiceResult<CompanyListItemDto>> CreateAsync(CreateCompanyDto request);

        Task<ServiceResult> AddEmailsAsync(int companyId, List<string>? emails);
    }
}