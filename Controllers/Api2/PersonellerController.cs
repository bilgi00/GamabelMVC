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
[Route("api2/personeller")]
public sealed class PersonellerController(
    IPersonelDirectoryService personelDirectoryService) : ControllerBase
{
    /// <summary>List active personnel using the server's MyConnection database.</summary>
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyList<Api2PersonelResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<Api2PersonelResponse>>> Get(
        CancellationToken cancellationToken) =>
        Ok(await personelDirectoryService.GetActivePersonellerAsync(cancellationToken));
}
