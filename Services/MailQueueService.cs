// Services/MailQueueService.cs
using System.Collections.Concurrent;

namespace gamabelmvc.Services;

public class MailQueueService : BackgroundService
{
    private readonly ConcurrentQueue<MailModel> _queue = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MailQueueService> _logger;

    public MailQueueService(IServiceScopeFactory scopeFactory, ILogger<MailQueueService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void Enqueue(MailModel mail)
    {
        _queue.Enqueue(mail);
        _logger.LogInformation($"Mail kuyruğa eklendi: {mail.To}");
    }

    public int QueueCount => _queue.Count;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_queue.TryDequeue(out var mail))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var mailService = scope.ServiceProvider.GetRequiredService<IMailService>();
                    
                    await mailService.SendAsync(mail);
                    await LogMailAsync(mail, "Gönderildi", true);
                    
                    _logger.LogInformation($"Mail gönderildi: {mail.To}");
                }
                catch (Exception ex)
                {
                    _queue.Enqueue(mail);
                    await LogMailAsync(mail, "Başarısız", false, ex.Message);
                    _logger.LogError($"Mail gönderim hatası: {ex.Message}");
                    
                    await Task.Delay(5000, stoppingToken);
                }
            }
            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task LogMailAsync(MailModel mail, string durum, bool basarili, string? hata = null)
    {
        // Veritabanına log kaydı ekle
        // Bu kısmı veritabanı bağlantınıza göre uyarlayın
    }
}