using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Edemly.Server.Api.Services;
using Edemly.Contracts.Remindings;

namespace Edemly.Server.Api.Controllers.Remindings
{
    [ApiController]
    [Route("api/[controller]")]
    public class RemindingController : ApiControllerBase
    {
        private readonly IRemindingService _service;

        public RemindingController(IRemindingService service)
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
        [HttpGet("my-remindings")]
        public async Task<IActionResult> GetByUser()
        {
            return ToServiceDataResult(await _service.GetByUser(GetCurrentUserIdOrDefault()));
        }

        [Authorize]
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateRemindingDto model)
        {
            return ToServiceMessageResult(await _service.Create(GetCurrentUserIdOrDefault(), model));
        }

        [Authorize]
        [HttpPut("update")]
        public async Task<IActionResult> Update([FromBody] UpdateRemindingDto model)
        {
            return ToServiceMessageResult(await _service.Update(GetCurrentUserIdOrDefault(), model));
        }

        [Authorize]
        [HttpPut("toggle-completion/{id}")]
        public async Task<IActionResult> Toggle(int id)
        {
            return ToServiceMessageResult(await _service.ToggleCompletion(GetCurrentUserIdOrDefault(), id));
        }

        [Authorize]
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            return ToServiceMessageResult(await _service.Delete(GetCurrentUserIdOrDefault(), id));
        }
    }
}
