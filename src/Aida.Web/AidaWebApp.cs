using System.Security.Claims;
using Aida.Arms.Agenda;
using Aida.Arms.Agenda.Assistant;
using Aida.Arms.Agenda.Desk;
using Aida.Arms.Agenda.Setup;
using Aida.Arms.Agenda.Wording;
using Aida.Cognition.Understanding;
using Aida.Engine.Ontology;
using Aida.Platform.Assistant;
using Aida.Platform.Audit;
using Aida.Platform.Identity;
using Aida.Platform.Keys;
using Aida.Platform.Paths;
using Aida.Platform.Roles;
using Aida.Platform.Sessions;
using Aida.Platform.Snapshots;
using Aida.Platform.Storage;
using Aida.Platform.Wording;
using Aida.Web.Components;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Aida.Web;

/// <summary>Arma el servidor: servicios, autenticación y tubería.</summary>
/// <remarks>
/// Fuera de <c>Program.cs</c> para que los tests levanten el mismo servidor, con su certificado y sus carpetas
/// (lección 11). Solo HTTPS: todo punto de escucha lleva TLS, así que una petición en claro no recibe ninguna página
/// (ADR 0020). El certificado lo da la configuración de Kestrel; en desarrollo, el de <c>dotnet dev-certs</c>.
/// </remarks>
public static class AidaWebApp
{
    /// <summary>El nombre de la cookie de sesión.</summary>
    public const string CookieName = "aida";

    /// <summary>El tipo de la reclamación que lleva el identificador de la sesión del puesto.</summary>
    public const string SessionClaim = "aida_session";

