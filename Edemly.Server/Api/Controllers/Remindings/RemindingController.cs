using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Edemly.Server.Api.Services;
using Edemly.Server.Data;
using Edemly.Server.Services;
using Edemly.Server.Api.Middleware;
using Edemly.Contracts.Remindings;
using Microsoft.Extensions.Configuration;

namespace Edemly.Server.Api.Controllers.Remindings
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
            var userId = GetCurrentUserIdOrDefault();

            if (!await _permissionService.IsRemindingAuthor(userId, id))
            {
                return Forbid();
            }

            var result = await _service.GetById(id);
            return OkOrNotFound(result.Success, result.Error, result.Reminding);
        }

        [Authorize]
        [HttpGet("my-remindings")]
        public async Task<IActionResult> GetByUser()
        {
            var userId = GetCurrentUserIdOrDefault();

            var result = await _service.GetByUser(userId);
            return OkOrNotFound(result.Success, result.Error, result.Remindings);
        }

        [Authorize]
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateRemindingDto model)
        {
            var userId = GetCurrentUserIdOrDefault();

            var result = await _service.Create(userId, model);
            return OkMessageOrBadRequest(result.Success, result.Error, "Reminding created");
        }

        [Authorize]
        [HttpPut("update")]
        public async Task<IActionResult> Update([FromBody] UpdateRemindingDto model)
        {
            var userId = GetCurrentUserIdOrDefault();

            if (!await _permissionService.IsRemindingAuthor(userId, model.Id))
            {
                return Forbid();
            }

            var result = await _service.Update(model);
            return OkMessageOrBadRequest(result.Success, result.Error, "Reminding updated");
        }

        [Authorize]
        [HttpPut("toggle-completion/{id}")]
        public async Task<IActionResult> Toggle(int id)
        {
            var userId = GetCurrentUserIdOrDefault();

            if (!await _permissionService.IsRemindingAuthor(userId, id))
            {
                return Forbid();
            }

            var result = await _service.ToggleCompletion(id);
            return OkMessageOrBadRequest(result.Success, result.Error, "Reminding updated");
        }

        [Authorize]
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetCurrentUserIdOrDefault();

            if (!await _permissionService.IsRemindingAuthor(userId, id))
            {
                return Forbid();
            }

            var result = await _service.Delete(id);
            return OkMessageOrBadRequest(result.Success, result.Error, "Reminding deleted");
        }
    }
}
