using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Axiom.Atlas.Api.Transformers
{
    public class HideOcelotOperationsTransformer : IOpenApiOperationTransformer
    {
        public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
        {
            var relativePath = context?.Description?.RelativePath;
            if (!string.IsNullOrEmpty(relativePath) &&
                relativePath.Contains("ocelot", StringComparison.OrdinalIgnoreCase))
            {
                if (context?.Document?.Paths != null)
                {
                    var key = "/" + relativePath.TrimStart('/');
                    if (context.Document.Paths.ContainsKey(key))
                        context.Document.Paths.Remove(key);
                }
            }

            return Task.CompletedTask;
        }
    }
}
