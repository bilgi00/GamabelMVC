// Services/MailService.cs
using System.Net;
using System.Net.Mail;
using gamabelmvc.Models.PRS;

namespace gamabelmvc.Services;

public interface IMailService
{
    Task<bool> SendAsync(MailModel mail);
    Task<string> RenderTemplateAsync(string templateCode, Dictionary<string, object> data);
    Task LogMailAsync(int logId, string durum, string? hataMesaji = null);
}

public class MailService : IMailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<MailService> _logger;

    public MailService(IConfiguration configuration, ILogger<MailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendAsync(MailModel mail)
    {
        try
        {
            var smtpHost = _configuration["Smtp:Host"] ?? "smtp.gmail.com";
            var smtpPort = int.Parse(_configuration["Smtp:Port"] ?? "587");
            var smtpUser = _configuration["Smtp:Username"] ?? "";
            var smtpPass = _configuration["Smtp:Password"] ?? "";
            var fromEmail = _configuration["Smtp:FromEmail"] ?? smtpUser;
            var fromName = _configuration["Smtp:FromName"] ?? "GAMABEL YATIRIM LTD";

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(smtpUser, smtpPass)
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = mail.Konu,
                Body = mail.Body,
                IsBodyHtml = true
            };

            mailMessage.To.Add(mail.To);

            if (!string.IsNullOrEmpty(mail.CC))
                mailMessage.CC.Add(mail.CC);
            if (!string.IsNullOrEmpty(mail.Bcc))
                mailMessage.Bcc.Add(mail.Bcc);

            await client.SendMailAsync(mailMessage);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Mail gönderme hatası: {ex.Message}");
            throw;
        }
    }

    public async Task<string> RenderTemplateAsync(string templateCode, Dictionary<string, object> data)
    {
        // Şablonu veritabanından getir
        var template = await GetTemplateFromDbAsync(templateCode);
        if (template == null)
            throw new Exception($"Şablon bulunamadı: {templateCode}");

        var html = template.Icerik;
        foreach (var item in data)
        {
            var placeholder = $"{{{{{item.Key}}}}}";
            html = html.Replace(placeholder, item.Value?.ToString() ?? "");
        }

        return html;
    }

    private async Task<MailSablonu?> GetTemplateFromDbAsync(string templateCode)
    {
        // Veritabanından şablonu getir
        // Bu kısmı veritabanı bağlantınıza göre uyarlayın
        return null;
    }

    public async Task LogMailAsync(int logId, string durum, string? hataMesaji = null)
    {
        // Log kaydını güncelle
        // Veritabanı güncelleme kodu
    }
}

public class MailModel
{
    public string To { get; set; } = string.Empty;
    public string? CC { get; set; }
    public string? Bcc { get; set; }
    public string Konu { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int TalimatId { get; set; }
    public int FirmaId { get; set; }
}