using System.Net.Http.Headers;
using System.Net.Http.Json;
using Axiom.Atlas.Application.DTOs.ServiceDesk;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;

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
        public async Task<IActionResult> List(int page = 1, int pageSize = 25, string? status = null, bool refresh = false)
        {
            try
            {
                var response = await CreateClient().GetAsync(
                    $"api/glpi/tickets/improvements?page={page}&pageSize={pageSize}&status={Uri.EscapeDataString(status ?? "not_solved")}&refresh={refresh.ToString().ToLowerInvariant()}");
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
