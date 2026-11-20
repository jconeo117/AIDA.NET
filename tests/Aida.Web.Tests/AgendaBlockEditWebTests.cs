using System.Net;
using Xunit;

namespace Aida.Web.Tests;

public sealed class AgendaBlockEditWebTests
{
    private static string Location(HttpResponseMessage response) =>
        response.Headers.Location?.IsAbsoluteUri == true
            ? response.Headers.Location.PathAndQuery
            : response.Headers.Location?.ToString() ?? string.Empty;

    private static async Task<HttpClient> CreateFirstAdminAsync(ServerHarness harness)
    {
        var client = harness.CreateClient(new CookieContainer());
        var getRes = await client.GetAsync("/primer-usuario");
        var token = ServerHarness.ExtractAntiforgeryToken(await getRes.Content.ReadAsStringAsync());

        var postContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "first",
            ["Form.UserName"] = "admin",
            ["Form.DisplayName"] = "Administradora",
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

        var postRes = await adminClient.PostAsync("/usuarios", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "create",
            ["NewUser.UserName"] = userName,
            ["NewUser.DisplayName"] = displayName,
            ["NewUser.RoleId"] = roleId,
            ["NewUser.Password"] = password,
            ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.OK, postRes.StatusCode);
    }

    private static async Task LoginAsync(HttpClient client, string userName, string password)
    {
        var getRes = await client.GetAsync("/entrar");
        var token = ServerHarness.ExtractAntiforgeryToken(await getRes.Content.ReadAsStringAsync());

        var postRes = await client.PostAsync("/entrar", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "login",
            ["Form.UserName"] = userName,
            ["Form.Password"] = password,
            ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, postRes.StatusCode);
    }

