using System.Text.Json;
using gamabelmvc.Services.MobileApi;
using Microsoft.AspNetCore.Mvc;

namespace gamabelmvc.Middleware;

public sealed class MobileApiConfigurationMiddleware(
    RequestDelegate next,
    IConfiguration configuration,
    ILogger<MobileApiConfigurationMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var isMobileApiRequest = context.Request.Path.StartsWithSegments("/api");
        var isApi2Request = context.Request.Path.StartsWithSegments("/api2");
        var isMobileHealthRequest =
            isMobileApiRequest && context.Request.Path.StartsWithSegments("/api/health");
        if ((isMobileApiRequest && !isMobileHealthRequest || isApi2Request)
            && !MobileJwtSettings.HasValidSigningKey(configuration))
        {
            logger.LogError(
                "API request rejected because MobileApi:Jwt:SigningKey is missing or shorter than 32 UTF-8 bytes.");
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/problem+json";
            var detail = isApi2Request
                ? "The API2 service is not configured."
                : "The mobile API is not configured.";
            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                new ProblemDetails
                {
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Title = "Service Unavailable",
                    Detail = detail
                },
                new JsonSerializerOptions(JsonSerializerDefaults.Web),
                context.RequestAborted);
            return;
        }

        await next(context);
    }
}
