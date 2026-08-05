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
        private readonly IConfiguration _configuration;
        public GlpiTicketsController(
            GlpiService glpiService,
            GlpiImprovementTicketSynchronizationQueue synchronizationQueue,
            IConfiguration configuration)
        {
            _glpiService = glpiService;
            _synchronizationQueue = synchronizationQueue;
            _configuration = configuration;
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
            [FromQuery] ServiceDeskQueueQueryDto request,
            [FromQuery] bool refresh = false)
        {
            try
            {
                if (refresh)
                {
                    // GLPI plus OpenProject reconciliation can take longer than a browser request.
                    // Queue it and return the local projection immediately (stale while revalidate).
                    await _synchronizationQueue.RequestSynchronizationAsync(
                        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name);
                }

                var tickets = await _glpiService.GetImprovementTicketsAsync(
                    request,
                    User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name);
                tickets.SynchronizationPending = await _synchronizationQueue.IsSynchronizationPendingAsync();
                tickets.SynchronizationIntervalSeconds = Math.Clamp(
                    _configuration.GetValue<int?>("GlpiSynchronization:IntervalSeconds") ?? 300,
                    60,
                    3600);
                return Ok(tickets);
            }
            catch (Exception exception)
            {
                return BadRequest(new { message = exception.Message });
            }
        }

        [HttpGet("kanban")]
        public async Task<IActionResult> GetUnifiedBacklog()
        {
            try
            {
                var backlog = await _glpiService.GetUnifiedBacklogAsync(
                    User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name);
                backlog.SynchronizationPending = await _synchronizationQueue.IsSynchronizationPendingAsync();
                backlog.SynchronizationIntervalSeconds = Math.Clamp(
                    _configuration.GetValue<int?>("GlpiSynchronization:IntervalSeconds") ?? 300,
                    60,
                    3600);
                return Ok(backlog);
            }
            catch (Exception exception)
            {
                return BadRequest(new { message = exception.Message });
            }
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetManagementDashboard()
        {
            try
            {
                // The dashboard reads the local projection; GLPI and OpenProject stay on the background reconciliation path.
                var backlog = await _glpiService.GetUnifiedBacklogAsync(
                    User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name);
                backlog.SynchronizationPending = await _synchronizationQueue.IsSynchronizationPendingAsync();
                backlog.SynchronizationIntervalSeconds = Math.Clamp(
                    _configuration.GetValue<int?>("GlpiSynchronization:IntervalSeconds") ?? 300,
                    60,
                    3600);
                return Ok(backlog);
            }
            catch (Exception exception)
            {
                return BadRequest(new { message = exception.Message });
            }
        }

        [HttpPost("improvements/synchronize")]
        public async Task<IActionResult> SynchronizeImprovementTickets()
        {
            await _synchronizationQueue.RequestSynchronizationAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name);
            return Accepted(new { message = "Atualização da fila solicitada. O quadro será reconciliado em segundo plano." });
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id) => (await _glpiService.GetWorkspaceAsync(id)) is { } workspace ? Ok(workspace) : NotFound();

        [HttpGet("{id:guid}/openproject-work-packages")]
        public async Task<IActionResult> SearchExistingWorkPackages(Guid id, [FromQuery] string? query, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(query)) return BadRequest(new { message = "Informe um termo para pesquisar no OpenProject." });
            try
            {
                return Ok(await _glpiService.SearchExistingWorkPackagesAsync(id, query, cancellationToken));
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (Exception exception)
            {
                return BadRequest(new { message = exception.Message });
            }
        }

        [HttpPost("improvements/bulk-update")]
        public async Task<IActionResult> BulkUpdateImprovementTickets(
            [FromBody] ServiceDeskBulkUpdateRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var updated = await _glpiService.BulkUpdateImprovementTicketsAsync(
                    request,
                    User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name,
                    cancellationToken);
                return Ok(new { updated, message = $"{updated} chamado(s) atualizado(s) na fila do Axiom Atlas." });
            }
            catch (Exception exception)
            {
                return BadRequest(new { message = exception.Message });
            }
        }

        [HttpPost("improvements/prepare-workspaces")]
        public async Task<IActionResult> PrepareImprovementTicketWorkspaces(
            [FromBody] ServiceDeskBulkPrepareRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var workspaces = await _glpiService.PrepareImprovementTicketWorkspacesAsync(
                    request,
                    User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "Sistema",
                    cancellationToken);
                return Ok(new { count = workspaces.Count, workspaces, message = $"{workspaces.Count} área(s) de trabalho preparada(s) para criar ou vincular Work Packages." });
            }
            catch (Exception exception)
            {
                return BadRequest(new { message = exception.Message });
            }
        }

        [HttpPost("{id:guid}/openproject-work-packages/{workPackageId:int}/glpi-link")]
        public async Task<IActionResult> LinkExistingWorkPackage(Guid id, int workPackageId, CancellationToken cancellationToken)
        {
            try { return Ok(await _glpiService.LinkExistingWorkPackageAsync(id, workPackageId, cancellationToken)); }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (Exception exception) { return BadRequest(new { message = exception.Message }); }
        }

        [HttpPost("{id:guid}/openproject-work-packages/{workPackageId:int}/private-comment")]
        public async Task<IActionResult> AddExistingWorkPackagePrivateComment(Guid id, int workPackageId, CancellationToken cancellationToken)
        {
            try { return Ok(await _glpiService.AddExistingWorkPackagePrivateCommentAsync(id, workPackageId, cancellationToken)); }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (Exception exception) { return BadRequest(new { message = exception.Message }); }
        }

        [HttpPut("{id:guid}/draft")]
        public async Task<IActionResult> SaveDraft(Guid id, [FromBody] SaveRequirementDraftRequest request)
        {
            try { return Ok(await _glpiService.SaveDraftAsync(id, request.RequirementMarkdown)); }
            catch (KeyNotFoundException) { return NotFound(); }
        }

        [HttpPost("{id:guid}/images")]
        [RequestSizeLimit(8 * 1024 * 1024)]
        public async Task<IActionResult> UploadWorkspaceImage(Guid id, IFormFile? image)
        {
            if (image == null || image.Length == 0)
                return BadRequest(new { message = "Selecione uma imagem para enviar." });
            if (image.Length > 8 * 1024 * 1024)
                return BadRequest(new { message = "A imagem deve ter no máximo 8 MB." });

            try
            {
                await using var stream = image.OpenReadStream();
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer);
                return Ok(await _glpiService.UploadWorkspaceImageAsync(id, image.FileName, buffer.ToArray()));
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (Exception exception)
            {
                return BadRequest(new { message = exception.Message });
            }
        }

        // OpenProject renders Markdown images directly. The opaque GUID keeps the public URL unguessable,
        // while the upload endpoint itself remains authenticated.
        [AllowAnonymous]
        [HttpGet("workspace-images/{imageId:guid}")]
        public async Task<IActionResult> GetWorkspaceImage(Guid imageId)
        {
            var image = await _glpiService.GetWorkspaceImageAsync(imageId);
            return image == null ? NotFound() : File(image.Value.Content, image.Value.ContentType);
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

        [HttpPost("{id:guid}/glpi-link/reprocess")]
        public async Task<IActionResult> ReprocessGlpiLink(Guid id)
        {
            var workspace = await _glpiService.GetWorkspaceAsync(id);
            if (workspace == null) return NotFound();
            if (!workspace.OpenProjectWorkPackageId.HasValue)
            {
                return BadRequest(new { message = "Crie ou vincule uma User Story antes de sincronizar o GLPI." });
            }

            var job = await _synchronizationQueue.RequestGlpiLinkUpdateAsync(
                id,
                workspace.GlpiTicketId,
                workspace.OpenProjectWorkPackageId,
                User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name);
            return Accepted(new
            {
                job.Id,
                status = job.Status.ToString(),
                message = "O vínculo com o GLPI foi colocado na fila de sincronização."
            });
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
