using Edemly.Contracts.Companies;

namespace Edemly.Server.Api.Services
{
    public class CompanyService : ICompanyService
    {
        private readonly TenantProvisioningService _provisioningService;
        private readonly ILogger<CompanyService> _logger;

        public CompanyService(
            TenantProvisioningService provisioningService,
            ILogger<CompanyService> logger)
        {
            _provisioningService = provisioningService;
            _logger = logger;
        }

        public async Task<ServiceDataResult<List<CompanyListItemDto>>> GetCompanies()
        {
            var companies = await _provisioningService.ListCompaniesAsync();

            var companyDtos = companies
                .Select(company => new CompanyListItemDto
                {
                    Id = company.Id,
                    Name = company.Name ?? ""
                })
                .ToList();

            return ServiceDataResult<List<CompanyListItemDto>>.Ok(companyDtos);
        }

        public async Task<ServiceDataResult<CompanyListItemDto>> CreateCompany(CreateCompanyDto model)
        {
            if (string.IsNullOrWhiteSpace(model?.Name))
                return ServiceDataResult<CompanyListItemDto>.BadRequest("Name required");

            var company = await _provisioningService.CreateCompanyAsync(model.Name);

            var companyDto = new CompanyListItemDto
            {
                Id = company.Id,
                Name = company.Name ?? ""
            };

            return ServiceDataResult<CompanyListItemDto>.Ok(companyDto);
        }

        public async Task<ServiceMessageResult> AddEmails(int companyId, List<string>? emails)
        {
            var validEmails = emails?
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Select(email => email.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (validEmails == null || validEmails.Count == 0)
                return ServiceMessageResult.BadRequest("At least one email is required");

            await _provisioningService.AddEmailsToTenantAsync(companyId, validEmails);

            return ServiceMessageResult.Ok("Emails added successfully");
        }
    }
}