namespace Todo.Api.Configurations;

public static class ApiVersionConfiguration
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddApiVersionConfiguration(string applicationName)
        {
            services
                .AddApiVersioning(o =>
                {
                    o.AssumeDefaultVersionWhenUnspecified = true;
                    o.ReportApiVersions = true;
                    o.ApiVersionReader = new UrlSegmentApiVersionReader();
                })
                .AddApiExplorer(o =>
                {
                    o.GroupNameFormat = "'v'VVV";
                    o.SubstituteApiVersionInUrl = true;
                })
                .AddOpenApi(o =>
                {
                    o.Document.AddDocumentTransformer((document, context, ct) =>
                    {
                        document.Info.Title = applicationName;
                        document.Info.Description = "Todo App Api";
                        return Task.CompletedTask;
                    });

                    o.Document.AddOperationTransformer((operation, context, ct) =>
                      {
                          context.Description.ActionDescriptor.AddProblemResponseDescription(operation);
                          return Task.CompletedTask;
                      });

                    o.Document.AddSchemaTransformer((schema, cotext, ct) =>
                    {
                        return Task.CompletedTask;
                    });
                });

            return services;

        }
    }
}

