using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace gamabelmvc.Services;

/// <summary>
/// Kullanıcı ve sistem bilgilerini almak için servis
/// IP adresi, MAC adresi, bilgisayar adı vb.
/// </summary>
public interface ISystemInfoService
{
    /// <summary>
    /// İstemci IP adresini al
    /// </summary>
    string GetClientIpAddress(HttpContext httpContext);

    /// <summary>
    /// Bilgisayar adını al
    /// </summary>
    string GetComputerName();

    /// <summary>
    /// MAC adresini al (ağ adaptöründen)
    /// </summary>
    string GetMacAddress();

    /// <summary>
    /// User-Agent'dan işletim sistemini çıkar
    /// </summary>
    string GetOperatingSystem(string userAgent);

    /// <summary>
    /// User-Agent'dan tarayıcı bilgisini çıkar
    /// </summary>
    string GetBrowserInfo(string userAgent);
}

public class SystemInfoService : ISystemInfoService
{
    public string GetClientIpAddress(HttpContext httpContext)
    {
        try
        {
            // Proxy arkasında olabilir, o yüzden birkaç kaynağı kontrol et
            var ipAddress = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(ipAddress))
            {
                ipAddress = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
            }

            if (string.IsNullOrEmpty(ipAddress))
            {
                ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
            }

            // IPv6 döngü adresini IPv4'e çevir
            if (ipAddress == "::1")
            {
                ipAddress = "127.0.0.1";
            }

            return ipAddress ?? "Bilinmiyor";
        }
        catch
        {
            return "Bilinmiyor";
        }
    }

    public string GetComputerName()
    {
        try
        {
            return Environment.MachineName ?? "Bilinmiyor";
        }
        catch
        {
            return "Bilinmiyor";
        }
    }

    public string GetMacAddress()
    {
        try
        {
            var macAddress = "";
            var allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (var networkInterface in allNetworkInterfaces)
            {
                // Etkin ve çalışan adaptörleri seç
                if (networkInterface.OperationalStatus == OperationalStatus.Up 
                    && networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    var physicalAddress = networkInterface.GetPhysicalAddress();
                    if (physicalAddress.ToString() != "")
                    {
                        macAddress = physicalAddress.ToString();
                        break;
                    }
                }
            }

            return string.IsNullOrEmpty(macAddress) ? "Bilinmiyor" : macAddress;
        }
        catch
        {
            return "Bilinmiyor";
        }
    }

    public string GetOperatingSystem(string userAgent)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userAgent))
                return "Bilinmiyor";

            if (userAgent.Contains("Windows NT"))
            {
                if (userAgent.Contains("Windows NT 10.0")) return "Windows 10/11";
                if (userAgent.Contains("Windows NT 6.3")) return "Windows 8.1";
                if (userAgent.Contains("Windows NT 6.2")) return "Windows 8";
                if (userAgent.Contains("Windows NT 6.1")) return "Windows 7";
            }
            else if (userAgent.Contains("Mac OS X"))
                return "macOS";
            else if (userAgent.Contains("Linux"))
                return "Linux";
            else if (userAgent.Contains("iPad") || userAgent.Contains("iPhone"))
                return "iOS";
            else if (userAgent.Contains("Android"))
                return "Android";

            return "Bilinmiyor";
        }
        catch
        {
            return "Bilinmiyor";
        }
    }

    public string GetBrowserInfo(string userAgent)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userAgent))
                return "Bilinmiyor";

            if (userAgent.Contains("Edge"))
                return "Edge";
            else if (userAgent.Contains("Chrome"))
                return "Chrome";
            else if (userAgent.Contains("Safari") && !userAgent.Contains("Chrome"))
                return "Safari";
            else if (userAgent.Contains("Firefox"))
                return "Firefox";
            else if (userAgent.Contains("Trident"))
                return "Internet Explorer";

            return "Bilinmiyor";
        }
        catch
        {
            return "Bilinmiyor";
        }
    }
}
