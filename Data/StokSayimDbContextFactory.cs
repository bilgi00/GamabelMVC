using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace gamabelmvc.Data;

public sealed class StokSayimDbContextFactory : IDesignTimeDbContextFactory<StokSayimDbContext>
{
    public StokSayimDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connectionString = configuration.GetConnectionString("MyConnection")
            ?? throw new InvalidOperationException("MyConnection veritabanı bağlantısı tanımlı değil.");
        var serverVersion = ServerVersion.Parse(
            configuration["EntityFramework:MySqlServerVersion"] ?? "8.0.0-mysql");

        var optionsBuilder = new DbContextOptionsBuilder<StokSayimDbContext>()
            .UseMySql(connectionString, serverVersion);

        return new StokSayimDbContext(optionsBuilder.Options);
    }
}
