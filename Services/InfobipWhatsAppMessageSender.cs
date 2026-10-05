using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace gamabelmvc.Services;

public interface IWhatsAppMessageSender
{
    Task SendAsync(string message, CancellationToken cancellationToken);
}

public sealed class InfobipWhatsAppMessageSender : IWhatsAppMessageSender
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public InfobipWhatsAppMessageSender(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task SendAsync(string message, CancellationToken cancellationToken)
    {
        var apiKey = GetRequiredSetting(_configuration, "Infobip:ApiKey");
        var sender = GetRequiredSetting(_configuration, "Infobip:WhatsAppSender");
        var recipient = GetRequiredSetting(_configuration, "Infobip:WhatsAppRecipient");
        var templateName = GetRequiredSetting(_configuration, "Infobip:WhatsAppTemplateName");
        var templateLanguage = GetRequiredSetting(_configuration, "Infobip:WhatsAppTemplateLanguage");
        var baseUrl = GetRequiredSetting(_configuration, "Infobip:BaseUrl").TrimEnd('/') + "/";

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri)
            || !Uri.TryCreate(baseUri, "whatsapp/1/message/template", out var endpoint))
            throw new InvalidOperationException("Infobip BaseUrl yapılandırması geçersiz.");

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("App", apiKey);
        request.Content = JsonContent.Create(new
        {
            messages = new[]
            {
                new
                {
                    from = sender,
                    to = recipient,
                    content = new
                    {
                        templateName,
                        templateData = new
                        {
                            body = new { placeholders = new[] { message } }
                        },
                        language = templateLanguage
                    }
                }
            }
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.IsSuccessStatusCode)
            return;

        var exception = new HttpRequestException(
            $"Infobip returned HTTP {(int)response.StatusCode}.",
            inner: null,
            response.StatusCode);
        exception.Data["InfobipResponseBody"] = responseBody;
        throw exception;
    }

    private static string GetRequiredSetting(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"'{key}' yapılandırması eksik.");

        return value;
    }
}
