using System.Net;
using Xunit;

namespace Aida.Web.Tests;

public sealed class AgendaPersonWebTests
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

    private static async Task AgendarCitaAsync(HttpClient client, string dia, string name, string phone)
    {
        var getRes = await client.GetAsync($"/agenda?dia={dia}&nueva=control");
        var token = ServerHarness.ExtractAntiforgeryToken(await getRes.Content.ReadAsStringAsync());

        var postRes = await client.PostAsync($"/agenda?dia={dia}&nueva=control", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-action",
            ["Command.Action"] = "agendar",
            ["Command.Client"] = "new",
            ["Command.Service"] = "control",
            ["Command.Name"] = name,
            ["Command.Phone"] = phone,
            ["Command.Slot"] = $"consultorio_1|{dia}T08:00:00-05:00",
            ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, postRes.StatusCode);
    }

    [Fact]
    public async Task Ficha_muestra_documento_y_editar_contacto_guarda_cuatro_campos()
    {
        await using var harness = await ServerHarness.CreateAsync();
        var adminClient = await CreateFirstAdminAsync(harness);
        await CreateUserAsync(harness, adminClient, "recep1", "Recepcionista Uno", "recepcion", "contrasena123");

        var recepCookies = new CookieContainer();
        using var recepClient = harness.CreateClient(recepCookies);
        await LoginAsync(recepClient, "recep1", "contrasena123");

        await AgendarCitaAsync(recepClient, "2026-11-16", "Ana Pérez", "3005550142");

        var getRes = await recepClient.GetAsync("/agenda?dia=2026-11-16&contacto=1");
        var token = ServerHarness.ExtractAntiforgeryToken(await getRes.Content.ReadAsStringAsync());

        var postRes = await recepClient.PostAsync("/agenda?dia=2026-11-16&contacto=1", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-action",
            ["Command.Action"] = "editar_contacto",
            ["Command.Client"] = "1",
            ["Command.Name"] = "Ana Pérez",
            ["Command.Phone"] = "3005550142",
            ["Command.DocumentType"] = "CC",
            ["Command.DocumentNumber"] = "12345678",
            ["Command.BirthDate"] = "1990-05-10",
            ["Command.Sex"] = "M",
            ["__RequestVerificationToken"] = token,
        }));

        Assert.Equal(HttpStatusCode.Redirect, postRes.StatusCode);
        Assert.Equal("/agenda?dia=2026-11-16&ficha=1", Location(postRes));

        var fichaRes = await recepClient.GetAsync(Location(postRes));
        var fichaHtml = WebUtility.HtmlDecode(await fichaRes.Content.ReadAsStringAsync());
        Assert.Contains("12345678", fichaHtml);
        Assert.Contains("1990-05-10", fichaHtml);
        Assert.Contains("Mujer", fichaHtml);
    }

    [Fact]
    public async Task Nueva_cita_manda_Command_Client_id_desde_ficha_y_new_para_nueva()
    {
        await using var harness = await ServerHarness.CreateAsync();
        var adminClient = await CreateFirstAdminAsync(harness);
        await CreateUserAsync(harness, adminClient, "recep1", "Recepcionista Uno", "recepcion", "contrasena123");

        var recepCookies = new CookieContainer();
        using var recepClient = harness.CreateClient(recepCookies);
        await LoginAsync(recepClient, "recep1", "contrasena123");

        await AgendarCitaAsync(recepClient, "2026-11-16", "Ana Pérez", "3005550142");

        // Con contacto prellenado -> Command.Client = 1
        var resFicha = await recepClient.GetAsync("/agenda?dia=2026-11-16&nueva=control&contacto=1");
        var htmlFicha = WebUtility.HtmlDecode(await resFicha.Content.ReadAsStringAsync());
        Assert.Contains("name=\"Command.Client\" value=\"1\"", htmlFicha);

        // Sin contacto prellenado -> Command.Client = new
        var resNueva = await recepClient.GetAsync("/agenda?dia=2026-11-16&nueva=control");
        var htmlNueva = WebUtility.HtmlDecode(await resNueva.Content.ReadAsStringAsync());
        Assert.Contains("name=\"Command.Client\" value=\"new\"", htmlNueva);
    }
}
