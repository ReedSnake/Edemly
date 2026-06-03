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
        private readonly INoteService _service;

        public NoteController(INoteService service)
        {
            _service = service;
        }

        [Authorize]
        [HttpGet("id/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            return ToServiceDataResult(await _service.GetById(GetCurrentUserIdOrDefault(), id));
        }

        [Authorize]
        [HttpGet("my-notes")]
        public async Task<IActionResult> GetByCreator()
        {
            return ToServiceDataResult(await _service.GetAll(GetCurrentUserIdOrDefault()));
        }

        [Authorize]
        [HttpGet("count")]
        public async Task<IActionResult> GetCount()
        {
            return ToServiceDataResult(
                await _service.GetCount(GetCurrentUserIdOrDefault()),
                count => new { count });
        }

        [Authorize]
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateNoteDto model)
        {
            return ToServiceMessageResult(await _service.Create(GetCurrentUserIdOrDefault(), model));
        }

        [Authorize]
        [HttpPut("update")]
        public async Task<IActionResult> Update([FromBody] UpdateNoteDto model)
        {
            return ToServiceMessageResult(await _service.Update(GetCurrentUserIdOrDefault(), model));
        }

        [Authorize]
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            return ToServiceMessageResult(await _service.Delete(GetCurrentUserIdOrDefault(), id));
        }
    }
}
