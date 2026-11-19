using System.Net;
using Aida.Platform.Identity;
using Aida.Platform.Packs;
using Xunit;

namespace Aida.Web.Tests;

public sealed class AgendaWebTests
{
    private static string LocationPath(HttpResponseMessage response) =>
        response.Headers.Location?.IsAbsoluteUri == true
            ? response.Headers.Location.AbsolutePath
            : response.Headers.Location?.OriginalString.Split('?')[0] ?? string.Empty;

    private static string Location(HttpResponseMessage response) =>
        response.Headers.Location?.IsAbsoluteUri == true
            ? response.Headers.Location.PathAndQuery
            : response.Headers.Location?.ToString() ?? string.Empty;

    private static async Task<HttpClient> CreateFirstAdminAsync(ServerHarness harness)
    {
        var cookies = new CookieContainer();
        var client = harness.CreateClient(cookies);

        var getRes = await client.GetAsync("/primer-usuario");
        var token = ServerHarness.ExtractAntiforgeryToken(await getRes.Content.ReadAsStringAsync());

        var postContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "first",
            ["Form.UserName"] = "admin",
            ["Form.DisplayName"] = "Ana Pérez",
            ["Form.Password"] = "contrasena123",
            ["Form.Password2"] = "contrasena123",
            ["__RequestVerificationToken"] = token,
        });

        var postRes = await client.PostAsync("/primer-usuario", postContent);
        Assert.Equal(HttpStatusCode.Redirect, postRes.StatusCode);
        return client;
    }

    private static async Task CreateUserAsync(ServerHarness harness, HttpClient adminClient, string userName, string displayName, string roleId, string password)
    {
        var getRes = await adminClient.GetAsync("/usuarios");
        var token = ServerHarness.ExtractAntiforgeryToken(await getRes.Content.ReadAsStringAsync());

        var postContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "create",
            ["NewUser.UserName"] = userName,
            ["NewUser.DisplayName"] = displayName,
            ["NewUser.RoleId"] = roleId,
            ["NewUser.Password"] = password,
            ["__RequestVerificationToken"] = token,
        });

        var postRes = await adminClient.PostAsync("/usuarios", postContent);
        Assert.Equal(HttpStatusCode.OK, postRes.StatusCode);
    }

    private static async Task LoginAsync(HttpClient client, string userName, string password)
    {
        var getRes = await client.GetAsync("/entrar");
        var token = ServerHarness.ExtractAntiforgeryToken(await getRes.Content.ReadAsStringAsync());

        var postContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "login",
            ["Form.UserName"] = userName,
            ["Form.Password"] = password,
            ["__RequestVerificationToken"] = token,
        });

        var postRes = await client.PostAsync("/entrar", postContent);
        Assert.Equal(HttpStatusCode.Redirect, postRes.StatusCode);
    }

    [Fact]
    public async Task La_agenda_pide_sesion()
    {
        await using var harness = await ServerHarness.CreateAsync();
        await CreateFirstAdminAsync(harness);
        using var client = harness.CreateClient();

        var res = await client.GetAsync("/agenda");
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Equal("/entrar", LocationPath(res));
    }

    [Fact]
    public async Task Recepcion_agenda_en_un_hueco_y_la_ve()
    {
        await using var harness = await ServerHarness.CreateAsync();
        var adminClient = await CreateFirstAdminAsync(harness);
        await CreateUserAsync(harness, adminClient, "recep1", "Recepcionista Uno", "recepcion", "contrasena123");

        var recepCookies = new CookieContainer();
        using var recepClient = harness.CreateClient(recepCookies);
        await LoginAsync(recepClient, "recep1", "contrasena123");

        // GET /agenda?dia=2026-11-16&nueva=control -> 200 con botones Command.Slot
        var getRes = await recepClient.GetAsync("/agenda?dia=2026-11-16&nueva=control");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var getHtml = await getRes.Content.ReadAsStringAsync();
        Assert.Contains("name=\"Command.Slot\"", getHtml);

        var token = ServerHarness.ExtractAntiforgeryToken(getHtml);

        // POST agendar
        var postContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-action",
            ["Command.Action"] = "agendar",
            ["Command.Service"] = "control",
            ["Command.Name"] = "Ana Pérez",
            ["Command.Phone"] = "300 555 0142",
            ["Command.Slot"] = "consultorio_1|2026-11-16T08:00:00-05:00",
            ["__RequestVerificationToken"] = token,
        });

        var postRes = await recepClient.PostAsync("/agenda?dia=2026-11-16&nueva=control", postContent);
        Assert.Equal(HttpStatusCode.Redirect, postRes.StatusCode);
        Assert.Equal("/agenda?dia=2026-11-16&cita=1", Location(postRes));

        // La rejilla tiene el nombre
        var viewRes = await recepClient.GetAsync(Location(postRes));
        Assert.Equal(HttpStatusCode.OK, viewRes.StatusCode);
        var viewHtml = WebUtility.HtmlDecode(await viewRes.Content.ReadAsStringAsync());
        Assert.Contains("Ana Pérez", viewHtml);

        // La auditoría tiene agenda_changed con el actor de recepción y Verify() vacío
        var lines = harness.Audit.Lines();
        var lastLine = lines[^1];
        using var doc = System.Text.Json.JsonDocument.Parse(lastLine);
        var root = doc.RootElement;
        Assert.Equal("agenda_changed", root.GetProperty("kind").GetString());

        var recepUser = harness.Users.List().First(u => u.UserName == "recep1");
        Assert.Equal(recepUser.Actor.Id, root.GetProperty("actor").GetString());
        Assert.Empty(harness.Audit.Verify());
    }

    [Fact]
    public async Task Un_rechazo_se_explica_y_no_cambia_nada()
    {
        await using var harness = await ServerHarness.CreateAsync();
        var adminClient = await CreateFirstAdminAsync(harness);
        await CreateUserAsync(harness, adminClient, "recep1", "Recepcionista Uno", "recepcion", "contrasena123");

        var recepCookies = new CookieContainer();
        using var recepClient = harness.CreateClient(recepCookies);
        await LoginAsync(recepClient, "recep1", "contrasena123");

        // 1. Agendamos el hueco por primera vez
        var getRes1 = await recepClient.GetAsync("/agenda?dia=2026-11-16&nueva=control");
        var token1 = ServerHarness.ExtractAntiforgeryToken(await getRes1.Content.ReadAsStringAsync());

        var postContent1 = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-action",
            ["Command.Action"] = "agendar",
            ["Command.Service"] = "control",
            ["Command.Name"] = "Ana Pérez",
            ["Command.Phone"] = "300 555 0142",
            ["Command.Slot"] = "consultorio_1|2026-11-16T08:00:00-05:00",
            ["__RequestVerificationToken"] = token1,
        });

        var postRes1 = await recepClient.PostAsync("/agenda?dia=2026-11-16&nueva=control", postContent1);
        Assert.Equal(HttpStatusCode.Redirect, postRes1.StatusCode);

        var auditCountBefore = harness.Audit.Lines().Count;

        // 2. Intentamos agendar el mismo hueco por segunda vez
        var getRes2 = await recepClient.GetAsync("/agenda?dia=2026-11-16&nueva=control");
        var token2 = ServerHarness.ExtractAntiforgeryToken(await getRes2.Content.ReadAsStringAsync());

        var postContent2 = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-action",
            ["Command.Action"] = "agendar",
            ["Command.Service"] = "control",
            ["Command.Name"] = "Beto Gómez",
            ["Command.Phone"] = "3105550000",
            ["Command.Slot"] = "consultorio_1|2026-11-16T08:00:00-05:00",
            ["__RequestVerificationToken"] = token2,
        });

        var postRes2 = await recepClient.PostAsync("/agenda?dia=2026-11-16&nueva=control", postContent2);
        // Debe responder 200 con el mensaje de rechazo
        Assert.Equal(HttpStatusCode.OK, postRes2.StatusCode);
        var html2 = WebUtility.HtmlDecode(await postRes2.Content.ReadAsStringAsync());
        Assert.Contains("Ese hueco ya está ocupado", html2);

        // No hay nuevo hecho de auditoría
        Assert.Equal(auditCountBefore, harness.Audit.Lines().Count);
    }

    [Fact]
    public async Task Caja_ve_pero_no_cambia()
    {
        await using var harness = await ServerHarness.CreateAsync();
        var adminClient = await CreateFirstAdminAsync(harness);
        await CreateUserAsync(harness, adminClient, "recep1", "Recepcionista Uno", "recepcion", "contrasena123");
        await CreateUserAsync(harness, adminClient, "caja1", "Cajero Uno", "caja", "contrasena123");

        // 1. Recepción agenda una cita
        var recepCookies = new CookieContainer();
        using var recepClient = harness.CreateClient(recepCookies);
        await LoginAsync(recepClient, "recep1", "contrasena123");

        var getResRecep = await recepClient.GetAsync("/agenda?dia=2026-11-16&nueva=control");
        var tokenRecep = ServerHarness.ExtractAntiforgeryToken(await getResRecep.Content.ReadAsStringAsync());
        await recepClient.PostAsync("/agenda?dia=2026-11-16&nueva=control", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-action",
            ["Command.Action"] = "agendar",
            ["Command.Service"] = "control",
            ["Command.Name"] = "Ana Pérez",
            ["Command.Phone"] = "300 555 0142",
            ["Command.Slot"] = "consultorio_1|2026-11-16T08:00:00-05:00",
            ["__RequestVerificationToken"] = tokenRecep,
        }));

        // 2. Caja entra y consulta la cita
        var cajaCookies = new CookieContainer();
        using var cajaClient = harness.CreateClient(cajaCookies);
        await LoginAsync(cajaClient, "caja1", "contrasena123");

        var getResCaja = await cajaClient.GetAsync("/agenda?dia=2026-11-16&cita=1");
        Assert.Equal(HttpStatusCode.OK, getResCaja.StatusCode);
        var htmlCaja = await getResCaja.Content.ReadAsStringAsync();

        // Como caja: el panel no tiene Command.Slot ni botones de acción
        Assert.DoesNotContain("name=\"Command.Slot\"", htmlCaja);
        Assert.DoesNotContain("<button type=\"submit\"", htmlCaja);

        var tokenCaja = ServerHarness.ExtractAntiforgeryToken(htmlCaja);

        var auditCountBefore = harness.Audit.Lines().Count;

        // POST forzado de cancelar -> 403 Forbidden
        var forcedPost = await cajaClient.PostAsync("/agenda?dia=2026-11-16&cita=1", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-action",
            ["Command.Action"] = "cancelar",
            ["Command.Appointment"] = "1",
            ["__RequestVerificationToken"] = tokenCaja,
        }));
        Assert.Equal(HttpStatusCode.Forbidden, forcedPost.StatusCode);

        // La cita sigue booked y no hay línea agenda_changed nueva
        var checkRes = await recepClient.GetAsync("/agenda?dia=2026-11-16&cita=1");
        var checkHtml = await checkRes.Content.ReadAsStringAsync();
        Assert.Contains("Agendada", checkHtml);
        Assert.Equal(auditCountBefore, harness.Audit.Lines().Count);
    }

    [Fact]
    public async Task Reprogramar_y_cancelar_desde_el_panel()
    {
        await using var harness = await ServerHarness.CreateAsync();
        var adminClient = await CreateFirstAdminAsync(harness);
        await CreateUserAsync(harness, adminClient, "recep1", "Recepcionista Uno", "recepcion", "contrasena123");

        var recepCookies = new CookieContainer();
        using var recepClient = harness.CreateClient(recepCookies);
        await LoginAsync(recepClient, "recep1", "contrasena123");

        // 1. Agendar
        var get1 = await recepClient.GetAsync("/agenda?dia=2026-11-16&nueva=control");
        var token1 = ServerHarness.ExtractAntiforgeryToken(await get1.Content.ReadAsStringAsync());
        await recepClient.PostAsync("/agenda?dia=2026-11-16&nueva=control", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-action",
            ["Command.Action"] = "agendar",
            ["Command.Service"] = "control",
            ["Command.Name"] = "Ana Pérez",
            ["Command.Phone"] = "300 555 0142",
            ["Command.Slot"] = "consultorio_1|2026-11-16T08:00:00-05:00",
            ["__RequestVerificationToken"] = token1,
        }));

        // 2. Mover: GET /agenda?dia=2026-11-16&mover=1 -> 200, POST con nuevo hueco -> 302
        var getMover = await recepClient.GetAsync("/agenda?dia=2026-11-16&mover=1");
        Assert.Equal(HttpStatusCode.OK, getMover.StatusCode);
        var tokenMover = ServerHarness.ExtractAntiforgeryToken(await getMover.Content.ReadAsStringAsync());

        var postMover = await recepClient.PostAsync("/agenda?dia=2026-11-16&mover=1", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-action",
            ["Command.Action"] = "reprogramar",
            ["Command.Appointment"] = "1",
            ["Command.Slot"] = "consultorio_1|2026-11-16T08:30:00-05:00",
            ["__RequestVerificationToken"] = tokenMover,
        }));
        Assert.Equal(HttpStatusCode.Redirect, postMover.StatusCode);
        Assert.Equal("/agenda?dia=2026-11-16&cita=1", Location(postMover));

        // 3. Cancelar: GET /agenda?dia=2026-11-16&cancelar=1 -> 200, POST con cancel_yes -> 302
        var getCancel = await recepClient.GetAsync("/agenda?dia=2026-11-16&cancelar=1");
        Assert.Equal(HttpStatusCode.OK, getCancel.StatusCode);
        var tokenCancel = ServerHarness.ExtractAntiforgeryToken(await getCancel.Content.ReadAsStringAsync());

        var postCancel = await recepClient.PostAsync("/agenda?dia=2026-11-16&cancelar=1", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-action",
            ["Command.Action"] = "cancelar",
            ["Command.Appointment"] = "1",
            ["__RequestVerificationToken"] = tokenCancel,
        }));
        Assert.Equal(HttpStatusCode.Redirect, postCancel.StatusCode);
        Assert.Equal("/agenda?dia=2026-11-16&cita=1", Location(postCancel));

        // La cita sale cancelada
        var viewRes = await recepClient.GetAsync("/agenda?dia=2026-11-16&cita=1");
        Assert.Equal(HttpStatusCode.OK, viewRes.StatusCode);
        var viewHtml = await viewRes.Content.ReadAsStringAsync();
        Assert.Contains("Cancelada", viewHtml);
    }

    [Fact]
    public async Task En_produccion_fuera_de_windows_la_agenda_no_abre()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        // Un arnés SIN la línea del protector (withDevelopmentKeyProtector: false)
        await using var harness = await ServerHarness.CreateAsync(withDevelopmentKeyProtector: false);
        var adminClient = await CreateFirstAdminAsync(harness);

        // / responde 200
        var homeRes = await adminClient.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, homeRes.StatusCode);

        // /agenda da 500 (PlatformNotSupportedException al intentar resolver IKeyProtector)
        var agendaRes = await adminClient.GetAsync("/agenda");
        Assert.Equal(HttpStatusCode.InternalServerError, agendaRes.StatusCode);

        // No se creó el archivo machine.key
        var machineKeyPath = Path.Combine(harness.TempDir, "data", "keys", "machine.key");
        Assert.False(File.Exists(machineKeyPath));
    }
}
