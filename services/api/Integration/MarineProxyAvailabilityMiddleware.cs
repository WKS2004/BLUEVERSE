using Yarp.ReverseProxy.Forwarder;

namespace Blueverse.Api.Integration;

/// <summary>Preserves independent gateway routes when the Marine destination is unreachable.</summary>
public sealed class MarineProxyAvailabilityMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);
        if (!context.Request.Path.StartsWithSegments("/api/marine") || context.Response.HasStarted ||
            context.RequestAborted.IsCancellationRequested ||
            context.Features.Get<IForwarderErrorFeature>()?.Error is not (ForwarderError.Request or ForwarderError.RequestTimedOut))
            return;

        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        var read = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);
        if (read) context.Response.Headers.RetryAfter = "5";
        await context.Response.WriteAsJsonAsync(new
        {
            status = 503,
            title = "Marine Safety Unavailable",
            detail = read ? "Marine safety data is unavailable. Try again shortly."
                : "The action outcome could not be confirmed. Check history or the safety profile before trying again.",
            availability = "UNAVAILABLE",
            dependency = "marine-safety",
            code = "MARINE_SERVICE_UNAVAILABLE",
            reason = context.Features.Get<IForwarderErrorFeature>()?.Error == ForwarderError.RequestTimedOut ? "TIMEOUT" : "UNREACHABLE",
            missingFields = new[] { "marineSafetyData" },
            retryable = read,
            data = (object?)null
        }, options: null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
}
