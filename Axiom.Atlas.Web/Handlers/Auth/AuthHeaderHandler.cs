using Microsoft.AspNetCore.Authentication;
using System.Net.Http.Headers;

namespace Axiom.Atlas.Web.Handlers.Auth
{
    public class AuthHeaderHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuthHeaderHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var token = httpContext is null ? null : await httpContext.GetTokenAsync("access_token");

            if (string.IsNullOrEmpty(token))
            {
                token = httpContext?.User.FindFirst("JWToken")?.Value;
            }

            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
