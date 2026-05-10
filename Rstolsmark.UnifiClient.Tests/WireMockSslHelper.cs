using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using WireMock.Server;
using WireMock.Settings;

namespace Rstolsmark.UnifiClient.Tests;

public static class WireMockSslHelper
{
    public static X509Certificate2 GenerateCrossPlatformCert(string domain)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={domain}", 
            rsa, 
            HashAlgorithmName.SHA256, 
            RSASignaturePadding.Pkcs1);

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName(domain);
        request.CertificateExtensions.Add(sanBuilder.Build());
        
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));

        var expireAt = DateTimeOffset.UtcNow.AddYears(1);
        
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), expireAt);

        return  X509CertificateLoader.LoadPkcs12(
            cert.Export(X509ContentType.Pfx), 
            null, 
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
    }
}