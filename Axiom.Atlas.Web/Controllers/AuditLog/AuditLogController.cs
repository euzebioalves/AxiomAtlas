using Axiom.Atlas.Web.Model.AuditLog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;

namespace Axiom.Atlas.Web.Controllers.AuditLog
{
    [Authorize(Policy = "AdministrationOnly")]
    public class AuditLogController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public AuditLogController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // Renderiza a View do Metronic que criamos
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // Endpoint local que o DataTables vai chamar
        [HttpGet]
        public async Task<IActionResult> SearchData([FromQuery] AuditLogFilterViewModel filter)
        {
            var client = _httpClientFactory.CreateClient("Api");
            var token = User.FindFirst("JWToken")?.Value;

            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            try
            {
                // Monta a QueryString com os parâmetros para enviar para a API
                var queryParams = new List<string>();
                if (filter.DataInicio.HasValue) queryParams.Add($"DataInicio={filter.DataInicio:yyyy-MM-dd}");
                if (filter.DataFim.HasValue) queryParams.Add($"DataFim={filter.DataFim:yyyy-MM-dd}");
                if (!string.IsNullOrEmpty(filter.Tabela)) queryParams.Add($"Tabela={filter.Tabela}");
                if (!string.IsNullOrEmpty(filter.TipoAcao)) queryParams.Add($"TipoAcao={filter.TipoAcao}");
                if (!string.IsNullOrEmpty(filter.Usuario)) queryParams.Add($"Usuario={filter.Usuario}");

                var queryString = queryParams.Any() ? "?" + string.Join("&", queryParams) : "";

                // Faz o GET seguro do Server (Web) para o Server (API)
                var response = await client.GetAsync($"api/AuditLog/search{queryString}");

                if (response.IsSuccessStatusCode)
                {
                    var logs = await response.Content.ReadFromJsonAsync<List<AuditLogViewModel>>();
                    return Json(logs); // O DataTables lê esse JSON automaticamente
                }

                return Json(new List<AuditLogViewModel>());
            }
            catch (Exception)
            {
                // Logar o erro internamente, se necessário
                return Json(new List<AuditLogViewModel>());
            }
        }
    }
}
