using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace ScenicFetch.Providers;

public static class ScenicFetchHttp
{
    private const string ProductName = "ScenicFetch";
    private const string ProductVersion = "1.0";
    private const string ProductComment = "(+https://github.com/B67687/ScenicFetch)";

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
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(ProductName, ProductVersion));
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(ProductComment));
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
        return client;
    }

    private static bool ValidateCertificate(
        HttpRequestMessage requestMessage,
        X509Certificate2? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        _ = certificate;
        _ = chain;

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
