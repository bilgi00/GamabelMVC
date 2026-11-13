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
        if (context.Request.Path.StartsWithSegments("/api")
            && !context.Request.Path.StartsWithSegments("/api/health")
            && !MobileJwtSettings.HasValidSigningKey(configuration))
        {
            logger.LogError(
                "Mobile API request rejected because MobileApi:Jwt:SigningKey is missing or shorter than 32 UTF-8 bytes.");
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/problem+json";
            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                new ProblemDetails
                {
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Title = "Service Unavailable",
                    Detail = "The mobile API is not configured."
                },
                new JsonSerializerOptions(JsonSerializerDefaults.Web),
                context.RequestAborted);
            return;
        }

        await next(context);
    }
}
