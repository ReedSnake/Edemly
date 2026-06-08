using Edemly.Contracts.Notes;
using Edemly.Server.Application.Notes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edemly.Server.Api.Controllers.Notes
{
    [ApiController]
    [Authorize]
    [Route("api/users/{targetUserId}/note")]
    public class ContactNotesController : ApiControllerBase
    {
        private readonly INoteService _noteService;

        public ContactNotesController(INoteService noteService)
        {
            _noteService = noteService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAsync(int targetUserId)
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(
                await _noteService.GetContactNoteAsync(currentUserId, targetUserId),
                note => new { note });
        }

        [HttpPut]
        public async Task<IActionResult> SaveAsync(
            int targetUserId,
            [FromBody] SaveContactNoteDto request)
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(
                await _noteService.SaveContactNoteAsync(currentUserId, targetUserId, request),
                note => new { note });
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteAsync(int targetUserId)
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(
                await _noteService.DeleteContactNoteAsync(currentUserId, targetUserId));
        }
    }
}