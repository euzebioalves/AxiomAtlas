using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Axiom.Atlas.Application.DTOs.ServiceDesk;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Axiom.Atlas.Web.Controllers.ServiceDesk
{
    public class ServiceDeskController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        public ServiceDeskController(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Kanban()
        {
            return View();
        }

        public IActionResult Dashboard()
        {
            return View();
        }

        public IActionResult ClientPortfolio()
        {
            return View();
        }

        public IActionResult FlowCapacity()
        {
            return View();
        }

        public IActionResult SlaAging()
        {
            return View();
        }

        public IActionResult DeliveryPredictability()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> KanbanData()
        {
            try
            {
                var response = await CreateClient().GetAsync("api/glpi/tickets/kanban");
                var content = await response.Content.ReadAsStringAsync();
                return new ContentResult
                {
                    Content = content,
                    ContentType = "application/json",
                    StatusCode = (int)response.StatusCode
                };
            }
            catch (Exception exception)
            {
                return StatusCode(503, new
                {
                    message = "Não foi possível carregar o quadro unificado de melhorias.",
                    detail = exception.Message
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> DashboardData()
        {
            try
            {
                var response = await CreateClient().GetAsync("api/glpi/tickets/dashboard");
                var content = await response.Content.ReadAsStringAsync();
                return new ContentResult
                {
                    Content = content,
                    ContentType = "application/json",
                    StatusCode = (int)response.StatusCode
                };
            }
            catch (Exception exception)
            {
                return StatusCode(503, new
                {
                    message = "Não foi possível carregar a visão gerencial da fila.",
                    detail = exception.Message
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> DashboardDetails(string? client = null, string? stage = null)
        {
            try
            {
                var dashboard = await GetDashboardBacklogAsync();
                IEnumerable<UnifiedBacklogItemDto> items = dashboard.Items;
                string title;
                string description;

                if (!string.IsNullOrWhiteSpace(client))
                {
                    items = items.Where(item => item.Stage != "completed" &&
                        string.Equals(item.ClientEntityName ?? "Cliente não informado", client, StringComparison.OrdinalIgnoreCase));
                    title = $"Demandas de {client}";
                    description = "Chamados abertos do cliente selecionado, ordenados pela prioridade recomendada.";
                }
                else if (!string.IsNullOrWhiteSpace(stage))
                {
                    items = items.Where(item => string.Equals(item.Stage, stage, StringComparison.OrdinalIgnoreCase));
                    var stageLabel = dashboard.Items.FirstOrDefault(item =>
                        string.Equals(item.Stage, stage, StringComparison.OrdinalIgnoreCase))?.StageLabel ?? stage;
                    title = $"Fluxo: {stageLabel}";
                    description = "Demandas atualmente classificadas nesta etapa do fluxo.";
                }
                else
                {
                    title = "Detalhamento da fila";
                    description = "Demandas da projeção local do Service Desk.";
                }

                ViewData["DashboardDetailsTitle"] = title;
                ViewData["DashboardDetailsDescription"] = description;
                return View(items.OrderBy(item => item.Stage == "completed")
                    .ThenBy(item => GetPriorityOrder(item.Priority))
                    .ThenByDescending(item => item.DaysOpen).ToList());
            }
            catch (Exception exception)
            {
                return StatusCode(503, new { message = "Não foi possível carregar o detalhamento gerencial.", detail = exception.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> DashboardReport()
        {
            try
            {
                ViewData["GeneratedAt"] = DateTime.Now;
                return View(await GetDashboardBacklogAsync());
            }
            catch (Exception exception)
            {
                return StatusCode(503, new { message = "Não foi possível gerar o relatório gerencial.", detail = exception.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> DashboardExcel()
        {
            try
            {
                var dashboard = await GetDashboardBacklogAsync();
                using var workbook = new XLWorkbook();
                var summary = workbook.Worksheets.Add("Resumo");
                var details = workbook.Worksheets.Add("Demandas");

                summary.Cell("A1").Value = "Axiom Atlas - Visão gerencial de melhorias";
                summary.Range("A1:B1").Merge();
                summary.Range("A1:B1").Style.Font.Bold = true;
                summary.Range("A1:B1").Style.Font.FontSize = 16;
                summary.Range("A1:B1").Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
                summary.Range("A1:B1").Style.Font.FontColor = XLColor.White;
                var summaryRows = new[]
                {
                    ("Gerado em", DateTime.Now.ToString("dd/MM/yyyy HH:mm")),
                    ("Chamados em aberto", dashboard.Summary.Total.ToString()),
                    ("Chamados em risco", dashboard.Summary.AtRisk.ToString()),
                    ("Demandas críticas", dashboard.Summary.Critical.ToString()),
                    ("Maior espera", $"{dashboard.Summary.OldestOpenDays} dias"),
                    ("Atenção necessária", dashboard.Summary.Attention.ToString()),
                    ("Triagem GLPI", dashboard.Summary.Triage.ToString()),
                    ("Análise de requisitos", dashboard.Summary.Analysis.ToString()),
                    ("User Story em andamento", dashboard.Summary.Delivery.ToString()),
                    ("Concluídas", dashboard.Summary.Completed.ToString())
                };
                for (var index = 0; index < summaryRows.Length; index++)
                {
                    summary.Cell(index + 3, 1).Value = summaryRows[index].Item1;
                    summary.Cell(index + 3, 2).Value = summaryRows[index].Item2;
                }
                summary.Range(3, 1, summaryRows.Length + 2, 1).Style.Font.Bold = true;
                summary.Columns().AdjustToContents();

                var headers = new[] { "Chamado GLPI", "Assunto", "URL GLPI", "Cliente", "Data abertura", "Dias em aberto", "Status GLPI", "Etapa", "Prioridade", "Motivo da prioridade", "Em risco", "Vínculo GLPI pendente", "Work Package", "URL Work Package", "Status WP", "Criador WP", "Dias da WP" };
                for (var column = 1; column <= headers.Length; column++) details.Cell(1, column).Value = headers[column - 1];
                var headerRange = details.Range(1, 1, 1, headers.Length);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
                headerRange.Style.Font.FontColor = XLColor.White;

                var row = 2;
                foreach (var item in dashboard.Items.OrderBy(item => item.Stage == "completed").ThenByDescending(item => item.DaysOpen))
                {
                    details.Cell(row, 1).Value = item.GlpiTicketId;
                    details.Cell(row, 2).Value = item.Subject;
                    details.Cell(row, 3).Value = item.GlpiTicketUrl;
                    details.Cell(row, 4).Value = item.ClientEntityName;
                    details.Cell(row, 5).Value = item.OpenedAt?.ToString("dd/MM/yyyy");
                    details.Cell(row, 6).Value = item.DaysOpen;
                    details.Cell(row, 7).Value = item.GlpiStatusName;
                    details.Cell(row, 8).Value = item.StageLabel;
                    details.Cell(row, 9).Value = item.Priority;
                    details.Cell(row, 10).Value = item.PriorityReason;
                    details.Cell(row, 11).Value = item.IsAtRisk ? "Sim" : "Não";
                    details.Cell(row, 12).Value = item.IsGlpiLinkPending ? "Sim" : "Não";
                    details.Cell(row, 13).Value = item.WorkPackageId;
                    details.Cell(row, 14).Value = item.WorkPackageUrl;
                    details.Cell(row, 15).Value = item.WorkPackageStatus;
                    details.Cell(row, 16).Value = item.WorkPackageCreator;
                    details.Cell(row, 17).Value = item.WorkPackageDaysOpen;
                    row++;
                }
                details.SheetView.FreezeRows(1);
                details.Range(1, 1, Math.Max(row - 1, 1), headers.Length).SetAutoFilter();
                details.Columns().AdjustToContents();
                details.Column(2).Width = 45;
                details.Column(3).Width = 45;
                details.Column(10).Width = 50;
                details.Column(14).Width = 45;

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"relatorio-melhorias-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
            }
            catch (Exception exception)
            {
                return StatusCode(503, new { message = "Não foi possível exportar a planilha gerencial.", detail = exception.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RefreshKanban()
        {
            var response = await CreateClient().PostAsync("api/glpi/tickets/improvements/synchronize", null);
            return new ContentResult
            {
                Content = await response.Content.ReadAsStringAsync(),
                ContentType = "application/json",
                StatusCode = (int)response.StatusCode
            };
        }

        [HttpGet]
        public async Task<IActionResult> List(
            int page = 1,
            int pageSize = 25,
            string? status = null,
            string? search = null,
            string? client = null,
            string? stage = null,
            string? priority = null,
            string? workPackage = null,
            bool onlyRisk = false,
            bool onlyMine = false,
            string? sort = null,
            bool refresh = false)
        {
            try
            {
                var request = new ServiceDeskQueueQueryDto
                {
                    Page = page,
                    PageSize = pageSize,
                    Status = status,
                    Search = search,
                    Client = client,
                    Stage = stage,
                    Priority = priority,
                    WorkPackage = workPackage,
                    OnlyRisk = onlyRisk,
                    OnlyMine = onlyMine,
                    Sort = sort
                };
                var response = await CreateClient().GetAsync($"api/glpi/tickets/improvements?{BuildQueueQuery(request)}&refresh={refresh.ToString().ToLowerInvariant()}");
                if (response.IsSuccessStatusCode)
                {
                    return PartialView("_ImprovementTicketsTable", await response.Content.ReadFromJsonAsync<GlpiImprovementTicketsResponse>() ?? new GlpiImprovementTicketsResponse());
                }

                var detail = await ReadErrorDetailAsync(response);
                return StatusCode((int)response.StatusCode, new
                {
                    message = "Não foi possível carregar as solicitações de melhoria do GLPI.",
                    detail
                });
            }
            catch (Exception exception)
            {
                return StatusCode(503, new
                {
                    message = "Não foi possível comunicar com o serviço de integração do GLPI.",
                    detail = exception.Message
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> BulkUpdate([FromBody] ServiceDeskBulkUpdateRequest request)
        {
            var response = await CreateClient().PostAsJsonAsync("api/glpi/tickets/improvements/bulk-update", request);
            return new ContentResult
            {
                Content = await response.Content.ReadAsStringAsync(),
                ContentType = "application/json",
                StatusCode = (int)response.StatusCode
            };
        }

        [HttpPost]
        public async Task<IActionResult> PrepareWorkspaces([FromBody] ServiceDeskBulkPrepareRequest request)
        {
            var response = await CreateClient().PostAsJsonAsync("api/glpi/tickets/improvements/prepare-workspaces", request);
            return new ContentResult
            {
                Content = await response.Content.ReadAsStringAsync(),
                ContentType = "application/json",
                StatusCode = (int)response.StatusCode
            };
        }

        [HttpGet]
        public async Task<IActionResult> ExportCsv([FromQuery] ServiceDeskQueueQueryDto request)
        {
            var report = await GetAllQueueItemsAsync(request);
            var builder = new StringBuilder();
            builder.AppendLine("Chamado GLPI;Assunto;Cliente;Data de abertura;Dias em aberto;Status GLPI;Etapa;Prioridade;Responsável;Classificação;Work Package;Status WP;Criador WP;Em risco;Vínculo pendente");
            foreach (var item in report.Items)
            {
                builder.AppendLine(string.Join(';', new[]
                {
                    Csv(item.GlpiTicketId), Csv(item.Subject), Csv(item.ClientEntityName), Csv(item.OpenedAt?.ToLocalTime().ToString("dd/MM/yyyy")), Csv(item.DaysOpen),
                    Csv(item.GlpiStatusName), Csv(item.StageLabel), Csv(item.Priority), Csv(item.AssignedUserName), Csv(item.Classification), Csv(item.WorkPackageId),
                    Csv(item.WorkPackageStatus), Csv(item.WorkPackageCreator), Csv(item.IsAtRisk ? "Sim" : "Não"), Csv(item.IsGlpiLinkPending ? "Sim" : "Não")
                }));
            }
            return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray(), "text/csv; charset=utf-8", $"fila-servicedesk-{DateTime.Now:yyyyMMdd-HHmm}.csv");
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel([FromQuery] ServiceDeskQueueQueryDto request)
        {
            var report = await GetAllQueueItemsAsync(request);
            using var workbook = new XLWorkbook();
            var summary = workbook.Worksheets.Add("Resumo");
            summary.Cell("A1").Value = "Axiom Atlas - Fila operacional do Service Desk";
            summary.Range("A1:B1").Merge();
            summary.Range("A1:B1").Style.Font.Bold = true;
            summary.Range("A1:B1").Style.Font.FontSize = 16;
            summary.Range("A1:B1").Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            summary.Range("A1:B1").Style.Font.FontColor = XLColor.White;
            var summaryRows = new[]
            {
                ("Gerado em", DateTime.Now.ToString("dd/MM/yyyy HH:mm")),
                ("Demandas filtradas", report.TotalCount.ToString()),
                ("Sem Work Package", report.Summary.WithoutWorkPackage.ToString()),
                ("Em risco", report.Summary.AtRisk.ToString()),
                ("Vínculos GLPI pendentes", report.Summary.PendingLinks.ToString()),
                ("Em desenvolvimento", report.Summary.InProgress.ToString()),
                ("Minhas atribuições", report.Summary.MyAssignments.ToString())
            };
            for (var index = 0; index < summaryRows.Length; index++)
            {
                summary.Cell(index + 3, 1).Value = summaryRows[index].Item1;
                summary.Cell(index + 3, 2).Value = summaryRows[index].Item2;
            }
            summary.Range(3, 1, summaryRows.Length + 2, 1).Style.Font.Bold = true;
            summary.Columns().AdjustToContents();

            var details = workbook.Worksheets.Add("Demandas");
            var headers = new[] { "Chamado GLPI", "Assunto", "Cliente", "Data de abertura", "Dias em aberto", "Status GLPI", "Etapa", "Prioridade", "Responsável", "Classificação", "Work Package", "Status WP", "Criador WP", "Em risco", "Vínculo pendente" };
            for (var column = 1; column <= headers.Length; column++) details.Cell(1, column).Value = headers[column - 1];
            var headerRange = details.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            headerRange.Style.Font.FontColor = XLColor.White;
            var row = 2;
            foreach (var item in report.Items)
            {
                var values = new object?[] { item.GlpiTicketId, item.Subject, item.ClientEntityName, item.OpenedAt?.ToLocalTime(), item.DaysOpen, item.GlpiStatusName, item.StageLabel, item.Priority, item.AssignedUserName, item.Classification, item.WorkPackageId, item.WorkPackageStatus, item.WorkPackageCreator, item.IsAtRisk ? "Sim" : "Não", item.IsGlpiLinkPending ? "Sim" : "Não" };
                for (var column = 1; column <= values.Length; column++) details.Cell(row, column).Value = XLCellValue.FromObject(values[column - 1]);
                details.Cell(row, 4).Style.DateFormat.Format = "dd/MM/yyyy";
                row++;
            }
            details.SheetView.FreezeRows(1);
            details.Range(1, 1, Math.Max(row - 1, 1), headers.Length).SetAutoFilter();
            details.Columns().AdjustToContents();
            details.Column(2).Width = 52;
            details.Column(10).Width = 26;
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"fila-servicedesk-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
        }

        [HttpGet]
        public async Task<IActionResult> ExportPdf([FromQuery] ServiceDeskQueueQueryDto request)
        {
            var report = await GetAllQueueItemsAsync(request);
            QuestPDF.Settings.License = LicenseType.Community;
            var bytes = Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(18);
                    page.DefaultTextStyle(style => style.FontSize(8));
                    page.Header().Column(column =>
                    {
                        column.Item().Text("Axiom Atlas - Fila operacional do Service Desk").FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
                        column.Item().Text($"Gerado em {DateTime.Now:dd/MM/yyyy HH:mm} | {report.TotalCount} demanda(s) filtrada(s)").FontColor(Colors.Grey.Darken1);
                    });
                    page.Content().PaddingVertical(12).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(62);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1.4f);
                            columns.RelativeColumn(1.1f);
                            columns.RelativeColumn(1.15f);
                            columns.RelativeColumn(1.15f);
                            columns.RelativeColumn(1.1f);
                            columns.RelativeColumn(1.1f);
                        });
                        table.Header(header =>
                        {
                            foreach (var title in new[] { "GLPI", "Assunto", "Cliente", "Etapa", "Prioridade", "Responsável", "WP", "Idade" })
                                header.Cell().Background(Colors.Blue.Darken2).Padding(4).Text(title).FontColor(Colors.White).Bold();
                        });
                        foreach (var item in report.Items)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text($"#{item.GlpiTicketId}");
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(item.Subject);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(item.ClientEntityName ?? "-");
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(item.StageLabel);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(item.Priority);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(item.AssignedUserName ?? "Não atribuído");
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(item.WorkPackageId.HasValue ? $"#{item.WorkPackageId}" : "Sem WP");
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text($"{item.DaysOpen} dia(s)");
                        }
                    });
                    page.Footer().AlignCenter().Text(text => { text.Span("Axiom Atlas | Página "); text.CurrentPageNumber(); text.Span(" de "); text.TotalPages(); });
                });
            }).GeneratePdf();
            return File(bytes, "application/pdf", $"fila-servicedesk-{DateTime.Now:yyyyMMdd-HHmm}.pdf");
        }

        [HttpPost]
        public async Task<IActionResult> Import([FromBody] ImportGlpiTicketRequest request)
        {
            var client = CreateClient();
            var response = await client.PostAsJsonAsync("api/glpi/tickets/import", request);
            return new ContentResult { Content = await response.Content.ReadAsStringAsync(), ContentType = "application/json", StatusCode = (int)response.StatusCode };
        }

        [HttpGet]
        public async Task<IActionResult> Workspace(Guid id, int returnPage = 1, int returnPageSize = 25, string? returnStatus = null, string? returnSource = null)
        {
            var response = await CreateClient().GetAsync($"api/glpi/tickets/{id}");
            if (!response.IsSuccessStatusCode) return RedirectToAction(nameof(Index));

            var workspace = await response.Content.ReadFromJsonAsync<GlpiTicketWorkspaceDto>();
            if (workspace is null) return RedirectToAction(nameof(Index));

            var pageSize = new[] { 10, 25, 50, 100 }.Contains(returnPageSize) ? returnPageSize : 25;
            ViewData["ReturnUrl"] = string.Equals(returnSource, "kanban", StringComparison.OrdinalIgnoreCase)
                ? Url.Action(nameof(Kanban), new { highlight = workspace.GlpiTicketId })
                : string.Equals(returnSource, "dashboard", StringComparison.OrdinalIgnoreCase)
                    ? Url.Action(nameof(Dashboard), new { highlight = workspace.GlpiTicketId })
                    : Url.Action(nameof(Index), new
                {
                    page = Math.Max(1, returnPage),
                    pageSize,
                    status = string.IsNullOrWhiteSpace(returnStatus) ? "not_solved" : returnStatus,
                    highlight = workspace.GlpiTicketId
                });

            return View(workspace);
        }

        [HttpPost]
        public async Task<IActionResult> SaveDraft(Guid id, [FromBody] SaveRequirementDraftRequest request)
        {
            var response = await CreateClient().PutAsJsonAsync($"api/glpi/tickets/{id}/draft", request);
            return new ContentResult { Content = await response.Content.ReadAsStringAsync(), ContentType = "application/json", StatusCode = (int)response.StatusCode };
        }

        [HttpGet]
        public async Task<IActionResult> SearchWorkspaceWorkPackages(Guid id, string query)
        {
            var response = await CreateClient().GetAsync(
                $"api/glpi/tickets/{id}/openproject-work-packages?query={Uri.EscapeDataString(query ?? string.Empty)}");
            return new ContentResult
            {
                Content = await response.Content.ReadAsStringAsync(),
                ContentType = "application/json",
                StatusCode = (int)response.StatusCode
            };
        }

        [HttpPost]
        public async Task<IActionResult> LinkWorkspaceWorkPackage(Guid id, int workPackageId)
        {
            var response = await CreateClient().PostAsync(
                $"api/glpi/tickets/{id}/openproject-work-packages/{workPackageId}/glpi-link", null);
            return new ContentResult
            {
                Content = await response.Content.ReadAsStringAsync(),
                ContentType = "application/json",
                StatusCode = (int)response.StatusCode
            };
        }

        [HttpPost]
        public async Task<IActionResult> AddWorkspaceWorkPackagePrivateComment(Guid id, int workPackageId)
        {
            var response = await CreateClient().PostAsync(
                $"api/glpi/tickets/{id}/openproject-work-packages/{workPackageId}/private-comment", null);
            return new ContentResult
            {
                Content = await response.Content.ReadAsStringAsync(),
                ContentType = "application/json",
                StatusCode = (int)response.StatusCode
            };
        }

        [HttpPost]
        [RequestSizeLimit(8 * 1024 * 1024)]
        public async Task<IActionResult> UploadWorkspaceImage(Guid id, IFormFile? image)
        {
            if (image == null || image.Length == 0)
                return BadRequest(new { message = "Selecione uma imagem para enviar." });
            if (image.Length > 8 * 1024 * 1024)
                return BadRequest(new { message = "A imagem deve ter no máximo 8 MB." });

            using var form = new MultipartFormDataContent();
            await using var stream = image.OpenReadStream();
            using var fileContent = new StreamContent(stream);
            var contentType = string.IsNullOrWhiteSpace(image.ContentType) ? "application/octet-stream" : image.ContentType;
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(fileContent, "image", image.FileName);

            var response = await CreateClient().PostAsync($"api/glpi/tickets/{id}/images", form);
            if (!response.IsSuccessStatusCode)
            {
                return new ContentResult
                {
                    Content = await response.Content.ReadAsStringAsync(),
                    ContentType = "application/json",
                    StatusCode = (int)response.StatusCode
                };
            }

            var uploaded = await response.Content.ReadFromJsonAsync<WorkspaceImageUploadResultDto>();
            if (uploaded == null) return StatusCode(502, new { message = "A imagem foi enviada, mas a integração não retornou sua identificação." });

            var imagePath = Url.Action(nameof(WorkspaceImage), new { imageId = uploaded.Id })
                ?? $"/ServiceDesk/WorkspaceImage/{uploaded.Id}";
            uploaded.Url = $"{Request.Scheme}://{Request.Host}{imagePath}";
            return Json(uploaded);
        }

        [AllowAnonymous]
        [HttpGet("ServiceDesk/WorkspaceImage/{imageId:guid}")]
        public async Task<IActionResult> WorkspaceImage(Guid imageId)
        {
            var response = await _httpClientFactory.CreateClient("Api")
                .GetAsync($"api/glpi/tickets/workspace-images/{imageId}");
            if (!response.IsSuccessStatusCode) return NotFound();

            return File(
                await response.Content.ReadAsByteArrayAsync(),
                response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream");
        }

        [HttpGet]
        public async Task<IActionResult> OpenProjectProjects()
        {
            var response = await CreateClient().GetAsync("api/glpi/tickets/openproject-projects");
            return new ContentResult { Content = await response.Content.ReadAsStringAsync(), ContentType = "application/json", StatusCode = (int)response.StatusCode };
        }

        [HttpPost]
        public async Task<IActionResult> CreateUserStory(Guid id, [FromBody] CreateOpenProjectUserStoryRequest request)
        {
            var response = await CreateClient().PostAsJsonAsync($"api/glpi/tickets/{id}/user-story", request);
            return new ContentResult { Content = await response.Content.ReadAsStringAsync(), ContentType = "application/json", StatusCode = (int)response.StatusCode };
        }

        [HttpPost]
        public async Task<IActionResult> ReprocessGlpiLink(Guid id)
        {
            var response = await CreateClient().PostAsync($"api/glpi/tickets/{id}/glpi-link/reprocess", null);
            return new ContentResult { Content = await response.Content.ReadAsStringAsync(), ContentType = "application/json", StatusCode = (int)response.StatusCode };
        }

        [HttpGet]
        public async Task<IActionResult> Attachment(Guid id, int documentId)
        {
            var response = await CreateClient().GetAsync($"api/glpi/tickets/{id}/attachments/{documentId}");
            if (!response.IsSuccessStatusCode) return NotFound();
            return File(await response.Content.ReadAsByteArrayAsync(), response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream");
        }

        private async Task<GlpiImprovementTicketsResponse> GetAllQueueItemsAsync(ServiceDeskQueueQueryDto request)
        {
            request ??= new ServiceDeskQueueQueryDto();
            var all = new List<GlpiImprovementTicketDto>();
            var page = 1;
            GlpiImprovementTicketsResponse? first = null;
            do
            {
                var pageRequest = new ServiceDeskQueueQueryDto
                {
                    Page = page,
                    PageSize = 100,
                    Status = request.Status,
                    Search = request.Search,
                    Client = request.Client,
                    Stage = request.Stage,
                    Priority = request.Priority,
                    WorkPackage = request.WorkPackage,
                    OnlyRisk = request.OnlyRisk,
                    OnlyMine = request.OnlyMine,
                    Sort = request.Sort
                };
                var response = await CreateClient().GetAsync($"api/glpi/tickets/improvements?{BuildQueueQuery(pageRequest)}");
                if (!response.IsSuccessStatusCode) throw new InvalidOperationException(await ReadErrorDetailAsync(response));
                var data = await response.Content.ReadFromJsonAsync<GlpiImprovementTicketsResponse>() ?? new GlpiImprovementTicketsResponse();
                first ??= data;
                all.AddRange(data.Items);
                if (page >= data.TotalPages) break;
                page++;
            } while (true);

            first ??= new GlpiImprovementTicketsResponse();
            first.Items = all;
            first.TotalCount = all.Count;
            first.Page = 1;
            first.PageSize = Math.Max(1, all.Count);
            return first;
        }

        private static string BuildQueueQuery(ServiceDeskQueueQueryDto request)
        {
            var values = new Dictionary<string, string?>
            {
                ["page"] = Math.Max(1, request.Page).ToString(),
                ["pageSize"] = request.PageSize.ToString(),
                ["status"] = request.Status,
                ["search"] = request.Search,
                ["client"] = request.Client,
                ["stage"] = request.Stage,
                ["priority"] = request.Priority,
                ["workPackage"] = request.WorkPackage,
                ["onlyRisk"] = request.OnlyRisk.ToString().ToLowerInvariant(),
                ["onlyMine"] = request.OnlyMine.ToString().ToLowerInvariant(),
                ["sort"] = request.Sort
            };
            return string.Join('&', values.Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
                .Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value!)}"));
        }

        private static string Csv(object? value)
        {
            var text = Convert.ToString(value) ?? string.Empty;
            return $"\"{text.Replace("\"", "\"\"")}\"";
        }

        private async Task<UnifiedBacklogResponse> GetDashboardBacklogAsync()
        {
            var response = await CreateClient().GetAsync("api/glpi/tickets/dashboard");
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException(await ReadErrorDetailAsync(response));
            return await response.Content.ReadFromJsonAsync<UnifiedBacklogResponse>() ?? new UnifiedBacklogResponse();
        }

        private static int GetPriorityOrder(string? priority) => priority switch
        {
            "Crítica" => 0,
            "Alta" => 1,
            _ => 2
        };

        private HttpClient CreateClient()
        {
            var client = _httpClientFactory.CreateClient("Api");
            var token = User.FindFirst("JWToken")?.Value;
            if (!string.IsNullOrWhiteSpace(token)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        private static async Task<string> ReadErrorDetailAsync(HttpResponseMessage response)
        {
            try
            {
                var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
                if (!string.IsNullOrWhiteSpace(error?.Message))
                {
                    return error.Message;
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Fall back to the response text when the integration did not return JSON.
            }

            var content = await response.Content.ReadAsStringAsync();
            return string.IsNullOrWhiteSpace(content)
                ? $"A integração retornou HTTP {(int)response.StatusCode} ({response.ReasonPhrase})."
                : content;
        }

        private sealed class ApiErrorResponse
        {
            public string? Message { get; init; }
        }
    }
}
