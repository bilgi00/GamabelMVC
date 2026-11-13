using System.ComponentModel.DataAnnotations;

namespace gamabelmvc.Api;

/// <summary>Credentials used to obtain a short-lived mobile API bearer token.</summary>
public sealed record MobileLoginRequest
{
    [Required, StringLength(50)]
    public required string Username { get; init; }

    [Required, StringLength(200)]
    public required string Password { get; init; }
}

/// <summary>Public identity information returned after a successful login.</summary>
public sealed record MobileUserResponse(int Id, string Username, string FullName, string Role);

/// <summary>A safe user-directory entry; password data is never included.</summary>
public sealed record MobileDirectoryUserResponse(
    int Id,
    string Username,
    string FullName,
    bool IsActive);

/// <summary>Bearer token and identity returned after a successful login.</summary>
public sealed record MobileLoginResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt,
    MobileUserResponse User);

/// <summary>A stock-count session available to the mobile application.</summary>
public sealed record MobileCountResponse(
    int Id,
    string Name,
    string Status,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    string CreatedBy);

/// <summary>A product in the active product catalog.</summary>
public sealed record MobileProductResponse(
    int Id,
    string Name,
    string? Barcode,
    string Unit,
    string? Company,
    bool IsActive);

/// <summary>Payload for adding a product to the shared product catalog.</summary>
public sealed record MobileCreateProductRequest
{
    [Required, StringLength(200)]
    public required string Name { get; init; }

    [StringLength(50)]
    public string? Barcode { get; init; }

    [StringLength(20)]
    public string? Unit { get; init; }

    [StringLength(255)]
    public string? Company { get; init; }
}

/// <summary>System-stock information for a product in a stock-count session.</summary>
public sealed record MobileSystemStockResponse(
    int Id,
    int CountId,
    string? StockCode,
    string? ReferenceCode,
    string Barcode,
    string ProductName,
    decimal? IncomingQuantity,
    decimal? OutgoingQuantity,
    decimal SystemStock,
    string? Unit,
    decimal? PurchasePrice,
    decimal? SalesPrice,
    string? PieceType);

/// <summary>A recorded movement in a stock-count session.</summary>
public sealed record MobileMovementResponse(
    int Id,
    int CountId,
    Guid? Uuid,
    string Barcode,
    string ProductName,
    decimal Quantity,
    string Username,
    DateTimeOffset OccurredAt,
    string? Comment,
    string Source,
    bool IsNewProduct);

/// <summary>Payload for recording a count movement; UUID makes retries idempotent.</summary>
public sealed record MobileCreateMovementRequest
{
    [Required, StringLength(36, MinimumLength = 36)]
    public required string Uuid { get; init; }

    [Required, StringLength(100)]
    public required string Barcode { get; init; }

    [Required, StringLength(300)]
    public required string ProductName { get; init; }

    [Range(typeof(decimal), "0.001", "999999999999999.999")]
    public decimal Quantity { get; init; }

    [StringLength(2000)]
    public string? Comment { get; init; }
}

public enum MobileMovementSaveStatus
{
    Created,
    Duplicate,
    NotFound,
    CountClosed,
    IdempotencyConflict
}

public sealed record MobileMovementSaveResult(
    MobileMovementSaveStatus Status,
    MobileMovementResponse? Movement);
