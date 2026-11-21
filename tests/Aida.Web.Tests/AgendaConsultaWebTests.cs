using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace Aida.Web.Tests;

public sealed class AgendaConsultaWebTests
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

        var postContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-action",
            ["Command.Action"] = "agendar",
            ["Command.Service"] = "control",
            ["Command.Name"] = name,
            ["Command.Phone"] = phone,
            ["Command.Slot"] = $"consultorio_1|{dia}T08:00:00-05:00",
            ["__RequestVerificationToken"] = token,
        });

        var postRes = await client.PostAsync($"/agenda?dia={dia}&nueva=control", postContent);
        Assert.Equal(HttpStatusCode.Redirect, postRes.StatusCode);
    }

    [Fact]
    public async Task Busqueda_por_POST_responde_200_sin_redireccion_y_sin_consulta_en_la_url()
    {
        await using var harness = await ServerHarness.CreateAsync();
        var adminClient = await CreateFirstAdminAsync(harness);
        await CreateUserAsync(harness, adminClient, "recep1", "Recepcionista Uno", "recepcion", "contrasena123");

        var recepCookies = new CookieContainer();
        using var recepClient = harness.CreateClient(recepCookies);
        await LoginAsync(recepClient, "recep1", "contrasena123");

        await AgendarCitaAsync(recepClient, "2026-11-16", "Ana Pérez", "3005550142");

        var getRes = await recepClient.GetAsync("/agenda?dia=2026-11-16");
        var token = ServerHarness.ExtractAntiforgeryToken(await getRes.Content.ReadAsStringAsync());

        var postContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-search",
            ["Search.Query"] = "Ana",
            ["__RequestVerificationToken"] = token,
        });

        var postRes = await recepClient.PostAsync("/agenda?dia=2026-11-16", postContent);

        // 1. Responde 200 OK (sin redirección)
        Assert.Equal(HttpStatusCode.OK, postRes.StatusCode);
        Assert.Null(postRes.Headers.Location);

        // 2. La URL final no incluye la consulta buscada
        var uri = postRes.RequestMessage?.RequestUri?.ToString() ?? string.Empty;
        Assert.DoesNotContain("Search.Query", uri);
        Assert.DoesNotContain("Ana", uri);

        // 3. El HTML incluye enlace a la ficha del contacto
        var html = WebUtility.HtmlDecode(await postRes.Content.ReadAsStringAsync());
        Assert.Contains("ficha=1", html);
        Assert.Contains("Ana Pérez · 3005550142", html);
    }

    [Fact]
    public async Task Ficha_pinta_cifras_y_datos_del_contacto()
    {
        await using var harness = await ServerHarness.CreateAsync();
        var adminClient = await CreateFirstAdminAsync(harness);
        await CreateUserAsync(harness, adminClient, "recep1", "Recepcionista Uno", "recepcion", "contrasena123");

        var recepCookies = new CookieContainer();
        using var recepClient = harness.CreateClient(recepCookies);
        await LoginAsync(recepClient, "recep1", "contrasena123");

        await AgendarCitaAsync(recepClient, "2026-11-16", "Ana Pérez", "3005550142");

        var cardRes = await recepClient.GetAsync("/agenda?dia=2026-11-16&ficha=1");
        Assert.Equal(HttpStatusCode.OK, cardRes.StatusCode);

        var html = WebUtility.HtmlDecode(await cardRes.Content.ReadAsStringAsync());
        Assert.Contains("<h3>Ana Pérez</h3>", html);
        Assert.Contains("3005550142", html);
        Assert.Contains("<dl class=\"kv\">", html);
        Assert.Contains("<dd>1</dd>", html); // 1 próxima
    }

    [Fact]
    public async Task Editar_contacto_redirige_a_ficha()
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

        var postContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-action",
            ["Command.Action"] = "editar_contacto",
            ["Command.Client"] = "1",
            ["Command.Name"] = "Ana María Pérez",
            ["Command.Phone"] = "3005550142",
            ["__RequestVerificationToken"] = token,
        });

        var postRes = await recepClient.PostAsync("/agenda?dia=2026-11-16&contacto=1", postContent);

        Assert.Equal(HttpStatusCode.Redirect, postRes.StatusCode);
        Assert.Equal("/agenda?dia=2026-11-16&ficha=1", Location(postRes));
    }

    [Fact]
    public async Task Nueva_con_contacto_prellena_datos()
    {
        await using var harness = await ServerHarness.CreateAsync();
        var adminClient = await CreateFirstAdminAsync(harness);
        await CreateUserAsync(harness, adminClient, "recep1", "Recepcionista Uno", "recepcion", "contrasena123");

        var recepCookies = new CookieContainer();
        using var recepClient = harness.CreateClient(recepCookies);
        await LoginAsync(recepClient, "recep1", "contrasena123");

        await AgendarCitaAsync(recepClient, "2026-11-16", "Ana Pérez", "3005550142");

        // GET /agenda?dia=2026-11-16&nueva=control&contacto=1
        var newRes = await recepClient.GetAsync("/agenda?dia=2026-11-16&nueva=control&contacto=1");
        Assert.Equal(HttpStatusCode.OK, newRes.StatusCode);

        var html = WebUtility.HtmlDecode(await newRes.Content.ReadAsStringAsync());
        Assert.Contains("name=\"Command.Name\" value=\"Ana Pérez\"", html);
        Assert.Contains("name=\"Command.Phone\" value=\"3005550142\"", html);
    }

    [Fact]
    public async Task Tira_de_la_semana_tiene_seis_enlaces_y_aria_current_en_dia_actual()
    {
        await using var harness = await ServerHarness.CreateAsync();
        var adminClient = await CreateFirstAdminAsync(harness);
        await CreateUserAsync(harness, adminClient, "recep1", "Recepcionista Uno", "recepcion", "contrasena123");

        var recepCookies = new CookieContainer();
        using var recepClient = harness.CreateClient(recepCookies);
        await LoginAsync(recepClient, "recep1", "contrasena123");

        var getRes = await recepClient.GetAsync("/agenda?dia=2026-11-16");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        var html = await getRes.Content.ReadAsStringAsync();
        Assert.Contains("<nav class=\"week\">", html);

        // 6 enlaces dentro de la tira de la semana (uno por día de lunes a sábado)
        var weekMatch = Regex.Match(html, @"<nav class=""week"">.*?</nav>", RegexOptions.Singleline);
        Assert.True(weekMatch.Success);
        var weekHtml = weekMatch.Value;

        var matches = Regex.Matches(weekHtml, @"<a href=""/agenda\?dia=\d{4}-\d{2}-\d{2}""([^>]*)>");
        Assert.Equal(6, matches.Count);

        // Solo el enlace del día actual (2026-11-16) tiene aria-current="date"
        var currentMatches = Regex.Matches(weekHtml, @"aria-current=""date""");
        Assert.Single(currentMatches);
        Assert.Contains("href=\"/agenda?dia=2026-11-16\" aria-current=\"date\"", weekHtml);
    }
}
