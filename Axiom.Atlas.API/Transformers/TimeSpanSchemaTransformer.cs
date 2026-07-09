using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Axiom.Atlas.Api.Transformers
{
    public sealed class TimeSpanSchemaTransformer : IOpenApiSchemaTransformer
    {
        public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context,
            CancellationToken cancellationToken)
        {
            var type = context.JsonTypeInfo?.Type;
            if (type == typeof(TimeSpan) || type == typeof(TimeSpan?))
            {
                schema.Type = JsonSchemaType.String;
                schema.Format = "duration";
            }

            return Task.CompletedTask;
        }
    }
}
