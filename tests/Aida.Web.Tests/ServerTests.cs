using System.Net;
using Xunit;

namespace Aida.Web.Tests;

public sealed class ServerTests
{
    [Fact]
    public async Task Sin_usuarios_todo_lleva_a_crear_el_primero()
    {
        await using var harness = await ServerHarness.CreateAsync();
        using var client = harness.CreateClient();

        var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/primer-usuario", LocationPath(response));
    }

    [Fact]
    public async Task El_primer_administrador_entra_y_ve_su_nombre()
    {
        await using var harness = await ServerHarness.CreateAsync();
        var cookies = new CookieContainer();
        using var client = harness.CreateClient(cookies);

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
        Assert.Equal("/", LocationPath(postRes));

        var homeRes = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, homeRes.StatusCode);
        var homeHtml = WebUtility.HtmlDecode(await homeRes.Content.ReadAsStringAsync());
        Assert.Contains("Ana Pérez", homeHtml);
        Assert.Contains("<a href=\"/usuarios\">", homeHtml);
    }

    [Fact]
    public async Task Sin_sesion_no_responde_ninguna_pagina_de_datos()
    {
        await using var harness = await ServerHarness.CreateAsync();
        await CreateFirstAdminAsync(harness);

        using var anonymousClient = harness.CreateClient();

        var homeRes = await anonymousClient.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, homeRes.StatusCode);
        Assert.Equal("/entrar", LocationPath(homeRes));

        var usersRes = await anonymousClient.GetAsync("/usuarios");
        Assert.Equal(HttpStatusCode.Redirect, usersRes.StatusCode);
        Assert.Equal("/entrar", LocationPath(usersRes));
    }

    [Fact]
    public async Task Una_accion_fuera_del_rol_no_se_ejecuta()
    {
        await using var harness = await ServerHarness.CreateAsync();
        var adminClient = await CreateFirstAdminAsync(harness);

        await CreateUserAsync(harness, adminClient, "recepcion1", "Carlos Gómez", "recepcion", "contrasena123");

        var recepCookies = new CookieContainer();
        using var recepClient = harness.CreateClient(recepCookies);
        await LoginAsync(recepClient, "recepcion1", "contrasena123");

        // GET /usuarios -> 302 a /
        var getRes = await recepClient.GetAsync("/usuarios");
        Assert.Equal(HttpStatusCode.Redirect, getRes.StatusCode);
        Assert.Equal("/", LocationPath(getRes));

        // Obtenemos token para intentar un POST
        var homeRes = await recepClient.GetAsync("/");
        var token = ServerHarness.ExtractAntiforgeryToken(await homeRes.Content.ReadAsStringAsync());

        var usersBefore = harness.Users.List().Count;
        var linesBefore = harness.Audit.Lines().Count;

        var postContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "create",
            ["NewUser.UserName"] = "intruso1",
            ["NewUser.DisplayName"] = "Intruso",
            ["NewUser.RoleId"] = "administracion",
            ["NewUser.Password"] = "contrasena123",
            ["__RequestVerificationToken"] = token,
        });

        var postRes = await recepClient.PostAsync("/usuarios", postContent);
        // La política de autorización deniega y redirige a AccessDeniedPath ("/")
        Assert.Equal(HttpStatusCode.Redirect, postRes.StatusCode);
        Assert.Equal("/", LocationPath(postRes));

        // Comprobamos que no se creó nadie ni se escribió en la auditoría
        Assert.Equal(usersBefore, harness.Users.List().Count);
        Assert.Equal(linesBefore, harness.Audit.Lines().Count);
    }

    [Fact]
    public async Task Todo_hecho_lleva_el_actor_de_la_sesion()
    {
        await using var harness = await ServerHarness.CreateAsync();
        await RunFullJourneyAsync(harness);

        var lines = harness.Audit.Lines();
        Assert.Equal(7, lines.Count);

        // Líneas 1 a 3 son del admin ("1", "Ana Pérez")
        for (var i = 0; i < 3; i++)
        {
            Assert.Contains("\"actor\": \"1\"", lines[i]);
            Assert.Contains("\"actor_name\": \"Ana Pérez\"", lines[i]);
            Assert.Contains("\"workstation\": \"127.0.0.1\"", lines[i]);
        }

        // Líneas 4 a 7 son de recepción ("2", "Carlos Gómez")
        for (var i = 3; i < 7; i++)
        {
            Assert.Contains("\"actor\": \"2\"", lines[i]);
            Assert.Contains("\"actor_name\": \"Carlos Gómez\"", lines[i]);
            Assert.Contains("\"workstation\": \"127.0.0.1\"", lines[i]);
        }
    }

    [Fact]
    public async Task Una_entrada_fallida_dice_los_intentos_que_quedan()
    {
        await using var harness = await ServerHarness.CreateAsync();
        await CreateFirstAdminAsync(harness);

        using var client = harness.CreateClient();
        var getRes = await client.GetAsync("/entrar");
        var token = ServerHarness.ExtractAntiforgeryToken(await getRes.Content.ReadAsStringAsync());

        var postContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "login",
            ["Form.UserName"] = "admin",
            ["Form.Password"] = "contrasena-incorrecta",
            ["__RequestVerificationToken"] = token,
        });

        var postRes = await client.PostAsync("/entrar", postContent);
        var html = WebUtility.HtmlDecode(await postRes.Content.ReadAsStringAsync());
        Assert.Contains("Le quedan 4 intentos", html);
    }

    [Fact]
    public async Task El_puesto_se_bloquea_y_se_desbloquea()
    {
        await using var harness = await ServerHarness.CreateAsync();
        var client = await CreateFirstAdminAsync(harness);

        harness.Time.Advance(TimeSpan.FromMinutes(10));

        var getHome = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, getHome.StatusCode);
        Assert.Equal("/bloqueado", LocationPath(getHome));

        var getBlocked = await client.GetAsync("/bloqueado");
        var token = ServerHarness.ExtractAntiforgeryToken(await getBlocked.Content.ReadAsStringAsync());

        var unlockContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "unlock",
            ["Form.Password"] = "contrasena123",
            ["__RequestVerificationToken"] = token,
        });

        var postUnlock = await client.PostAsync("/bloqueado", unlockContent);
        Assert.Equal(HttpStatusCode.Redirect, postUnlock.StatusCode);
        Assert.Equal("/", LocationPath(postUnlock));

        var getBlockedAgain = await client.GetAsync("/bloqueado");
        Assert.Equal(HttpStatusCode.Redirect, getBlockedAgain.StatusCode);
        Assert.Equal("/", LocationPath(getBlockedAgain));
    }

    [Fact]
    public async Task Salir_cierra_la_sesion()
    {
        await using var harness = await ServerHarness.CreateAsync();
        var client = await CreateFirstAdminAsync(harness);

        var homeRes = await client.GetAsync("/");
        var token = ServerHarness.ExtractAntiforgeryToken(await homeRes.Content.ReadAsStringAsync());

        var logoutContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
        });

        var logoutRes = await client.PostAsync("/salir", logoutContent);
        Assert.Equal(HttpStatusCode.Redirect, logoutRes.StatusCode);
        Assert.Equal("/entrar", LocationPath(logoutRes));

        var afterLogout = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, afterLogout.StatusCode);
        Assert.Equal("/entrar", LocationPath(afterLogout));
    }

    [Fact]
    public async Task La_secuencia_de_la_auditoria()
    {
        await using var harness = await ServerHarness.CreateAsync();
        await RunFullJourneyAsync(harness);

        var lines = harness.Audit.Lines();
        Assert.Equal(7, lines.Count);

        Assert.Contains("\"kind\": \"user_created\"", lines[0]);
        Assert.Contains("\"kind\": \"session_opened\"", lines[1]);
        Assert.Contains("\"kind\": \"user_created\"", lines[2]);
        Assert.Contains("\"kind\": \"session_opened\"", lines[3]);
        Assert.Contains("\"kind\": \"locked\"", lines[4]);
        Assert.Contains("\"kind\": \"unlocked\"", lines[5]);
        Assert.Contains("\"kind\": \"session_closed\"", lines[6]);

        Assert.Empty(harness.Audit.Verify());
    }

    [Fact]
    public async Task Http_en_claro_no_recibe_paginas()
    {
        await using var harness = await ServerHarness.CreateAsync();
        using var plainClient = new HttpClient(new HttpClientHandler { UseProxy = false });

        var plainUrl = $"{harness.HttpBaseUrl}/entrar";
        await Assert.ThrowsAsync<HttpRequestException>(() => plainClient.GetAsync(plainUrl));
    }

    private static string LocationPath(HttpResponseMessage response) =>
        response.Headers.Location?.IsAbsoluteUri == true
            ? response.Headers.Location.AbsolutePath
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

    private static async Task RunFullJourneyAsync(ServerHarness harness)
    {
        // 1. Admin entra (user_created [1], session_opened [2])
        var adminClient = await CreateFirstAdminAsync(harness);

        // 2. Admin crea recepcion (user_created [3])
        await CreateUserAsync(harness, adminClient, "recepcion1", "Carlos Gómez", "recepcion", "contrasena123");

        // 3. Recepción entra (session_opened [4])
        var recepCookies = new CookieContainer();
        using var recepClient = harness.CreateClient(recepCookies);
        await LoginAsync(recepClient, "recepcion1", "contrasena123");

        // 4. Recepción se bloquea tras 10 min de inactividad (locked [5])
        harness.Time.Advance(TimeSpan.FromMinutes(10));
        var checkBlocked = await recepClient.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, checkBlocked.StatusCode);
        Assert.Equal("/bloqueado", LocationPath(checkBlocked));

        // 5. Recepción desbloquea (unlocked [6])
        var getBlocked = await recepClient.GetAsync("/bloqueado");
        var tokenUnlock = ServerHarness.ExtractAntiforgeryToken(await getBlocked.Content.ReadAsStringAsync());
        var unlockPost = await recepClient.PostAsync("/bloqueado", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "unlock",
            ["Form.Password"] = "contrasena123",
            ["__RequestVerificationToken"] = tokenUnlock,
        }));
        Assert.Equal(HttpStatusCode.Redirect, unlockPost.StatusCode);

        // 6. Recepción sale (session_closed [7])
        var getHome = await recepClient.GetAsync("/");
        var tokenLogout = ServerHarness.ExtractAntiforgeryToken(await getHome.Content.ReadAsStringAsync());
        var logoutPost = await recepClient.PostAsync("/salir", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = tokenLogout,
        }));
        Assert.Equal(HttpStatusCode.Redirect, logoutPost.StatusCode);
    }
}
