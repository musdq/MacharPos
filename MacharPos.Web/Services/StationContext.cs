using MacharPos.Web.Auth;
using Microsoft.AspNetCore.Identity;

namespace MacharPos.Web.Services;

public interface IStationContext
{
    Task<Guid> GetStationIdOrThrowAsync();
}

public class StationContext : IStationContext
{
    private readonly IHttpContextAccessor _http;
    private readonly UserManager<ApplicationUser> _users;

    public StationContext(IHttpContextAccessor http, UserManager<ApplicationUser> users)
    {
        _http = http;
        _users = users;
    }

    public async Task<Guid> GetStationIdOrThrowAsync()
    {
        var principal = _http.HttpContext?.User;
        if (principal is null) throw new InvalidOperationException("No HttpContext.");

        var user = await _users.GetUserAsync(principal);
        if (user?.HomeStationId is null || user.HomeStationId == Guid.Empty)
            throw new InvalidOperationException("User has no Home Station assigned.");

        return user.HomeStationId.Value;
    }
}