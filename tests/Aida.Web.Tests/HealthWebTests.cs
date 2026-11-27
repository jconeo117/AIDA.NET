using System.Net;
using Xunit;

namespace Aida.Web.Tests;

public sealed class HealthWebTests
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

        var postRes = await client.PostAsync("/primer-usuario", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "first",
            ["Form.UserName"] = "admin",
            ["Form.DisplayName"] = "Admin",
            ["Form.Password"] = "contrasena123",
            ["Form.Password2"] = "contrasena123",
            ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, postRes.StatusCode);
        return client;
    }

    private static async Task CreateUserAsync(HttpClient adminClient, string userName, string displayName, string roleId, string password)
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

    private static async Task AgendarYCompletarPersonaAsync(HttpClient client, string dia, string name, string phone)
    {
        var getRes = await client.GetAsync($"/agenda?dia={dia}&nueva=control");
        var token = ServerHarness.ExtractAntiforgeryToken(await getRes.Content.ReadAsStringAsync());

        await client.PostAsync($"/agenda?dia={dia}&nueva=control", new FormUrlEncodedContent(new Dictionary<string, string>
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

        var getContacto = await client.GetAsync($"/agenda?dia={dia}&contacto=1");
        var tokenContacto = ServerHarness.ExtractAntiforgeryToken(await getContacto.Content.ReadAsStringAsync());

        await client.PostAsync($"/agenda?dia={dia}&contacto=1", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-action",
            ["Command.Action"] = "editar_contacto",
            ["Command.Client"] = "1",
            ["Command.Name"] = name,
            ["Command.Phone"] = phone,
            ["Command.DocumentType"] = "CC",
            ["Command.DocumentNumber"] = "12345678",
            ["Command.BirthDate"] = "1990-01-01",
            ["Command.Sex"] = "M",
            ["__RequestVerificationToken"] = tokenContacto,
        }));
    }

    [Fact]
    public async Task Profesional_ve_enlace_en_ficha_y_recepcionista_no_y_403()
    {
        await using var harness = await ServerHarness.CreateAsync();
        var adminClient = await CreateFirstAdminAsync(harness);
        await CreateUserAsync(adminClient, "prof1", "Dr. Uno", "profesional", "contrasena123");
        await CreateUserAsync(adminClient, "rec1", "Recep", "recepcion", "contrasena123");

        using var recClient = harness.CreateClient(new CookieContainer());
        await LoginAsync(recClient, "rec1", "contrasena123");
        await AgendarYCompletarPersonaAsync(recClient, "2026-11-20", "Ana Perez", "3001234567");

        // Recepcionista ve ficha sin enlace a historia
        var fichaRec = await recClient.GetAsync("/agenda?dia=2026-11-20&ficha=1");
        var htmlRec = await fichaRec.Content.ReadAsStringAsync();
        Assert.DoesNotContain("/historia/1", htmlRec);

        // Recepcionista intenta entrar a /historia/1 -> 403
        var histRec = await recClient.GetAsync("/historia/1");
        Assert.Equal(HttpStatusCode.Forbidden, histRec.StatusCode);

        // Profesional ve la ficha con enlace a historia
        using var profClient = harness.CreateClient(new CookieContainer());
        await LoginAsync(profClient, "prof1", "contrasena123");
        var fichaProf = await profClient.GetAsync("/agenda?dia=2026-11-20&ficha=1");
        var htmlProf = await fichaProf.Content.ReadAsStringAsync();
        Assert.Contains("/historia/1", htmlProf);

        // Profesional entra a /historia/1 -> 200 OK
        var histProf = await profClient.GetAsync("/historia/1");
        Assert.Equal(HttpStatusCode.OK, histProf.StatusCode);
    }

    [Fact]
    public async Task Ciclo_completo_nueva_guardar_revisar_y_firmar_sin_filtrar_contrasena()
    {
        await using var harness = await ServerHarness.CreateAsync();
        var adminClient = await CreateFirstAdminAsync(harness);
        const string profPass = "MiClaveSecreta987!";
        await CreateUserAsync(adminClient, "prof1", "Dr. Uno", "profesional", profPass);
        await CreateUserAsync(adminClient, "rec1", "Recep", "recepcion", "contrasena123");

        using var recClient = harness.CreateClient(new CookieContainer());
        await LoginAsync(recClient, "rec1", "contrasena123");
        await AgendarYCompletarPersonaAsync(recClient, "2026-11-20", "Carlos Ruiz", "3009876543");

        using var profClient = harness.CreateClient(new CookieContainer());
        await LoginAsync(profClient, "prof1", profPass);

        // 1. GET /historia/1
        var getHist = await profClient.GetAsync("/historia/1");
        var tokenHist = ServerHarness.ExtractAntiforgeryToken(await getHist.Content.ReadAsStringAsync());

        // 2. POST nueva atención -> redirect /historia/atencion/{id}
        var createRes = await profClient.PostAsync("/historia/1", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "health-create",
            ["__RequestVerificationToken"] = tokenHist,
        }));
        Assert.Equal(HttpStatusCode.Redirect, createRes.StatusCode);
        var atencionUrl = Location(createRes);
        Assert.StartsWith("/historia/atencion/", atencionUrl);

        // 3. GET /historia/atencion/{id}
        var getAten = await profClient.GetAsync(atencionUrl);
        Assert.Equal(HttpStatusCode.OK, getAten.StatusCode);
        var tokenAten = ServerHarness.ExtractAntiforgeryToken(await getAten.Content.ReadAsStringAsync());

        // 4. POST guardar
        var saveRes = await profClient.PostAsync(atencionUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "health-encounter",
            ["Encounter.Step"] = "save",
            ["Encounter.Fields[motivo_consulta]"] = "Cefalea persistente",
            ["__RequestVerificationToken"] = tokenAten,
        }));
        Assert.Equal(HttpStatusCode.OK, saveRes.StatusCode);
        var saveHtml = await saveRes.Content.ReadAsStringAsync();
        tokenAten = ServerHarness.ExtractAntiforgeryToken(saveHtml);

        // 5. POST revisar -> muestra vista previa y aviso de faltantes
        var revRes = await profClient.PostAsync(atencionUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "health-encounter",
            ["Encounter.Step"] = "review",
            ["Encounter.Fields[motivo_consulta]"] = "Cefalea persistente",
            ["__RequestVerificationToken"] = tokenAten,
        }));
        Assert.Equal(HttpStatusCode.OK, revRes.StatusCode);
        var revHtml = await revRes.Content.ReadAsStringAsync();
        Assert.Contains("Cefalea persistente", revHtml);
        tokenAten = ServerHarness.ExtractAntiforgeryToken(revHtml);

        // 6. POST firmar con contraseña
        var signRes = await profClient.PostAsync(atencionUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "health-encounter",
            ["Encounter.Step"] = "sign",
            ["Encounter.Fields[motivo_consulta]"] = "Cefalea persistente",
            ["Encounter.Password"] = profPass,
            ["__RequestVerificationToken"] = tokenAten,
        }));
        Assert.Equal(HttpStatusCode.Redirect, signRes.StatusCode);

        // 7. GET atencion firmada
        var signedRes = await profClient.GetAsync(Location(signRes));
        var signedHtml = await signedRes.Content.ReadAsStringAsync();
        Assert.Contains("Huella", signedHtml);
        Assert.Contains("Firmada por", signedHtml);

        // Verificación crítica: La contraseña NO aparece en ninguna respuesta HTML
        Assert.DoesNotContain(profPass, saveHtml);
        Assert.DoesNotContain(profPass, revHtml);
        Assert.DoesNotContain(profPass, signedHtml);
    }

    [Fact]
    public async Task Borrador_ajeno_da_404()
    {
        await using var harness = await ServerHarness.CreateAsync();
        var adminClient = await CreateFirstAdminAsync(harness);
        await CreateUserAsync(adminClient, "prof1", "Dr. Uno", "profesional", "contrasena123");
        await CreateUserAsync(adminClient, "prof2", "Dr. Dos", "profesional", "contrasena123");
        await CreateUserAsync(adminClient, "rec1", "Recep", "recepcion", "contrasena123");

        using var recClient = harness.CreateClient(new CookieContainer());
        await LoginAsync(recClient, "rec1", "contrasena123");
        await AgendarYCompletarPersonaAsync(recClient, "2026-11-20", "Elena Diaz", "3007654321");

        // prof1 crea un borrador
        using var prof1Client = harness.CreateClient(new CookieContainer());
        await LoginAsync(prof1Client, "prof1", "contrasena123");
        var getHist = await prof1Client.GetAsync("/historia/1");
        var tokenHist = ServerHarness.ExtractAntiforgeryToken(await getHist.Content.ReadAsStringAsync());

        var createRes = await prof1Client.PostAsync("/historia/1", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "health-create",
            ["__RequestVerificationToken"] = tokenHist,
        }));
        var atencionUrl = Location(createRes);

        // prof2 intenta abrir el borrador de prof1 -> 404
        using var prof2Client = harness.CreateClient(new CookieContainer());
        await LoginAsync(prof2Client, "prof2", "contrasena123");
        var resProf2 = await prof2Client.GetAsync(atencionUrl);
        Assert.Equal(HttpStatusCode.NotFound, resProf2.StatusCode);
    }
}
