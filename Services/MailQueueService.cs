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
        _logger.LogInformation($"Mail kuyruğa eklendi: {mail.To} (planlanan zaman: {mail.GonderimZamani?.ToString("dd.MM.yyyy HH:mm") ?? "hemen"})");
    }

    public int QueueCount => _queue.Count;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_queue.TryDequeue(out var mail))
            {
                var now = DateTime.Now;
                if (mail.GonderimZamani.HasValue && mail.GonderimZamani.Value > now)
                {
                    var delay = mail.GonderimZamani.Value - now;
                    _queue.Enqueue(mail);
                    var waitTime = delay > TimeSpan.FromMinutes(5) ? TimeSpan.FromMinutes(5) : delay;
                    if (waitTime > TimeSpan.Zero)
                    {
                        await Task.Delay(waitTime, stoppingToken);
                    }
                    continue;
                }

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
                    mail.RetryCount++;

                    if (mail.RetryCount >= 3)
                    {
                        await LogMailAsync(mail, "Başarısız (maks deneme aşıldı)", false, ex.Message);
                        _logger.LogError(ex, "Mail gönderim hatası, tekrar deneme limiti aşıldı. To: {To}", mail.To);
                        continue;
                    }

                    _queue.Enqueue(mail);
                    await LogMailAsync(mail, "Başarısız", false, ex.Message);
                    _logger.LogError(ex, "Mail gönderim hatası. Tekrar denenecek. To: {To}. RetryCount: {RetryCount}", mail.To, mail.RetryCount);
                    
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