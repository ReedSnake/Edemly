using Edemly.Contracts.Companies;
using Edemly.Server.Application.Common;

namespace Edemly.Server.Application.Companies
{
    public interface ICompanyService
    {
        Task<ServiceResult<List<CompanyListItemDto>>> GetCompaniesAsync();

        Task<ServiceResult<CompanyListItemDto>> CreateAsync(CreateCompanyDto request);

        Task<ServiceResult> AddEmailsAsync(int companyId, List<string>? emails);
    }
}