namespace CoNotes.Api.Endpoints.v1.Billing;

internal sealed class BillingGroupEndpoint : IGroupEndpoint<ApiVersionEndpoint>
{
    public RouteGroupBuilder Configure(IEndpointRouteBuilder endpoints)
    {
        var billing = endpoints
                    .MapGroup("billing")
                    .WithTags("billing")
                    .HasApiVersion(1.0)
                    .RequireAuthorization();

        return billing;
    }
}
