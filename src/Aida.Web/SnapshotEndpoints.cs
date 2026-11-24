using System.Globalization;
using System.Security.Cryptography;
using Aida.Platform.Audit;
using Aida.Platform.Identity;
using Aida.Platform.Paths;
using Aida.Platform.Roles;
using Aida.Platform.Snapshots;
using Aida.Web.Components;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace Aida.Web;

/// <summary>Las dos puertas de la copia: hacerla desde Ajustes y restaurarla desde el primer arranque.</summary>
/// <remarks>
/// Son formularios HTML y no componentes: la descarga es un archivo, y la restauración termina cerrando AIDA. Cada
/// puerta comprueba su permiso o su condición aquí, no solo en la página (ADR 0036).
/// </remarks>
internal static class SnapshotEndpoints
{
    /// <summary>Registra las dos rutas de la copia.</summary>
    /// <param name="app">La aplicación.</param>
    internal static void MapSnapshotEndpoints(this WebApplication app)
    {
        app.MapPost("/ajustes/copia", CreateCopy).RequireAuthorization(PlatformPermission.CreateSnapshot);
        app.MapPost("/primer-usuario/restaurar", Restore)
            .AllowAnonymous()
            .WithMetadata(new RequestSizeLimitAttribute(SnapshotStager.MaxFileBytes))
            .WithFormOptions(multipartBodyLengthLimit: SnapshotStager.MaxFileBytes);
    }

    private static IResult CreateCopy(
        HttpContext http,
        [FromForm] string? passphrase,
        [FromForm] string? passphrase2,
        SnapshotWriter writer,
        AuditLog audit,
        ServerPaths paths,
        TimeProvider time)
    {
        if (!string.Equals(passphrase, passphrase2, StringComparison.Ordinal))
        {
            return Results.Redirect("/ajustes?error=mismatch");
        }

        if (SessionGate.CurrentUser(http) is not { } user)
        {
            return Results.StatusCode(403);
        }

        Directory.CreateDirectory(paths.TemporaryDirectory);
        var path = Path.Combine(paths.TemporaryDirectory, $"{RandomNumberGenerator.GetHexString(32, lowercase: true)}.aidacopia");
        var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
        try
        {
            var files = writer.Write(stream, passphrase ?? string.Empty);
            audit.Record(AuditKind.SnapshotCreated, user.Actor, AidaWebApp.Workstation(http), new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["files"] = files.ToString(CultureInfo.InvariantCulture),
            });
        }
        catch (SnapshotException exception)
        {
            stream.Dispose();
            return Results.Redirect($"/ajustes?error={exception.Code}");
        }
        catch
        {
            stream.Dispose();
            throw;
        }

        stream.Position = 0;
        var name = $"aida-{time.GetLocalNow().ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}.aidacopia";
        return Results.File(stream, "application/octet-stream", name);
    }

    private static IResult Restore(
        HttpContext http,
        [FromForm] IFormFile? file,
        [FromForm] string? passphrase,
        SnapshotStager stager,
        UserStore users,
        IHostApplicationLifetime lifetime)
    {
        if (!ServerOrigin.IsServerItself(http) || users.HasUsers())
        {
            return Results.StatusCode(403);
        }

        if (file is null || file.Length == 0)
        {
            return Results.Redirect("/primer-usuario?copia=nofile");
        }

        try
        {
            using var stream = file.OpenReadStream();
            stager.Stage(stream, passphrase ?? string.Empty);
        }
        catch (SnapshotException exception)
        {
            return Results.Redirect($"/primer-usuario?copia={exception.Code}");
        }

        http.Response.OnCompleted(() =>
        {
            lifetime.StopApplication();
            return Task.CompletedTask;
        });
        return new RazorComponentResult<RestoreDone>();
    }
}
