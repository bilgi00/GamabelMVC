using gamabelmvc.Api;
using gamabelmvc.Data;
using gamabelmvc.Models.StokSayim;
using gamabelmvc.Services;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace gamabelmvc.Services.MobileApi;

public sealed class MobileInventoryService(
    StokSayimDbContext dbContext,
    DbConnectionFactory connectionFactory) : IMobileInventoryService
{
    public async Task<IReadOnlyList<MobileCountResponse>> GetCountsAsync(
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Sayimlar
            .AsNoTracking()
            .OrderByDescending(count => count.AcilisZamani)
            .Select(count => new
            {
                count.Id,
                count.Ad,
                count.Durum,
                count.AcilisZamani,
                count.KapanisZamani,
                count.OlusturanKullanici
            })
            .ToListAsync(cancellationToken);

        return rows.Select(count => new MobileCountResponse(
            count.Id,
            count.Ad,
            count.Durum,
            AsUtcOffset(count.AcilisZamani),
            count.KapanisZamani.HasValue ? AsUtcOffset(count.KapanisZamani.Value) : null,
            count.OlusturanKullanici)).ToList();
    }

    public async Task<IReadOnlyList<MobileProductResponse>> GetProductsAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(@"
            SELECT Id, Ad, Barkod, Birim, Firma, AktifMi
            FROM stk_Urun
            WHERE AktifMi = TRUE
            ORDER BY Ad", connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var products = new List<MobileProductResponse>();

        while (await reader.ReadAsync(cancellationToken))
        {
            products.Add(new MobileProductResponse(
                reader.GetInt32("Id"),
                reader.GetString("Ad"),
                reader["Barkod"] == DBNull.Value ? null : reader["Barkod"]?.ToString(),
                reader["Birim"]?.ToString() ?? "Adet",
                reader["Firma"] == DBNull.Value ? null : reader["Firma"]?.ToString(),
                reader.GetBoolean("AktifMi")));
        }

        return products;
    }

    public async Task<MobileProductResponse?> CreateProductAsync(
        MobileCreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim();
        var unit = string.IsNullOrWhiteSpace(request.Unit) ? "Adet" : request.Unit.Trim();
        var company = string.IsNullOrWhiteSpace(request.Company) ? null : request.Company.Trim();

        await using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(@"
            INSERT INTO stk_Urun (Ad, Barkod, Birim, Firma, AktifMi)
            VALUES (@name, @barcode, @unit, @company, TRUE)", connection);
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@barcode", (object?)barcode ?? DBNull.Value);
        command.Parameters.AddWithValue("@unit", unit);
        command.Parameters.AddWithValue("@company", (object?)company ?? DBNull.Value);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (MySqlException exception) when (exception.Number == 1062)
        {
            return null;
        }

        return new MobileProductResponse(
            checked((int)command.LastInsertedId),
            name,
            barcode,
            unit,
            company,
            true);
    }

    public async Task<IReadOnlyList<MobileSystemStockResponse>?> GetSystemStockAsync(
        int countId,
        CancellationToken cancellationToken)
    {
        var countExists = await dbContext.Sayimlar
            .AsNoTracking()
            .AnyAsync(count => count.Id == countId, cancellationToken);
        if (!countExists)
            return null;

        return await dbContext.SayimSistemStoklari
            .AsNoTracking()
            .Where(stock => stock.SayimId == countId)
            .OrderBy(stock => stock.UrunAdi)
            .Select(stock => new MobileSystemStockResponse(
                stock.Id,
                stock.SayimId,
                stock.StokKodu,
                stock.ReferansKodu,
                stock.Barkod ?? string.Empty,
                stock.UrunAdi,
                stock.GirenMiktar,
                stock.CikanMiktar,
                stock.SistemStok,
                stock.Birim,
                stock.AlisFiyati,
                stock.SatisFiyati,
                stock.PCinsi))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MobileMovementResponse>?> GetMovementsAsync(
        int countId,
        CancellationToken cancellationToken)
    {
        var countExists = await dbContext.Sayimlar
            .AsNoTracking()
            .AnyAsync(count => count.Id == countId, cancellationToken);
        if (!countExists)
            return null;

        var movements = await dbContext.SayimHareketleri
            .AsNoTracking()
            .Where(movement => movement.SayimId == countId && !movement.Iptal)
            .OrderByDescending(movement => movement.Zaman)
            .Select(movement => new MovementRow(
                movement.Id,
                movement.SayimId,
                movement.MobileRequestId,
                movement.Barkod,
                movement.UrunAdi,
                movement.Miktar,
                movement.KullaniciAdi,
                movement.Zaman,
                movement.Aciklama,
                movement.Kaynak,
                movement.YeniUrunMu))
            .ToListAsync(cancellationToken);

        return movements.Select(ToResponse).ToList();
    }

    public async Task<MobileMovementSaveResult> CreateMovementAsync(
        int countId,
        Guid uuid,
        MobileCreateMovementRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        var barcode = request.Barcode.Trim();
        var productName = request.ProductName.Trim();
        var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();

        var existing = await dbContext.SayimHareketleri
            .AsNoTracking()
            .SingleOrDefaultAsync(movement => movement.MobileRequestId == uuid, cancellationToken);
        if (existing != null)
            return ExistingResult(existing, countId, barcode, productName, request.Quantity, comment);

        var countStatus = await dbContext.Sayimlar
            .AsNoTracking()
            .Where(count => count.Id == countId)
            .Select(count => count.Durum)
            .SingleOrDefaultAsync(cancellationToken);
        if (countStatus == null)
            return new MobileMovementSaveResult(MobileMovementSaveStatus.NotFound, null);
        if (!string.Equals(countStatus, "Açık", StringComparison.OrdinalIgnoreCase))
            return new MobileMovementSaveResult(MobileMovementSaveStatus.CountClosed, null);

        var isNewProduct = !await dbContext.SayimSistemStoklari
            .AsNoTracking()
            .AnyAsync(stock => stock.SayimId == countId && stock.Barkod == barcode, cancellationToken);
        var movement = new SayimHareketi
        {
            SayimId = countId,
            MobileRequestId = uuid,
            Barkod = barcode,
            UrunAdi = productName,
            Miktar = request.Quantity,
            KullaniciAdi = username,
            Zaman = DateTime.UtcNow,
            Aciklama = comment,
            Kaynak = "Mobil",
            YeniUrunMu = isNewProduct
        };
        dbContext.SayimHareketleri.Add(movement);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is MySqlException { Number: 1062 })
        {
            dbContext.Entry(movement).State = EntityState.Detached;
            existing = await dbContext.SayimHareketleri
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.MobileRequestId == uuid, cancellationToken);
            if (existing == null)
                throw;

            return ExistingResult(existing, countId, barcode, productName, request.Quantity, comment);
        }

        return new MobileMovementSaveResult(
            MobileMovementSaveStatus.Created,
            ToResponse(movement));
    }

    private static MobileMovementSaveResult ExistingResult(
        SayimHareketi existing,
        int countId,
        string barcode,
        string productName,
        decimal quantity,
        string? comment)
    {
        var sameRequest = existing.SayimId == countId
            && string.Equals(existing.Barkod, barcode, StringComparison.Ordinal)
            && string.Equals(existing.UrunAdi, productName, StringComparison.Ordinal)
            && existing.Miktar == quantity
            && string.Equals(existing.Aciklama, comment, StringComparison.Ordinal);
        return sameRequest
            ? new MobileMovementSaveResult(MobileMovementSaveStatus.Duplicate, ToResponse(existing))
            : new MobileMovementSaveResult(MobileMovementSaveStatus.IdempotencyConflict, null);
    }

    private static MobileMovementResponse ToResponse(SayimHareketi movement) =>
        new(
            movement.Id,
            movement.SayimId,
            movement.MobileRequestId,
            movement.Barkod,
            movement.UrunAdi,
            movement.Miktar,
            movement.KullaniciAdi,
            AsUtcOffset(movement.Zaman),
            movement.Aciklama,
            movement.Kaynak,
            movement.YeniUrunMu);

    private static MobileMovementResponse ToResponse(MovementRow movement) =>
        new(
            movement.Id,
            movement.SayimId,
            movement.MobileRequestId,
            movement.Barkod,
            movement.UrunAdi,
            movement.Miktar,
            movement.KullaniciAdi,
            AsUtcOffset(movement.Zaman),
            movement.Aciklama,
            movement.Kaynak,
            movement.YeniUrunMu);

    private sealed record MovementRow(
        int Id,
        int SayimId,
        Guid? MobileRequestId,
        string Barkod,
        string UrunAdi,
        decimal Miktar,
        string KullaniciAdi,
        DateTime Zaman,
        string? Aciklama,
        string Kaynak,
        bool YeniUrunMu);

    private static DateTimeOffset AsUtcOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
