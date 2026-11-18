using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Aida.Platform.Audit;
using Aida.Platform.Identity;
using Aida.Platform.Keys;
using Aida.Platform.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Aida.Web.Tests;

internal sealed class ServerHarness : IAsyncDisposable
{
    public WebApplication App { get; }
    public string TempDir { get; }
    public string HttpsBaseUrl { get; }
    public string HttpBaseUrl { get; }
    public ManualTime Time { get; }
    public AuditLog Audit => App.Services.GetRequiredService<AuditLog>();
    public UserStore Users => App.Services.GetRequiredService<UserStore>();

    private ServerHarness(WebApplication app, string tempDir, string httpsBaseUrl, string httpBaseUrl, ManualTime time)
    {
        App = app;
        TempDir = tempDir;
        HttpsBaseUrl = httpsBaseUrl;
        HttpBaseUrl = httpBaseUrl;
        Time = time;
    }

    public static async Task<ServerHarness> CreateAsync(
        Action<WebApplicationBuilder>? configure = null,
        bool withDevelopmentKeyProtector = true)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "aida-web-test-" + Guid.NewGuid().ToString("n"));
        var dataDir = Path.Combine(tempDir, "data");
        Directory.CreateDirectory(dataDir);

        var repoRoot = FindRepoRoot();
        var packsDir = Path.Combine(repoRoot, "packs");

        var cert = CreateCertificate();
        var time = new ManualTime();

        var app = AidaWebApp.Build(
            [
                "--urls=http://127.0.0.1:0",
                $"--Aida:PacksDirectory={packsDir}",
                $"--Aida:DataDirectory={dataDir}",
                "--environment=Production",
            ],
            builder =>
            {
                builder.WebHost.ConfigureKestrel(k => k.ConfigureHttpsDefaults(h => h.ServerCertificate = cert));
                builder.Services.AddSingleton<TimeProvider>(time);
                builder.Services.AddSingleton(new PasswordHasher(10));
                if (withDevelopmentKeyProtector)
                {
                    builder.Services.AddSingleton<IKeyProtector>(new DevelopmentKeyProtector());
                }

                configure?.Invoke(builder);
            });

        await app.StartAsync();

        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()!.Addresses;
        var boundHttp = addresses.First();
        var boundHttps = boundHttp.Replace("http://", "https://");

        return new ServerHarness(app, tempDir, boundHttps, boundHttp, time);
    }

    public HttpClient CreateClient(CookieContainer? cookies = null)
    {
        var container = cookies ?? new CookieContainer();
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            CookieContainer = container,
            UseProxy = false,
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };
        return new HttpClient(handler) { BaseAddress = new Uri(HttpsBaseUrl) };
    }

    public static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(html, @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");
        if (!match.Success)
        {
            match = Regex.Match(html, @"value=""([^""]+)""[^>]*name=""__RequestVerificationToken""");
        }

        if (!match.Success)
        {
            throw new InvalidOperationException($"No se encontró __RequestVerificationToken en el HTML:\n{html}");
        }

        return match.Groups[1].Value;
    }

    public async ValueTask DisposeAsync()
    {
        await App.StopAsync();
        await App.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(TempDir))
            {
                Directory.Delete(TempDir, true);
            }
        }
        catch
        {
        }
    }

    private static X509Certificate2 CreateCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=127.0.0.1", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var pfx = cert.Export(X509ContentType.Pfx);
        return X509CertificateLoader.LoadPkcs12(pfx, null, X509KeyStorageFlags.Exportable);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aida.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }
}
