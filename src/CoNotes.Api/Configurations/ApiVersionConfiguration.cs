using Asp.Versioning;

namespace CoNotes.Api.Configurations;

internal static class ApiVersionConfiguration
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddApiVersionConfiguration()
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
                .AddOpenApi();

            return services;
        }
    }
}
