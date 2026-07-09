using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Axiom.Atlas.Api.Transformers
{
    public sealed class DateTimeSchemaTransformer : IOpenApiSchemaTransformer
    {
        public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
        {
            // Implementação que ajusta schema para DateTime
            return Task.CompletedTask;
        }
    }
}
