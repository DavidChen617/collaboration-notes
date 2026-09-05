namespace Todo.Api.Configurations;

internal static class ProblemDetailConfiguration
{
    extension(IServiceCollection services)
    {

        public IServiceCollection AddProblemDetailConfiguration()
        {
            services.AddProblemDetails(o =>
            {
                o.CustomizeProblemDetails = context =>
                {
                    context.ProblemDetails.Instance = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
                    context.ProblemDetails.Extensions.TryAdd("requestId", context.HttpContext.TraceIdentifier);
                };
            });

            return services;
        }
    }
}

