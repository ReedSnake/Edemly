using Edemly.Contracts.Notes;
using Edemly.Server.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edemly.Server.Api.Controllers.Notes
{
    [ApiController]
    [Route("api/[controller]")]
    public class NoteController : ApiControllerBase
    {
        private readonly INoteService _noteService;

        public NoteController(INoteService noteService)
        {
            _noteService = noteService;
        }

        [Authorize]
        [HttpGet("id/{noteId}")]
        public async Task<IActionResult> GetByIdAsync(int noteId)
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _noteService.GetByIdAsync(currentUserId, noteId));
        }

        [Authorize]
        [HttpGet("my-notes")]
        public async Task<IActionResult> GetByCreatorAsync()
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _noteService.GetAllAsync(currentUserId));
        }

        [Authorize]
        [HttpGet("count")]
        public async Task<IActionResult> GetCountAsync()
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(
                await _noteService.GetCountAsync(currentUserId),
                count => new { count });
        }

        [Authorize]
        [HttpPost("create")]
        public async Task<IActionResult> CreateAsync([FromBody] CreateNoteDto request)
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _noteService.CreateAsync(currentUserId, request));
        }

        [Authorize]
        [HttpPut("update")]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateNoteDto request)
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _noteService.UpdateAsync(currentUserId, request));
        }

        [Authorize]
        [HttpDelete("delete/{noteId}")]
        public async Task<IActionResult> DeleteAsync(int noteId)
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _noteService.DeleteAsync(currentUserId, noteId));
        }
    }
}