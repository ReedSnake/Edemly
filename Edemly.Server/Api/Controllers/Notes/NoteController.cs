using Edemly.Contracts.Notes;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Api.Services;
using Edemly.Server.Data;
using Edemly.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Edemly.Server.Api.Controllers.Notes
{
    [ApiController]
    [Route("api/[controller]")]
    public class NoteController : ApiControllerBase
    {
        private readonly INoteService _service;
        private readonly IPermissionService _permissionService;

        public NoteController(INoteService service, IPermissionService permissionService, ServerDbContext serverDb, ITenantProvider tenantProvider, ITenantDbContextFactory tenantDbFactory, IConfiguration configuration)
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

            if (!await _permissionService.IsNoteAuthor(userId, id))
            {
                return Forbid();
            }

            var result = await _service.GetById(id);
            return OkOrNotFound(result.Success, result.Error, result.Note);
        }

        [Authorize]
        [HttpGet("my-notes")]
        public async Task<IActionResult> GetByCreator()
        {
            var userId = GetCurrentUserIdOrDefault();

            var result = await _service.GetAll(userId);
            return OkOrNotFound(result.Success, result.Error, result.Notes);
        }

        [Authorize]
        [HttpGet("count")]
        public async Task<IActionResult> GetCount()
        {
            var userId = GetCurrentUserIdOrDefault();
            var result = await _service.GetCount(userId);
            return OkOrBadRequest(result.Success, result.Error, new { count = result.Count });
        }

        [Authorize]
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateNoteDto model)
        {
            var userId = GetCurrentUserIdOrDefault();

            var result = await _service.Create(userId, model);
            return OkMessageOrBadRequest(result.Success, result.Error, "Note created");
        }

        [Authorize]
        [HttpPut("update")]
        public async Task<IActionResult> Update([FromBody] UpdateNoteDto model)
        {
            var userId = GetCurrentUserIdOrDefault();

            if (!await _permissionService.IsNoteAuthor(userId, model.Id))
            {
                return Forbid();
            }

            var result = await _service.Update(model);
            return OkMessageOrBadRequest(result.Success, result.Error, "Note updated");
        }

        [Authorize]
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetCurrentUserIdOrDefault();

            if (!await _permissionService.IsNoteAuthor(userId, id))
            {
                return Forbid();
            }

            var result = await _service.Delete(id);
            return OkMessageOrBadRequest(result.Success, result.Error, "Note deleted");
        }
    }
}
