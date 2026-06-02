using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Edemly.Server.Api.DTOs;
using Edemly.Server.Api.Services;
using Edemly.Server.Data;
using Edemly.Server.Services;
using Edemly.Server.Api.Middleware;
using Microsoft.Extensions.Configuration;

namespace Edemly.Server.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RemindingController : ApiControllerBase
    {
        private readonly IRemindingService _service;
        private readonly IPermissionService _permissionService;

        public RemindingController(IRemindingService service, IPermissionService permissionService, ServerDbContext serverDb, ITenantProvider tenantProvider, ITenantDbContextFactory tenantDbFactory, IConfiguration configuration)
            : base(serverDb, tenantProvider, tenantDbFactory, configuration)
        {
            _service = service;
            _permissionService = permissionService;
        }

        [Authorize]
        [HttpGet("id/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            if (!await _permissionService.IsRemindingAuthor(userIdClaim, id))
            {
                return Forbid();
            }

            var result = await _service.GetById(id);
            if (!result.Success) return NotFound(new { message = result.Error });
            return Ok(result.Reminding);
        }

        [Authorize]
        [HttpGet("my-remindings")]
        public async Task<IActionResult> GetByUser()
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            var result = await _service.GetByUser(userIdClaim);
            if (!result.Success) return NotFound(new { message = result.Error });
            return Ok(result.Remindings);
        }

        [Authorize]
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] RemindingDtos.RemindingCreateDto model)
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            var result = await _service.Create(userIdClaim, model);
            if (!result.Success) return BadRequest(new { message = result.Error });
            return Ok(new { message = "Reminding created" });
        }

        [Authorize]
        [HttpPut("update")]
        public async Task<IActionResult> Update([FromBody] RemindingDtos.RemindingUpdateDto model)
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            if (!await _permissionService.IsRemindingAuthor(userIdClaim, model.Id))
            {
                return Forbid();
            }

            var result = await _service.Update(model);
            if (!result.Success) return BadRequest(new { message = result.Error });
            return Ok(new { message = "Reminding updated" });
        }

        [Authorize]
        [HttpPut("toggle-completion/{id}")]
        public async Task<IActionResult> Toggle(int id)
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            if (!await _permissionService.IsRemindingAuthor(userIdClaim, id))
            {
                return Forbid();
            }

            var result = await _service.ToggleCompletion(id);
            if (!result.Success) return BadRequest(new { message = result.Error });
            return Ok(new { message = "Reminding updated" });
        }

        [Authorize]
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            if (!await _permissionService.IsRemindingAuthor(userIdClaim, id))
            {
                return Forbid();
            }

            var result = await _service.Delete(id);
            if (!result.Success) return BadRequest(new { message = result.Error });
            return Ok(new { message = "Reminding deleted" });
        }
    }
}
