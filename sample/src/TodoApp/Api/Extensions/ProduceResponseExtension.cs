namespace Todo.Api.Extensions;

internal static class ProduceResponseExtension
{
    internal sealed record ProblemResponseDescription(string StatusCode, string Description);

    extension<TBuilder>(TBuilder builder) where TBuilder : IEndpointConventionBuilder
    {
        public TBuilder ProduceProblem(int statusCode, string description, string? contentType = null)
        {
            builder.ProducesProblem(statusCode, contentType);
            builder.WithMetadata(new ProblemResponseDescription(statusCode.ToString(), description));

            return builder;
        }

        public TBuilder ProduceProblemValidation(
                        string description,
                        int statusCode = StatusCodes.Status400BadRequest,
                        string? contentType = null)
        {
            builder.ProducesValidationProblem(statusCode, contentType);

            builder.WithMetadata(new ProblemResponseDescription(statusCode.ToString(), description));

            return builder;
        }

    }

    extension(ActionDescriptor actionDescriptor)
    {
        public void AddProblemResponseDescription(OpenApiOperation operation)
        {

            foreach (var meta in actionDescriptor.EndpointMetadata
                         .OfType<ProblemResponseDescription>())
            {
                if (operation.Responses!.TryGetValue(meta.StatusCode, out var response))
                    response.Description = meta.Description;
            }
        }
    }
}

