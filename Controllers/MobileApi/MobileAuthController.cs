using gamabelmvc.Api;
using gamabelmvc.Services.MobileApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace gamabelmvc.Controllers.MobileApi;

[ApiController]
[Route("api/auth")]
public sealed class MobileAuthController(IMobileAuthService authService) : ControllerBase
{
    /// <summary>Authenticate an active STS user and issue a short-lived bearer token.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("MobileLogin")]
    [HttpPost("login")]
    [ProducesResponseType<MobileLoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<MobileLoginResponse>> Login(
        MobileLoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);
        return result == null
            ? Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid credentials",
                detail: "The username or password is incorrect.")
            : Ok(result);
    }
}
