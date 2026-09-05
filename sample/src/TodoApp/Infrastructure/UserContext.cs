using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Todo.Infrastructure;

internal sealed class UserContext(IHttpContextAccessor accessor) : IUserContext
{
    private ClaimsPrincipal User =>
           accessor.HttpContext?.User
           ?? throw new InvalidOperationException("HttpContext was not available.");

    public Guid UserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    public string Name => User.FindFirst("name")!.Value;
    public string Email => User.FindFirst(ClaimTypes.Email)!.Value;
}

