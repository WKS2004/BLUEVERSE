using System.Diagnostics;
using System.Net.Http.Headers;

namespace Blueverse.CoastalPlanner.Integration;

public sealed class PeerRequestContextHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    public static readonly HttpRequestOptionsKey<bool> DoNotForwardUserAuthorization =
        new("Blueverse.DoNotForwardUserAuthorization");

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var context = accessor.HttpContext;
        if (!request.Options.TryGetValue(DoNotForwardUserAuthorization, out var doNotForward) || !doNotForward)
        {
            if (context?.User.Identity?.IsAuthenticated == true)
            {
                var token = context.Request.Headers.Authorization.ToString();
                if (AuthenticationHeaderValue.TryParse(token, out var authorization) && authorization.Scheme == "Bearer")
                    request.Headers.Authorization = authorization;
                else if (context.Request.Cookies.TryGetValue("blueverse_access_token", out var cookie))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cookie);
            }
        }
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", Activity.Current?.TraceId.ToString() ?? context?.TraceIdentifier ?? Guid.NewGuid().ToString("N"));
        return base.SendAsync(request, ct);
    }
}
