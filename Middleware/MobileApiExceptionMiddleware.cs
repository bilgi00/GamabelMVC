using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace gamabelmvc.Middleware;

public sealed class MobileApiExceptionMiddleware(
    RequestDelegate next,
    ILogger<MobileApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception) when (
            context.Request.Path.StartsWithSegments("/api")
            && exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Unhandled error while processing mobile API request.");
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Internal Server Error",
                    Detail = "The request could not be completed.",
                    Instance = context.Request.Path
                },
                new JsonSerializerOptions(JsonSerializerDefaults.Web),
                context.RequestAborted);
        }
    }
}
