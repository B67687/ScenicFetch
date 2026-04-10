using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace ScenicFetch.Providers;

public static class ScenicFetchHttp
{
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) ScenicFetch/1.0 (+https://github.com/B67687/ScenicFetch)";

    public static HttpClient CreateDefaultClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            ServerCertificateCustomValidationCallback = ValidateCertificate,
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(45),
        };
        client.DefaultRequestHeaders.UserAgent.Add(ProductInfoHeaderValue.Parse(UserAgent));
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
        return client;
    }

    private static bool ValidateCertificate(
        HttpRequestMessage requestMessage,
        X509Certificate2? _,
        X509Chain? _,
        SslPolicyErrors sslPolicyErrors)
    {
        if (sslPolicyErrors == SslPolicyErrors.None)
        {
            return true;
        }

        return string.Equals(
            requestMessage.RequestUri?.Host,
            "sylvan.apple.com",
            StringComparison.OrdinalIgnoreCase);
    }
}
