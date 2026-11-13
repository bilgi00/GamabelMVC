using gamabelmvc.Api;
using gamabelmvc.Services.MobileApi;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace gamabelmvc.Controllers.MobileApi;

[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api")]
public sealed class MobileInventoryController(IMobileInventoryService inventoryService) : ControllerBase
{
    /// <summary>List stock-count sessions.</summary>
    [HttpGet("counts")]
    [ProducesResponseType<IReadOnlyList<MobileCountResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MobileCountResponse>>> GetCounts(
        CancellationToken cancellationToken) =>
        Ok(await inventoryService.GetCountsAsync(cancellationToken));

    /// <summary>List active products in the catalog.</summary>
    [HttpGet("products")]
    [ProducesResponseType<IReadOnlyList<MobileProductResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MobileProductResponse>>> GetProducts(
        CancellationToken cancellationToken) =>
        Ok(await inventoryService.GetProductsAsync(cancellationToken));

    /// <summary>Add a product to the shared catalog.</summary>
    [HttpPost("products")]
    [ProducesResponseType<MobileProductResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MobileProductResponse>> CreateProduct(
        MobileCreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await inventoryService.CreateProductAsync(request, cancellationToken);
        return product == null
            ? Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Product already exists",
                detail: "A product with this barcode is already registered.")
            : CreatedAtAction(nameof(GetProducts), product);
    }

    /// <summary>List the system-stock rows for a stock-count session.</summary>
    [HttpGet("counts/{id:int}/system-stock")]
    [ProducesResponseType<IReadOnlyList<MobileSystemStockResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<MobileSystemStockResponse>>> GetSystemStock(
        int id,
        CancellationToken cancellationToken)
    {
        var stock = await inventoryService.GetSystemStockAsync(id, cancellationToken);
        return stock == null
            ? Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Count not found",
                detail: "The requested stock-count session does not exist.")
            : Ok(stock);
    }

    /// <summary>List non-cancelled movements for a stock-count session.</summary>
    [HttpGet("counts/{id:int}/movements")]
    [ProducesResponseType<IReadOnlyList<MobileMovementResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<MobileMovementResponse>>> GetMovements(
        int id,
        CancellationToken cancellationToken)
    {
        var movements = await inventoryService.GetMovementsAsync(id, cancellationToken);
        return movements == null
            ? Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Count not found",
                detail: "The requested stock-count session does not exist.")
            : Ok(movements);
    }

    /// <summary>Record a count movement. Repeating the same UUID and payload is safe.</summary>
    [HttpPost("counts/{id:int}/movements")]
    [ProducesResponseType<MobileMovementResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<MobileMovementResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MobileMovementResponse>> CreateMovement(
        int id,
        MobileCreateMovementRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.Uuid, out var uuid) || uuid == Guid.Empty)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid UUID",
                detail: "Uuid must be a non-empty UUID string.");
        }

        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username))
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthenticated",
                detail: "A valid bearer token is required.");
        }

        var result = await inventoryService.CreateMovementAsync(
            id,
            uuid,
            request,
            username,
            cancellationToken);
        return result.Status switch
        {
            MobileMovementSaveStatus.Created =>
                CreatedAtAction(nameof(GetMovements), new { id }, result.Movement),
            MobileMovementSaveStatus.Duplicate => Ok(result.Movement),
            MobileMovementSaveStatus.NotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Count not found",
                detail: "The requested stock-count session does not exist."),
            MobileMovementSaveStatus.CountClosed => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Count is closed",
                detail: "Movements cannot be added to a closed stock-count session."),
            MobileMovementSaveStatus.IdempotencyConflict => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "UUID already used",
                detail: "This UUID was already used for a different movement."),
            _ => throw new InvalidOperationException("Unknown movement save status.")
        };
    }
}
