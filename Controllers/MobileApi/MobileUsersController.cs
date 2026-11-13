using gamabelmvc.Api;
using gamabelmvc.Services.MobileApi;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace gamabelmvc.Controllers.MobileApi;

[ApiController]
[Authorize(
    AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
    Roles = "Admin")]
[Route("api/users")]
public sealed class MobileUsersController(
    IMobileUserDirectoryService userDirectoryService) : ControllerBase
{
    /// <summary>List safe user-directory fields for administrators.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MobileDirectoryUserResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<MobileDirectoryUserResponse>>> GetUsers(
        CancellationToken cancellationToken) =>
        Ok(await userDirectoryService.GetUsersAsync(cancellationToken));
}
