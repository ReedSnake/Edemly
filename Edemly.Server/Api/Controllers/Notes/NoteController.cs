using Edemly.Contracts.Notes;
using Edemly.Server.Application.Notes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edemly.Server.Api.Controllers.Notes
{
    [ApiController]
    [Authorize]
    [Route("api/notes")]
    public class NotesController : ApiControllerBase
    {
        private readonly INoteService _noteService;

        public NotesController(INoteService noteService)
        {
            _noteService = noteService;
        }

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
    }
}