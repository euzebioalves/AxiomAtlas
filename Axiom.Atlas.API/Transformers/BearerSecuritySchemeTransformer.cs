using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Axiom.Atlas.Api.Transformers
{
    public sealed class BearerSecuritySchemeTransformer(IAuthenticationSchemeProvider authenticationSchemeProvider) : IOpenApiDocumentTransformer
    {
        public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
        {
            var authenticationSchemes = await authenticationSchemeProvider.GetAllSchemesAsync();
            if (authenticationSchemes.Any(authScheme => authScheme.Name == "Bearer" || authScheme.Name == "Identity.Bearer"))
            {
                document.Components ??= new OpenApiComponents();

                // Inicializa o dicionário de SecuritySchemes se estiver nulo
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

                // 1. Define o Esquema
                document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    In = ParameterLocation.Header,
                    BearerFormat = "JWT",
                    Description = "Insira seu token JWT"
                };

                // 2. Adiciona o Requisito (O QUE FAZ O CADEADO APARECER)
                var requirement = new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecuritySchemeReference("Bearer", document),
                        new List<string>()
                    }
                };

                document.Security ??= new List<OpenApiSecurityRequirement>();
                document.Security.Add(requirement);
            }
        }
    }
}
