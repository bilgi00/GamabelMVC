// Services/MailService.cs
using System.Net;
using System.Net.Mail;
using System.Text;
using MySqlConnector;
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
        var smtpHost = _configuration["Smtp:Host"];
        var smtpPortText = _configuration["Smtp:Port"];
        var smtpUser = _configuration["Smtp:Username"];
        var smtpPass = _configuration["Smtp:Password"];
        var fromName = _configuration["Smtp:FromName"] ?? "GAMABEL YATIRIM LTD";

        if (string.IsNullOrWhiteSpace(smtpHost) ||
            string.IsNullOrWhiteSpace(smtpUser) ||
            string.IsNullOrWhiteSpace(smtpPass))
        {
            throw new InvalidOperationException("SMTP ayarları eksik. appsettings.json içindeki Smtp:Host, Username ve Password alanlarını kontrol edin.");
        }

        var fromEmail = string.IsNullOrWhiteSpace(_configuration["Smtp:FromEmail"]) ? smtpUser : _configuration["Smtp:FromEmail"];
        if (string.IsNullOrWhiteSpace(fromEmail))
        {
            throw new InvalidOperationException("SMTP gönderici e-postası boş olamaz.");
        }

        if (string.IsNullOrWhiteSpace(mail.To))
        {
            throw new InvalidOperationException("Mail alıcısı boş olamaz.");
        }

        if (!int.TryParse(smtpPortText, out var smtpPort))
        {
            smtpPort = 587;
        }

        var tlsEnabled = bool.TryParse(_configuration["Smtp:EnableSsl"], out var parsedEnableSsl) && parsedEnableSsl;
        var candidatePorts = new[] { smtpPort, 465, 587, 25 }.Distinct().ToArray();
        Exception? lastException = null;

        foreach (var port in candidatePorts)
        {
            try
            {
                using var client = new SmtpClient(smtpHost, port)
                {
                    EnableSsl = tlsEnabled,
                    UseDefaultCredentials = false,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 30000,
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
            catch (SmtpException ex)
            {
                lastException = ex;
                _logger.LogError(ex,
                    "SMTP gönderim hatası. Host={Host}, Port={Port}, EnableSsl={EnableSsl}, From={From}, To={To}. Status={StatusCode}, Message={Message}",
                    smtpHost,
                    port,
                    tlsEnabled,
                    fromEmail,
                    mail.To,
                    ex.StatusCode,
                    ex.Message);
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogError(ex,
                    "Mail gönderme hatası. Host={Host}, Port={Port}, EnableSsl={EnableSsl}, From={From}, To={To}. Message={Message}",
                    smtpHost,
                    port,
                    tlsEnabled,
                    fromEmail,
                    mail.To,
                    ex.Message);
            }
        }

        if (lastException != null)
        {
            throw new InvalidOperationException(
                $"SMTP sunucusu red etti. Host={smtpHost}, Port={string.Join(",", candidatePorts)}, EnableSsl={tlsEnabled}, From={fromEmail}. Detay: {lastException.Message}",
                lastException);
        }

        throw new InvalidOperationException($"SMTP gönderim işlemi başarısız oldu. Host={smtpHost}, Port={string.Join(",", candidatePorts)}, EnableSsl={tlsEnabled}.");
    }

    public async Task<string> RenderTemplateAsync(string templateCode, Dictionary<string, object> data)
    {
        var normalizedCode = (templateCode ?? string.Empty).Trim();
        var template = await GetTemplateFromDbAsync(normalizedCode);
        var html = template?.Icerik ?? BuildDefaultTemplate(normalizedCode);

        foreach (var item in data)
        {
            var placeholder = $"{{{{{item.Key}}}}}";
            html = html.Replace(placeholder, item.Value?.ToString() ?? "");
        }

        return html;
    }

    private async Task<MailSablonu?> GetTemplateFromDbAsync(string templateCode)
    {
        if (string.IsNullOrWhiteSpace(templateCode))
            return null;

        var connString = _configuration.GetConnectionString("MyConnection");
        if (string.IsNullOrWhiteSpace(connString))
            return null;

        var candidateTables = new[]
        {
            "mail_sablonlari",
            "prs_mail_sablonlari",
            "mail_templates",
            "prs_mail_templates"
        };

        foreach (var tableName in candidateTables)
        {
            try
            {
                await using var connection = new MySqlConnection(connString);
                await connection.OpenAsync();

                var query = $@"
                    SELECT sablon_kodu, sablon_adi, konu, icerik
                    FROM {tableName}
                    WHERE aktif_mi = 1
                      AND (LOWER(sablon_kodu) = LOWER(@code) OR LOWER(sablon_adi) = LOWER(@code))
                    LIMIT 1";

                await using var cmd = new MySqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@code", templateCode);
                await using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new MailSablonu
                    {
                        SablonKodu = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                        SablonAdi = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        Konu = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        Icerik = reader.IsDBNull(3) ? string.Empty : reader.GetString(3)
                    };
                }
            }
            catch
            {
                // İlgili tablo yoksa veya kolonlar farklıysa sonraki olasılığa geç.
            }
        }

        return null;
    }

    private static string BuildDefaultTemplate(string templateCode)
    {
        var template = new StringBuilder();
        template.AppendLine("<html><body style='font-family: Arial, sans-serif; color: #1f2937; background: #f8fafc; padding: 24px;'>");
        template.AppendLine("<div style='max-width: 760px; margin: 0 auto; background: white; border: 1px solid #e2e8f0; border-radius: 12px; overflow: hidden;'>");
        template.AppendLine("<div style='background: linear-gradient(135deg, #1e3c72, #2a5298); color: white; padding: 20px 28px; font-weight: 700; font-size: 20px;'>Ödeme Talimatı</div>");
        template.AppendLine("<div style='padding: 24px;'>");
        template.AppendLine("<p><strong>Firma:</strong> {{FIRMA_ADI}}</p>");
        template.AppendLine("<p><strong>Talimat No:</strong> {{TALIMAT_NO}}</p>");
        template.AppendLine("<p><strong>Tarih:</strong> {{TARIH}}</p>");
        template.AppendLine("<p><strong>Banka:</strong> {{BANKA}}</p>");
        template.AppendLine("<p><strong>IBAN:</strong> {{IBAN}}</p>");
        template.AppendLine("<p><strong>Gönderim Tarihi:</strong> {{GONDERIM_TARIHI}}</p>");
        template.AppendLine("<hr />");
        template.AppendLine("<h3 style='margin-top: 16px;'>Fatura Detayları</h3>");
        template.AppendLine("{{TABLE_HTML}}");
        template.AppendLine("<div style='margin-top: 16px;'><strong>Toplam Fatura Sayısı:</strong> {{TOPLAM_ADET}}</div>");
        template.AppendLine("{{OZEL_MESAJ}}");
        template.AppendLine("</div>");
        template.AppendLine("</div>");
        template.AppendLine("</body></html>");
        return template.ToString();
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
    public DateTime? GonderimZamani { get; set; }
    public int RetryCount { get; set; }
}