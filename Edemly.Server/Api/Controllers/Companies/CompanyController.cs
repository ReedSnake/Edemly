using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Edemly.Server.Api.Services;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Microsoft.Extensions.Configuration;
using Edemly.Contracts.Companies;
using Microsoft.EntityFrameworkCore;

namespace Edemly.Server.Api.Controllers.Companies
{
    [ApiController]
    [Route("api/admin/companies")]
    public class CompanyController : ApiControllerBase
    {
        private readonly TenantProvisioningService _provisioningService;
        private readonly ServerDbContext _serverDb;
        private readonly IConfiguration _configuration;

        public CompanyController(TenantProvisioningService provisioningService, ServerDbContext serverDb, IConfiguration configuration)
        {
            _provisioningService = provisioningService;
            _serverDb = serverDb;
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> GetCompanies()
        {
            var list = await _provisioningService.ListCompaniesAsync();

            // Return only minimal, non-sensitive data (Id and Name) to the client
            var dto = list.Select(c => new { Id = c.Id, Name = string.IsNullOrEmpty(c.Name) ? "" : c.Name }).ToList();
            return Ok(dto);
        }

        // Only admin should be able to call - assume policy "Admin" exists
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateCompany([FromBody] CreateCompanyDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.Name)) return BadRequestMessage("Name required");

            var company = await _provisioningService.CreateCompanyAsync(dto.Name);
            return Ok(new { Id = company.Id, Name = company.Name });
        }

        [HttpPost("{companyId}/emails")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddEmails(int companyId, [FromBody] List<string>? emails)
        {
            var validEmails = emails?
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Select(email => email.Trim())
                .ToList();

            if (validEmails == null || validEmails.Count == 0) return BadRequestMessage("At least one email is required");

            await _provisioningService.AddEmailsToTenantAsync(companyId, validEmails);
            return Ok();
        }

        [HttpPut("admin/update")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateAdmin([FromBody] UpdateAdminDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.Email)) return BadRequestMessage("Email required");

            // find existing admin logininfo by configured AdminEmail or default
            var adminEmail = _configuration.GetValue<string>("AdminEmail") ?? "admin@edemly.com";

            var existing = await _serverDb.LoginInfos.FirstOrDefaultAsync(l => l.Email == adminEmail);

            // create new logininfo and user
            var loginInfo = new LoginInfo { Email = dto.Email, IsEmailVerified = true };
            _serverDb.LoginInfos.Add(loginInfo);
            await _serverDb.SaveChangesAsync();

            var user = new User
            {
                Username = string.IsNullOrWhiteSpace(dto.Username) ? dto.Email.Split('@')[0] : dto.Username,
                LoginInfoId = loginInfo.Id,
                CreatedAt = DateTime.UtcNow,
                LastOnline = DateTime.UtcNow,
                SubscriptionStatus = SubscriptionStatus.Vip
            };

            _serverDb.Users.Add(user);
            await _serverDb.SaveChangesAsync();

            // delete old admin if exists
            if (existing != null)
            {
                var oldUser = await _serverDb.Users.FirstOrDefaultAsync(u => u.LoginInfoId == existing.Id);
                if (oldUser != null)
                {
                    _serverDb.Users.Remove(oldUser);
                }

                _serverDb.LoginInfos.Remove(existing);
                await _serverDb.SaveChangesAsync();
            }

            // update configuration AdminEmail
            // Note: cannot edit appsettings.json at runtime reliably; instruct user to update config manually or set environment variable

            return OkMessage("Admin updated. Please update AdminEmail in appsettings.json or environment variables.");
        }
    }
}