    [Fact]
    public async Task Recepcion_bloquea_y_rejilla_pinta_blocked()
    {
        await using var harness = await ServerHarness.CreateAsync();
        var adminClient = await CreateFirstAdminAsync(harness);
        await CreateUserAsync(harness, adminClient, "recep1", "Recepcionista Uno", "recepcion", "contrasena123");

        var recepCookies = new CookieContainer();
        using var recepClient = harness.CreateClient(recepCookies);
        await LoginAsync(recepClient, "recep1", "contrasena123");

        var getRes = await recepClient.GetAsync("/agenda?dia=2026-11-16&bloquear=1");
        var token = ServerHarness.ExtractAntiforgeryToken(await getRes.Content.ReadAsStringAsync());

        var postContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-action",
            ["Command.Action"] = "bloquear",
            ["Command.Resource"] = "consultorio_1",
            ["Command.From"] = "2026-11-16T08:00",
            ["Command.Until"] = "2026-11-16T10:00",
            ["Command.Reason"] = "Congreso médico",
            ["__RequestVerificationToken"] = token,
        });

        var postRes = await recepClient.PostAsync("/agenda?dia=2026-11-16&bloquear=1", postContent);
        Assert.Equal(HttpStatusCode.Redirect, postRes.StatusCode);
        Assert.Equal("/agenda?dia=2026-11-16&bloqueo=1", Location(postRes));

        // Seguir redirección y verificar rejilla
        var viewRes = await recepClient.GetAsync(Location(postRes));
        Assert.Equal(HttpStatusCode.OK, viewRes.StatusCode);
        var viewHtml = WebUtility.HtmlDecode(await viewRes.Content.ReadAsStringAsync());
        Assert.Contains("class=\"blocked\"", viewHtml);
        Assert.Contains("Congreso médico", viewHtml);
    }

    [Fact]
    public async Task Bloquear_con_cita_dentro_muestra_conflicts()
    {
        await using var harness = await ServerHarness.CreateAsync();
        var adminClient = await CreateFirstAdminAsync(harness);
        await CreateUserAsync(harness, adminClient, "recep1", "Recepcionista Uno", "recepcion", "contrasena123");

        var recepCookies = new CookieContainer();
        using var recepClient = harness.CreateClient(recepCookies);
        await LoginAsync(recepClient, "recep1", "contrasena123");

        // 1. Agendar cita a las 08:00
        var getRes1 = await recepClient.GetAsync("/agenda?dia=2026-11-16&nueva=control");
        var token1 = ServerHarness.ExtractAntiforgeryToken(await getRes1.Content.ReadAsStringAsync());
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

        // 2. Intentar bloquear ese tramo
        var getRes2 = await recepClient.GetAsync("/agenda?dia=2026-11-16&bloquear=1");
        var token2 = ServerHarness.ExtractAntiforgeryToken(await getRes2.Content.ReadAsStringAsync());

        var postRes2 = await recepClient.PostAsync("/agenda?dia=2026-11-16&bloquear=1", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-action",
            ["Command.Action"] = "bloquear",
            ["Command.Resource"] = "consultorio_1",
            ["Command.From"] = "2026-11-16T08:00",
            ["Command.Until"] = "2026-11-16T09:00",
            ["Command.Reason"] = "Reunión",
            ["__RequestVerificationToken"] = token2,
        }));

        Assert.Equal(HttpStatusCode.OK, postRes2.StatusCode);
        var html = WebUtility.HtmlDecode(await postRes2.Content.ReadAsStringAsync());
        Assert.Contains("Hay citas agendadas dentro de ese horario", html);
        Assert.Contains("Citas dentro del bloqueo:", html);
        Assert.Contains("Ana Pérez", html);
    }

    [Fact]
    public async Task Desbloquear_redirige_al_dia_y_libera_hueco()
    {
        await using var harness = await ServerHarness.CreateAsync();
        var adminClient = await CreateFirstAdminAsync(harness);
        await CreateUserAsync(harness, adminClient, "recep1", "Recepcionista Uno", "recepcion", "contrasena123");

        var recepCookies = new CookieContainer();
        using var recepClient = harness.CreateClient(recepCookies);
        await LoginAsync(recepClient, "recep1", "contrasena123");

        // 1. Bloquear
        var getRes1 = await recepClient.GetAsync("/agenda?dia=2026-11-16&bloquear=1");
        var token1 = ServerHarness.ExtractAntiforgeryToken(await getRes1.Content.ReadAsStringAsync());
        await recepClient.PostAsync("/agenda?dia=2026-11-16&bloquear=1", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-action",
            ["Command.Action"] = "bloquear",
            ["Command.Resource"] = "consultorio_1",
            ["Command.From"] = "2026-11-16T08:00",
            ["Command.Until"] = "2026-11-16T09:00",
            ["Command.Reason"] = "Pintura",
            ["__RequestVerificationToken"] = token1,
        }));

        // 2. Desbloquear
        var getRes2 = await recepClient.GetAsync("/agenda?dia=2026-11-16&bloqueo=1");
        var token2 = ServerHarness.ExtractAntiforgeryToken(await getRes2.Content.ReadAsStringAsync());

        var postRes2 = await recepClient.PostAsync("/agenda?dia=2026-11-16&bloqueo=1", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-action",
            ["Command.Action"] = "desbloquear",
            ["Command.Block"] = "1",
            ["__RequestVerificationToken"] = token2,
        }));

        Assert.Equal(HttpStatusCode.Redirect, postRes2.StatusCode);
        Assert.Equal("/agenda?dia=2026-11-16", Location(postRes2));

        // 3. Verificar que el hueco vuelve a ofrecerse
        var getRes3 = await recepClient.GetAsync("/agenda?dia=2026-11-16&nueva=control");
        var html3 = await getRes3.Content.ReadAsStringAsync();
        Assert.Contains("consultorio_1|2026-11-16T08:00:00-05:00", html3);
    }

    [Fact]
    public async Task Editar_los_cuatro_campos_de_cita_y_rechazo_con_nombre_vacio()
    {
        await using var harness = await ServerHarness.CreateAsync();
        var adminClient = await CreateFirstAdminAsync(harness);
        await CreateUserAsync(harness, adminClient, "recep1", "Recepcionista Uno", "recepcion", "contrasena123");

        var recepCookies = new CookieContainer();
        using var recepClient = harness.CreateClient(recepCookies);
        await LoginAsync(recepClient, "recep1", "contrasena123");

        // Agendar cita a las 09:00
        var getRes1 = await recepClient.GetAsync("/agenda?dia=2026-11-16&nueva=control");
        var token1 = ServerHarness.ExtractAntiforgeryToken(await getRes1.Content.ReadAsStringAsync());
        await recepClient.PostAsync("/agenda?dia=2026-11-16&nueva=control", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-action",
            ["Command.Action"] = "agendar",
            ["Command.Service"] = "control",
            ["Command.Name"] = "Ana Pérez",
            ["Command.Phone"] = "300 555 0142",
            ["Command.Slot"] = "consultorio_1|2026-11-16T09:00:00-05:00",
            ["__RequestVerificationToken"] = token1,
        }));

        // 1. Nombre vaciado da missing_client
        var getResEdit = await recepClient.GetAsync("/agenda?dia=2026-11-16&editar=1");
        var tokenEdit = ServerHarness.ExtractAntiforgeryToken(await getResEdit.Content.ReadAsStringAsync());

        var postEmptyName = await recepClient.PostAsync("/agenda?dia=2026-11-16&editar=1", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-action",
            ["Command.Action"] = "editar",
            ["Command.Appointment"] = "1",
            ["Command.Name"] = "   ",
            ["Command.Phone"] = "3005550142",
            ["Command.Service"] = "control",
            ["Command.Resource"] = "consultorio_1",
            ["__RequestVerificationToken"] = tokenEdit,
        }));
        Assert.Equal(HttpStatusCode.OK, postEmptyName.StatusCode);
        var emptyHtml = WebUtility.HtmlDecode(await postEmptyName.Content.ReadAsStringAsync());
        Assert.Contains("Falta el nombre o el teléfono", emptyHtml);

        // 2. Editar los cuatro campos con éxito (consultorio_2 abre a las 09:00 y ofrece procedimiento)
        var tokenEdit2 = ServerHarness.ExtractAntiforgeryToken(emptyHtml);
        var postOk = await recepClient.PostAsync("/agenda?dia=2026-11-16&editar=1", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-action",
            ["Command.Action"] = "editar",
            ["Command.Appointment"] = "1",
            ["Command.Name"] = "Carlos Gómez",
            ["Command.Phone"] = "3109998877",
            ["Command.Service"] = "procedimiento",
            ["Command.Resource"] = "consultorio_2",
            ["__RequestVerificationToken"] = tokenEdit2,
        }));
        Assert.Equal(HttpStatusCode.Redirect, postOk.StatusCode);
        Assert.Equal("/agenda?dia=2026-11-16&cita=1", Location(postOk));

        var viewRes = await recepClient.GetAsync(Location(postOk));
        var viewHtml = WebUtility.HtmlDecode(await viewRes.Content.ReadAsStringAsync());
        Assert.Contains("Carlos Gómez", viewHtml);
        Assert.Contains("3109998877", viewHtml);
    }

    [Fact]
    public async Task Profesional_no_ve_block_button_y_POST_recibe_403()
    {
        await using var harness = await ServerHarness.CreateAsync();
        var adminClient = await CreateFirstAdminAsync(harness);
        await CreateUserAsync(harness, adminClient, "doc1", "Doctora Uno", "profesional", "contrasena123");

        var docCookies = new CookieContainer();
        using var docClient = harness.CreateClient(docCookies);
        await LoginAsync(docClient, "doc1", "contrasena123");

        // 1. GET no contiene block_button
        var getRes = await docClient.GetAsync("/agenda?dia=2026-11-16");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var getHtml = WebUtility.HtmlDecode(await getRes.Content.ReadAsStringAsync());
        Assert.DoesNotContain("Bloquear horario", getHtml);
        Assert.DoesNotContain("bloquear=1", getHtml);

        var token = ServerHarness.ExtractAntiforgeryToken(getHtml);

        // 2. POST directo de bloquear recibe 403 Forbidden
        var postRes = await docClient.PostAsync("/agenda?dia=2026-11-16", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-action",
            ["Command.Action"] = "bloquear",
            ["Command.Resource"] = "consultorio_1",
            ["Command.From"] = "2026-11-16T08:00",
            ["Command.Until"] = "2026-11-16T10:00",
            ["__RequestVerificationToken"] = token,
        }));

        Assert.Equal(HttpStatusCode.Forbidden, postRes.StatusCode);
        var postHtml = WebUtility.HtmlDecode(await postRes.Content.ReadAsStringAsync());
        Assert.Contains("Su rol no puede cambiar la agenda", postHtml);
    }
}
