using Edemly.Contracts.Companies;
using Edemly.Server.Application.Companies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edemly.Server.Api.Controllers.Companies
{
    [ApiController]
    [Route("api/admin/companies")]
    public class CompanyController : ApiControllerBase
    {
        private readonly ICompanyService _companyService;

        public CompanyController(ICompanyService companyService)
        {
            _companyService = companyService;
        }

        [HttpGet]
        public async Task<IActionResult> GetCompaniesAsync()
        {
            return ToServiceResult(await _companyService.GetCompaniesAsync());
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateAsync([FromBody] CreateCompanyDto request)
        {
            return ToServiceResult(await _companyService.CreateAsync(request));
        }

        [HttpPost("{companyId}/emails")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddEmailsAsync(int companyId, [FromBody] List<string>? emails)
        {
            return ToServiceResult(await _companyService.AddEmailsAsync(companyId, emails));
        }
    }
}