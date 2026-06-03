using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Edemly.Contracts.Companies;
using Edemly.Server.Api.Services;

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
        public async Task<IActionResult> GetCompanies()
        {
            return ToServiceDataResult(await _companyService.GetCompanies());
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateCompany([FromBody] CreateCompanyDto dto)
        {
            return ToServiceDataResult(await _companyService.CreateCompany(dto));
        }

        [HttpPost("{companyId}/emails")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddEmails(int companyId, [FromBody] List<string>? emails)
        {
            return ToServiceMessageResult(await _companyService.AddEmails(companyId, emails));
        }
    }
}
