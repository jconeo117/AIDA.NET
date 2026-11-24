using System.Net;
using System.Net.Http.Headers;
using Aida.Platform.Audit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Aida.Web.Tests;

public sealed class SnapshotWebTests
{
    private const string Passphrase = "una_frase_segura_de_12_chars";

    [Fact]
    public async Task Ajustes_permiso_por_rol_y_post_no_autorizado_no_deja_copia_ni_auditoria()
    {
        await using var harness = await ServerHarness.CreateAsync();
        var adminClient = await CreateAdminAsync(harness);

        // Admin ve /ajustes
        var adminGet = await adminClient.GetAsync("/ajustes");
        Assert.Equal(HttpStatusCode.OK, adminGet.StatusCode);
        var adminHtml = await adminGet.Content.ReadAsStringAsync();
        Assert.Contains("Copia de seguridad", adminHtml);

        // Creamos recepcionista
        await CreateUserAsync(harness, adminClient, "recep1", "Recepcionista", "recepcion", "contrasena123");
        var recepClient = harness.CreateClient(new CookieContainer());
        await LoginAsync(recepClient, "recep1", "contrasena123");

        // Recepción es redirigida al intentar ver /ajustes
        var recepGet = await recepClient.GetAsync("/ajustes");
        Assert.Equal(HttpStatusCode.Redirect, recepGet.StatusCode);

        // POST /ajustes/copia por recepción no autorizado
        var token = ServerHarness.ExtractAntiforgeryToken(adminHtml);
        var postContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["passphrase"] = Passphrase, ["passphrase2"] = Passphrase, ["__RequestVerificationToken"] = token,
        });

        var postRes = await recepClient.PostAsync("/ajustes/copia", postContent);
        Assert.True(postRes.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Redirect);
        Assert.DoesNotContain("snapshot_created", string.Join("\n", harness.Audit.Lines()));
    }

    [Fact]
    public async Task Errores_de_la_copia_mismatch_y_weak_redirigen_con_su_codigo()
    {
        await using var harness = await ServerHarness.CreateAsync();
        var client = await CreateAdminAsync(harness);
        var getRes = await client.GetAsync("/ajustes");
        var token = ServerHarness.ExtractAntiforgeryToken(await getRes.Content.ReadAsStringAsync());

        // Mismatch
        var postMismatch = await client.PostAsync("/ajustes/copia", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["passphrase"] = Passphrase, ["passphrase2"] = "distinta_de_12_chars", ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, postMismatch.StatusCode);
        Assert.Equal("/ajustes?error=mismatch", Location(postMismatch));

        // Weak
        var postWeak = await client.PostAsync("/ajustes/copia", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["passphrase"] = "corta_de_11", ["passphrase2"] = "corta_de_11", ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, postWeak.StatusCode);
        Assert.Equal("/ajustes?error=weak", Location(postWeak));
    }

    [Fact]
    public async Task La_copia_responde_octet_stream_con_nombre_aidacopia_y_auditoria()
    {
        await using var harness = await ServerHarness.CreateAsync();
        var client = await CreateAdminAsync(harness);
        var getRes = await client.GetAsync("/ajustes");
        var token = ServerHarness.ExtractAntiforgeryToken(await getRes.Content.ReadAsStringAsync());

        var postRes = await client.PostAsync("/ajustes/copia", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["passphrase"] = Passphrase, ["passphrase2"] = Passphrase, ["__RequestVerificationToken"] = token,
        }));

        Assert.Equal(HttpStatusCode.OK, postRes.StatusCode);
        Assert.Equal("application/octet-stream", postRes.Content.Headers.ContentType?.MediaType);
        var fileName = postRes.Content.Headers.ContentDisposition?.FileNameStar ?? postRes.Content.Headers.ContentDisposition?.FileName;
        Assert.NotNull(fileName);
        Assert.EndsWith(".aidacopia", fileName);

        var created = harness.Audit.Lines().FirstOrDefault(line => line.Contains("snapshot_created"));
        Assert.NotNull(created);
        Assert.Contains("files", created);
    }

    [Fact]
    public async Task Ida_y_vuelta_entre_servidores_con_restauracion_y_aviso()
    {
        // 1. Servidor A con usuario y cita
        byte[] backupBytes;
        await using (var harnessA = await ServerHarness.CreateAsync())
        {
            var adminA = await CreateAdminAsync(harnessA);
            await CreateUserAsync(harnessA, adminA, "recep1", "Carlos", "recepcion", "contrasena123");
            var recepClient = harnessA.CreateClient(new CookieContainer());
            await LoginAsync(recepClient, "recep1", "contrasena123");
            await AgendarCitaAsync(recepClient, "2026-11-16", "Ana Pérez", "3005550142");

            var getAjustes = await adminA.GetAsync("/ajustes");
            var tokenA = ServerHarness.ExtractAntiforgeryToken(await getAjustes.Content.ReadAsStringAsync());
            var copyRes = await adminA.PostAsync("/ajustes/copia", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["passphrase"] = Passphrase, ["passphrase2"] = Passphrase, ["__RequestVerificationToken"] = tokenA,
            }));
            Assert.Equal(HttpStatusCode.OK, copyRes.StatusCode);
            backupBytes = await copyRes.Content.ReadAsByteArrayAsync();
        }

        // 2. Servidor B vacío: se restaura
        var harnessB = await ServerHarness.CreateAsync(deleteOnDispose: false);
        var tempDirB = harnessB.TempDir;
        var stoppingTask = WaitForStopAsync(harnessB.App.Services.GetRequiredService<IHostApplicationLifetime>());
        using (var clientB = harnessB.CreateClient())
        {
            var getFirst = await clientB.GetAsync("/primer-usuario");
            var tokenB = ServerHarness.ExtractAntiforgeryToken(await getFirst.Content.ReadAsStringAsync());

            using var form = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(backupBytes) { Headers = { ContentType = MediaTypeHeaderValue.Parse("application/octet-stream") } };
            form.Add(fileContent, "file", "copia.aidacopia");
            form.Add(new StringContent(Passphrase), "passphrase");
            form.Add(new StringContent(tokenB), "__RequestVerificationToken");

            var restoreRes = await clientB.PostAsync("/primer-usuario/restaurar", form);
            Assert.Equal(HttpStatusCode.OK, restoreRes.StatusCode);
            var restoreHtml = await restoreRes.Content.ReadAsStringAsync();
            Assert.Contains("Copia preparada", restoreHtml);
        }

        await stoppingTask;
        await harnessB.DisposeAsync();

        // 3. Servidor C sobre los datos de B
        await using (var harnessC = await ServerHarness.CreateAsync(existingTempDir: tempDirB))
        {
            using var clientC = harnessC.CreateClient(new CookieContainer());
            await LoginAsync(clientC, "admin", "contrasena123");

            // Verifica que la cita sigue existiendo
            var agendaRes = await clientC.GetAsync("/agenda?dia=2026-11-16&cita=1");
            Assert.Equal(HttpStatusCode.OK, agendaRes.StatusCode);
            var agendaHtml = WebUtility.HtmlDecode(await agendaRes.Content.ReadAsStringAsync());
            Assert.Contains("Ana Pérez", agendaHtml);

            // Verifica que snapshot_restored se firmó en la auditoría con el autor admin
            var lines = harnessC.Audit.Lines();
            var restoredLine = lines.FirstOrDefault(l => l.Contains("snapshot_restored"));
            Assert.NotNull(restoredLine);
            Assert.Contains("Admin", restoredLine);
        }
    }

    [Fact]
    public async Task Rechazos_de_restauracion_con_usuarios_da_403_y_no_deja_restaurar()
    {
        await using var harness = await ServerHarness.CreateAsync();
        await CreateAdminAsync(harness);

        using var client = harness.CreateClient();
        var getEntrar = await client.GetAsync("/entrar");
        var token = ServerHarness.ExtractAntiforgeryToken(await getEntrar.Content.ReadAsStringAsync());

        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([1, 2, 3]), "file", "fake.aidacopia");
        form.Add(new StringContent(Passphrase), "passphrase");
        form.Add(new StringContent(token), "__RequestVerificationToken");

        var response = await client.PostAsync("/primer-usuario/restaurar", form);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var stagingDir = Path.Combine(harness.DataDir, "restaurar");
        Assert.False(Directory.Exists(stagingDir));
    }

    [Fact]
    public async Task Rechazos_de_restauracion_otra_frase_sin_archivo_y_xss()
    {
        await using var harness = await ServerHarness.CreateAsync();
        using var client = harness.CreateClient();
        var getFirst = await client.GetAsync("/primer-usuario");
        var token = ServerHarness.ExtractAntiforgeryToken(await getFirst.Content.ReadAsStringAsync());

        // Sin archivo redirige a copia=nofile
        using var emptyForm = new MultipartFormDataContent();
        emptyForm.Add(new StringContent(Passphrase), "passphrase");
        emptyForm.Add(new StringContent(token), "__RequestVerificationToken");
        var resNoFile = await client.PostAsync("/primer-usuario/restaurar", emptyForm);
        Assert.Equal(HttpStatusCode.Redirect, resNoFile.StatusCode);
        Assert.Equal("/primer-usuario?copia=nofile", Location(resNoFile));

        // Otra frase redirige a copia=bad_passphrase
        byte[] copyBytes;
        await using (var adminHarness = await ServerHarness.CreateAsync())
        {
            var admin = await CreateAdminAsync(adminHarness);
            var getA = await admin.GetAsync("/ajustes");
            var tok = ServerHarness.ExtractAntiforgeryToken(await getA.Content.ReadAsStringAsync());
            var resA = await admin.PostAsync("/ajustes/copia", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["passphrase"] = Passphrase, ["passphrase2"] = Passphrase, ["__RequestVerificationToken"] = tok,
            }));
            copyBytes = await resA.Content.ReadAsByteArrayAsync();
        }

        using var formBadPass = new MultipartFormDataContent();
        formBadPass.Add(new ByteArrayContent(copyBytes), "file", "copia.aidacopia");
        formBadPass.Add(new StringContent("frase_erronea_12"), "passphrase");
        formBadPass.Add(new StringContent(token), "__RequestVerificationToken");
        var resBadPass = await client.PostAsync("/primer-usuario/restaurar", formBadPass);
        Assert.Equal(HttpStatusCode.Redirect, resBadPass.StatusCode);
        Assert.Equal("/primer-usuario?copia=bad_passphrase", Location(resBadPass));

        // XSS con script no se pinta sin escapar
        var resXss = await client.GetAsync("/primer-usuario?copia=<script>alert(1)</script>");
        Assert.Equal(HttpStatusCode.OK, resXss.StatusCode);
        Assert.DoesNotContain("<script>alert(1)</script>", await resXss.Content.ReadAsStringAsync());
    }

    private static async Task<Task> WaitForStopAsync(IHostApplicationLifetime lifetime)
    {
        var tcs = new TaskCompletionSource();
        lifetime.ApplicationStopping.Register(() => tcs.TrySetResult());
        return tcs.Task;
    }

    private static string Location(HttpResponseMessage res) =>
        res.Headers.Location?.IsAbsoluteUri == true ? res.Headers.Location.PathAndQuery : res.Headers.Location?.ToString() ?? string.Empty;

    private static async Task<HttpClient> CreateAdminAsync(ServerHarness harness)
    {
        var client = harness.CreateClient(new CookieContainer());
        var getRes = await client.GetAsync("/primer-usuario");
        var token = ServerHarness.ExtractAntiforgeryToken(await getRes.Content.ReadAsStringAsync());
        var postRes = await client.PostAsync("/primer-usuario", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "first", ["Form.UserName"] = "admin", ["Form.DisplayName"] = "Admin", ["Form.Password"] = "contrasena123", ["Form.Password2"] = "contrasena123", ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, postRes.StatusCode);
        return client;
    }

    private static async Task CreateUserAsync(ServerHarness harness, HttpClient adminClient, string userName, string displayName, string roleId, string password)
    {
        var getRes = await adminClient.GetAsync("/usuarios");
        var token = ServerHarness.ExtractAntiforgeryToken(await getRes.Content.ReadAsStringAsync());
        var postRes = await adminClient.PostAsync("/usuarios", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "create", ["NewUser.UserName"] = userName, ["NewUser.DisplayName"] = displayName, ["NewUser.RoleId"] = roleId, ["NewUser.Password"] = password, ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.OK, postRes.StatusCode);
    }

    private static async Task LoginAsync(HttpClient client, string userName, string password)
    {
        var getRes = await client.GetAsync("/entrar");
        var token = ServerHarness.ExtractAntiforgeryToken(await getRes.Content.ReadAsStringAsync());
        var postRes = await client.PostAsync("/entrar", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "login", ["Form.UserName"] = userName, ["Form.Password"] = password, ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, postRes.StatusCode);
    }

    private static async Task AgendarCitaAsync(HttpClient client, string dia, string name, string phone)
    {
        var getRes = await client.GetAsync($"/agenda?dia={dia}&nueva=control");
        var token = ServerHarness.ExtractAntiforgeryToken(await getRes.Content.ReadAsStringAsync());
        var postRes = await client.PostAsync($"/agenda?dia={dia}&nueva=control", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-action", ["Command.Action"] = "agendar", ["Command.Service"] = "control", ["Command.Name"] = name, ["Command.Phone"] = phone, ["Command.Slot"] = $"consultorio_1|{dia}T08:00:00-05:00", ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, postRes.StatusCode);
    }
}
