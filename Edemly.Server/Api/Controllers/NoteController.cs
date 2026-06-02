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
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            if (!await _permissionService.IsNoteAuthor(userIdClaim, id))
            {
                return Forbid();
            }

            var result = await _service.GetById(id);
            if (!result.Success) return NotFound(new { message = result.Error });
            return Ok(result.Note);
        }

        [Authorize]
        [HttpGet("my-notes")]
        public async Task<IActionResult> GetByCreator()
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            var result = await _service.GetAll(userIdClaim);
            if (!result.Success) return NotFound(new { message = result.Error });
            return Ok(result.Notes);
        }

        [Authorize]
        [HttpGet("count")]
        public async Task<IActionResult> GetCount()
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");
            var result = await _service.GetCount(userIdClaim);
            if (!result.Success) return BadRequest(new { message = result.Error });
            return Ok(new { count = result.Count });
        }

        [Authorize]
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] NoteDtos.NoteCreateDto model)
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            var result = await _service.Create(userIdClaim, model);
            if (!result.Success) return BadRequest(new { message = result.Error });
            return Ok(new { message = "Note created" });
        }

        [Authorize]
        [HttpPut("update")]
        public async Task<IActionResult> Update([FromBody] NoteDtos.NoteUpdateDto model)
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            if (!await _permissionService.IsNoteAuthor(userIdClaim, model.Id))
            {
                return Forbid();
            }

            var result = await _service.Update(model);
            if (!result.Success) return BadRequest(new { message = result.Error });
            return Ok(new { message = "Note updated" });
        }

        [Authorize]
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            if (!await _permissionService.IsNoteAuthor(userIdClaim, id))
            {
                return Forbid();
            }

            var result = await _service.Delete(id);
            if (!result.Success) return BadRequest(new { message = result.Error });
            return Ok(new { message = "Note deleted" });
        }
    }
}
