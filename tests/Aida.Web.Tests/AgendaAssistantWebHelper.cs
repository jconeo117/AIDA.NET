using System.Net;
using Microsoft.Extensions.DependencyInjection;

namespace Aida.Web.Tests;

internal static class AgendaAssistantWebHelper
{
    public static async Task<HttpClient> CreateFirstAdminAsync(ServerHarness harness)
    {
        var cookies = new CookieContainer();
        var client = harness.CreateClient(cookies);

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

    public static async Task CreateUserAsync(ServerHarness harness, HttpClient adminClient, string userName, string displayName, string roleId, string password)
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

    public static async Task LoginAsync(HttpClient client, string userName, string password)
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

    public static async Task<long> SeedClientsAsync(HttpClient client)
    {
        var getRes1 = await client.GetAsync("/agenda?dia=2026-11-24&nueva=control");
        var token1 = ServerHarness.ExtractAntiforgeryToken(await getRes1.Content.ReadAsStringAsync());

        var res1 = await client.PostAsync("/agenda?dia=2026-11-24&nueva=control", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-action",
            ["Command.Action"] = "agendar",
            ["Command.Service"] = "control",
            ["Command.Name"] = "Ana Pérez",
            ["Command.Phone"] = "3001111111",
            ["Command.Slot"] = "consultorio_1|2026-11-24T10:00:00-05:00",
            ["__RequestVerificationToken"] = token1,
        }));
        Assert.Equal(HttpStatusCode.Redirect, res1.StatusCode);

        var getRes2 = await client.GetAsync("/agenda?dia=2026-11-24&nueva=control");
        var token2 = ServerHarness.ExtractAntiforgeryToken(await getRes2.Content.ReadAsStringAsync());

        var res2 = await client.PostAsync("/agenda?dia=2026-11-24&nueva=control", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-action",
            ["Command.Action"] = "agendar",
            ["Command.Service"] = "control",
            ["Command.Name"] = "Ana Gómez",
            ["Command.Phone"] = "3002222222",
            ["Command.Slot"] = "consultorio_1|2026-11-24T10:30:00-05:00",
            ["__RequestVerificationToken"] = token2,
        }));
        Assert.Equal(HttpStatusCode.Redirect, res2.StatusCode);

        // Retorna el ID de la primera cita agendada (Ana Pérez)
        return 1L;
    }
}
