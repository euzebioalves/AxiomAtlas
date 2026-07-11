using System.Security.Claims;
using Axiom.Atlas.Application.DTOs.ServiceDesk;
using Axiom.Atlas.Infrastructure.Services.ServiceDesk;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Axiom.Atlas.API.Controllers.ServiceDesk
{
    [ApiController]
    [Route("api/glpi/tickets")]
    [Authorize]
    public class GlpiTicketsController : ControllerBase
    {
        private readonly GlpiService _glpiService;
        public GlpiTicketsController(GlpiService glpiService) => _glpiService = glpiService;

        [HttpPost("import")]
        public async Task<IActionResult> Import([FromBody] ImportGlpiTicketRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Query)) return BadRequest(new { message = "Informe o número ou assunto do chamado." });
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "Sistema";
            try { return Ok(await _glpiService.ImportTicketAsync(request.Query, userId)); }
            catch (Exception exception) { return BadRequest(new { message = exception.Message }); }
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id) => (await _glpiService.GetWorkspaceAsync(id)) is { } workspace ? Ok(workspace) : NotFound();

        [HttpPut("{id:guid}/draft")]
        public async Task<IActionResult> SaveDraft(Guid id, [FromBody] SaveRequirementDraftRequest request)
        {
            try { return Ok(await _glpiService.SaveDraftAsync(id, request.RequirementMarkdown)); }
            catch (KeyNotFoundException) { return NotFound(); }
        }
    }
}
