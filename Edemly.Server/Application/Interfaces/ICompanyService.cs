using Edemly.Contracts.Companies;

namespace Edemly.Server.Api.Services
{
    public interface ICompanyService
    {
        Task<ServiceDataResult<List<CompanyListItemDto>>> GetCompanies();
        Task<ServiceDataResult<CompanyListItemDto>> CreateCompany(CreateCompanyDto model);
        Task<ServiceMessageResult> AddEmails(int companyId, List<string>? emails);
    }
}