using gamabelmvc.Api;
using gamabelmvc.Services.Api2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace gamabelmvc.Controllers.Api2;

[ApiController]
[Authorize(
    AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
    Roles = "Admin")]
[Route("api2/birimler")]
public sealed class BirimlerController(
    IBirimDirectoryService birimDirectoryService) : ControllerBase
{
    /// <summary>List units from the server's MyConnection database.</summary>
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyList<Api2BirimResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<Api2BirimResponse>>> Get(
        CancellationToken cancellationToken) =>
        Ok(await birimDirectoryService.GetBirimlerAsync(cancellationToken));
}