    /// <summary>Construye la aplicación sin arrancarla.</summary>
    /// <param name="args">Los argumentos de la línea de órdenes.</param>
    /// <param name="configure">Lo que un test cambia antes de construir; <see langword="null"/> en producción.</param>
    /// <returns>La aplicación.</returns>
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ApplicationName = typeof(AidaWebApp).Assembly.GetName().Name });
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.ConfigureEndpointDefaults(listen => listen.UseHttps()));
        var paths = ResolvePaths(builder);
        builder.Services.AddSingleton(paths);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(_ => new PlatformDatabase(paths.DatabaseFile));
        builder.Services.AddSingleton(_ => RoleCatalog.Load(paths.PlatformPackFile("roles.yaml"), AgendaDeclarations.Permissions));
        builder.Services.AddSingleton(_ => ShellWording.Load(paths.PlatformPackFile("ui.yaml")));
        builder.Services.AddSingleton(_ => new PasswordHasher(PasswordHasher.ServerIterations));
        builder.Services.AddSingleton(provider => new AuditLog(provider.GetRequiredService<PlatformDatabase>(), provider.GetRequiredService<TimeProvider>(), AgendaDeclarations.AuditKinds));
        builder.Services.AddSingleton<UserStore>();
        builder.Services.AddSingleton(provider => new SessionTracker(
            provider.GetRequiredService<UserStore>(),
            provider.GetRequiredService<AuditLog>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ShellWording>().IdleLock));
        var agendaPack = paths.ArmPackDirectory(AgendaDeclarations.ArmId);
        builder.Services.AddSingleton(_ => KeyProtectors.ForHost(builder.Environment.IsDevelopment()));
        builder.Services.AddSingleton(provider => MachineKey.OpenOrCreate(paths.MachineKeyFile, provider.GetRequiredService<IKeyProtector>()));
        builder.Services.AddSingleton(provider => new SnapshotWriter(provider.GetRequiredService<PlatformDatabase>(), provider.GetRequiredService<MachineKey>(), paths, provider.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton(provider => new SnapshotStager(paths, provider.GetRequiredService<IKeyProtector>(), new Dictionary<string, int>(StringComparer.Ordinal) { [AgendaDeclarations.ArmId] = AgendaDeclarations.SchemaVersion }));
        builder.Services.AddSingleton(provider => new RestoreNotice(paths, provider.GetRequiredService<AuditLog>()));
        builder.Services.AddSingleton(_ => AgendaManifest.Load(Path.Combine(agendaPack, "arm.yaml")));
        builder.Services.AddSingleton(_ => AgendaWording.Load(Path.Combine(agendaPack, "ui.yaml")));
        builder.Services.AddSingleton(_ => TimeVocabulary.Load(paths.PlatformPackFile("time.yaml")));
        builder.Services.AddSingleton(_ => IntentCatalog.Load(Path.Combine(agendaPack, "intents.yaml"), AgendaDeclarations.Intents));
        builder.Services.AddSingleton(provider => new AssistantInterpreter(provider.GetRequiredService<IntentCatalog>(), provider.GetRequiredService<TimeVocabulary>()));
        builder.Services.AddSingleton(provider => new AgendaAssistant(provider.GetRequiredService<AgendaDesk>(), provider.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton(provider =>
        {
            var manifest = provider.GetRequiredService<AgendaManifest>();
            return new AgendaDesk(
                provider.GetRequiredService<PlatformDatabase>(),
                provider.GetRequiredService<AuditLog>(),
                provider.GetRequiredService<RoleCatalog>(),
                provider.GetRequiredService<UserStore>(),
                AgendaSetupLoader.Load(paths.ConfigFile("agenda.yaml"), agendaPack, manifest.DefaultSector),
                manifest.Actions,
                Reversibility.Reversible,
                provider.GetRequiredService<MachineKey>(),
                provider.GetRequiredService<TimeProvider>());
        });
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(cookie =>
        {
            cookie.Cookie.Name = CookieName;
            cookie.Cookie.HttpOnly = true;
            cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            cookie.Cookie.SameSite = SameSiteMode.Strict;
            cookie.LoginPath = "/entrar";
            cookie.AccessDeniedPath = "/";
        });
        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(PlatformPermission.ManageUsers, policy => policy.RequireAssertion(context =>
                context.Resource is HttpContext http && SessionGate.CurrentUser(http) is { } user
                && http.RequestServices.GetRequiredService<RoleCatalog>().Allows(user.RoleId, PlatformPermission.ManageUsers)))
            .AddPolicy(PlatformPermission.CreateSnapshot, policy => policy.RequireAssertion(context =>
                context.Resource is HttpContext http && SessionGate.CurrentUser(http) is { } user
                && http.RequestServices.GetRequiredService<RoleCatalog>().Allows(user.RoleId, PlatformPermission.CreateSnapshot)));
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddRazorComponents();
        configure?.Invoke(builder);

        var app = builder.Build();
        var restore = SnapshotInstaller.ApplyPending(paths, app.Services.GetRequiredService<TimeProvider>());
        if (restore == PendingRestore.Installed)
        {
            app.Logger.LogWarning("Se aplicó una copia restaurada; lo que había quedó apartado en la carpeta de datos.");
        }
        else if (restore == PendingRestore.Discarded)
        {
            app.Logger.LogError("Había una copia preparada, pero la base ya tiene usuarios: se apartó sin aplicarla.");
        }

        if (!OperatingSystem.IsWindows() && app.Environment.IsDevelopment())
        {
            app.Logger.LogWarning("La clave del equipo se guarda sin proteger: solo vale para desarrollar fuera de Windows (ADR 0034).");
        }

        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
        app.UseAuthentication();
        app.UseMiddleware<SessionGate>();
        app.UseAuthorization();
        app.UseAntiforgery();
        app.MapStaticAssets();
        app.MapRazorComponents<App>();
        app.MapPost("/salir", async (HttpContext http, SessionTracker sessions) =>
        {
            if (http.User.FindFirstValue(SessionClaim) is { } session)
            {
                sessions.Close(session);
            }

            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/entrar");
        }).WithMetadata(new Microsoft.AspNetCore.Antiforgery.RequireAntiforgeryTokenAttribute());
        app.MapSnapshotEndpoints();
        return app;
    }

    /// <summary>Desde qué puesto llega una petición: la dirección del navegador.</summary>
    /// <param name="http">La petición.</param>
    /// <returns>La dirección, en IPv4 si llega como IPv6 mapeada.</returns>
    public static string Workstation(HttpContext http)
    {
        var address = http.Connection.RemoteIpAddress;
        return address is null ? "desconocido" : (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
    }

    private static ServerPaths ResolvePaths(WebApplicationBuilder builder)
    {
        var root = builder.Environment.ContentRootPath;
        var packs = builder.Configuration["Aida:PacksDirectory"] ?? Path.Combine(AppContext.BaseDirectory, "packs");
        var data = builder.Configuration["Aida:DataDirectory"]
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "AIDA");
        return new ServerPaths(Path.GetFullPath(packs, root), Path.GetFullPath(data, root));
    }
}
