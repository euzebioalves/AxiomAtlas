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
        private readonly GlpiImprovementTicketSynchronizationQueue _synchronizationQueue;
        public GlpiTicketsController(GlpiService glpiService, GlpiImprovementTicketSynchronizationQueue synchronizationQueue)
        {
            _glpiService = glpiService;
            _synchronizationQueue = synchronizationQueue;
        }

        [HttpPost("import")]
        public async Task<IActionResult> Import([FromBody] ImportGlpiTicketRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Query)) return BadRequest(new { message = "Informe o número ou assunto do chamado." });
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "Sistema";
            try { return Ok(await _glpiService.ImportTicketAsync(request.Query, userId)); }
            catch (Exception exception) { return BadRequest(new { message = exception.Message }); }
        }

        [HttpGet("improvements")]
        public async Task<IActionResult> GetImprovementTickets(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? status = null,
            [FromQuery] bool refresh = false)
        {
            try
            {
                if (refresh)
                {
                    // A manual refresh is explicit: wait for GLPI and OpenProject reconciliation
                    // before reading the local queue again.
                    await _glpiService.SynchronizeImprovementTicketsAsync(HttpContext.RequestAborted);
                }
                else
                {
                    _synchronizationQueue.RequestSynchronization();
                }

                var tickets = await _glpiService.GetImprovementTicketsAsync(page, pageSize, status);
                tickets.SynchronizationPending = _synchronizationQueue.IsSynchronizationPending;
                return Ok(tickets);
            }
            catch (Exception exception)
            {
                return BadRequest(new { message = exception.Message });
            }
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id) => (await _glpiService.GetWorkspaceAsync(id)) is { } workspace ? Ok(workspace) : NotFound();

        [HttpPut("{id:guid}/draft")]
        public async Task<IActionResult> SaveDraft(Guid id, [FromBody] SaveRequirementDraftRequest request)
        {
            try { return Ok(await _glpiService.SaveDraftAsync(id, request.RequirementMarkdown)); }
            catch (KeyNotFoundException) { return NotFound(); }
        }

        [HttpGet("openproject-projects")]
        public async Task<IActionResult> GetOpenProjectProjects()
        {
            try { return Ok(await _glpiService.GetOpenProjectProjectsAsync()); }
            catch (Exception exception) { return BadRequest(new { message = exception.Message }); }
        }

        [HttpPost("{id:guid}/user-story")]
        public async Task<IActionResult> CreateUserStory(Guid id, [FromBody] CreateOpenProjectUserStoryRequest request)
        {
            try { return Ok(await _glpiService.CreateUserStoryAsync(id, request)); }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (Exception exception) { return BadRequest(new { message = exception.Message }); }
        }

        [HttpGet("{id:guid}/attachments/{documentId:int}")]
        public async Task<IActionResult> DownloadAttachment(Guid id, int documentId)
        {
            try { var file = await _glpiService.DownloadAttachmentAsync(id, documentId); return File(file.Content, file.ContentType); }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (Exception exception) { return BadRequest(new { message = exception.Message }); }
        }
    }
}
