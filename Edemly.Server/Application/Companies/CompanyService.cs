using Edemly.Contracts.Companies;
using Edemly.Server.Application.Common;
using Edemly.Server.Application.Common.Mappers;
using Edemly.Server.Infrastructure.Tenancy;

namespace Edemly.Server.Application.Companies
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

        public async Task<ServiceResult<List<CompanyListItemDto>>> GetCompaniesAsync()
        {
            var companies = await _provisioningService.ListCompaniesAsync();

            var companyDtos = companies
                .Select(CompanyMappings.ToListItemDto)
                .ToList();

            return ServiceResult<List<CompanyListItemDto>>.Ok(companyDtos);
        }

        public async Task<ServiceResult<CompanyListItemDto>> CreateAsync(CreateCompanyDto request)
        {
            if (string.IsNullOrWhiteSpace(request?.Name))
                return ServiceResult<CompanyListItemDto>.BadRequest("Name required");

            var company = await _provisioningService.CreateCompanyAsync(request.Name);

            return ServiceResult<CompanyListItemDto>.Ok(CompanyMappings.ToListItemDto(company));
        }

        public async Task<ServiceResult> AddEmailsAsync(int companyId, List<string>? emails)
        {
            var validEmails = emails?
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Select(email => email.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (validEmails == null || validEmails.Count == 0)
                return ServiceResult.BadRequest("At least one email is required");

            await _provisioningService.AddEmailsToTenantAsync(companyId, validEmails);

            return ServiceResult.Ok("Emails added successfully");
        }
    }
}