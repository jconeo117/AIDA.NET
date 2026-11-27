using System.Net;
using System.Text.RegularExpressions;
using Aida.Arms.Agenda.Desk;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aida.Web.Tests;

public sealed class AgendaSamePhoneWebTests
{
    [Fact]
    public async Task Post_agendar_con_telefono_conocido_muestra_panel_mismo_telefono_sin_Location_y_permite_confirmar()
    {
        await using var harness = await ServerHarness.CreateAsync();
        harness.Time.Now = DateTimeOffset.Parse("2026-11-24T09:00:00-05:00");
        var adminClient = await AgendaAssistantWebHelper.CreateFirstAdminAsync(harness);

        // 1. Agendamos a Ana Pérez inicialmente (crea la persona con teléfono 3001111111)
        var getRes1 = await adminClient.GetAsync("/agenda?dia=2026-11-24&nueva=control");
        var token1 = ServerHarness.ExtractAntiforgeryToken(await getRes1.Content.ReadAsStringAsync());

        var postRes1 = await adminClient.PostAsync("/agenda?dia=2026-11-24&nueva=control", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-action",
            ["Command.Action"] = "agendar",
            ["Command.Client"] = "new",
            ["Command.Service"] = "control",
            ["Command.Name"] = "Ana Pérez",
            ["Command.Phone"] = "3001111111",
            ["Command.Slot"] = "consultorio_1|2026-11-24T10:00:00-05:00",
            ["__RequestVerificationToken"] = token1,
        }));
        Assert.Equal(HttpStatusCode.Redirect, postRes1.StatusCode);

        var desk = harness.App.Services.GetRequiredService<AgendaDesk>();

        // 2. Intentamos agendar a "Niño Pérez" con el mismo teléfono (client=new)
        var getRes2 = await adminClient.GetAsync("/agenda?dia=2026-11-24&nueva=control");
        var token2 = ServerHarness.ExtractAntiforgeryToken(await getRes2.Content.ReadAsStringAsync());

        var postRes2 = await adminClient.PostAsync("/agenda?dia=2026-11-24&nueva=control", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-action",
            ["Command.Action"] = "agendar",
            ["Command.Client"] = "new",
            ["Command.Service"] = "control",
            ["Command.Name"] = "Niño Pérez",
            ["Command.Phone"] = "3001111111",
            ["Command.Slot"] = "consultorio_1|2026-11-24T10:30:00-05:00",
            ["__RequestVerificationToken"] = token2,
        }));

        // Devuelve 200, sin Location
        Assert.Equal(HttpStatusCode.OK, postRes2.StatusCode);
        Assert.Null(postRes2.Headers.Location);

        var html2 = WebUtility.HtmlDecode(await postRes2.Content.ReadAsStringAsync());
        // Muestra el panel y el texto del rechazo
        Assert.Contains("Ese teléfono ya es de otra persona", html2);
        Assert.Contains("Ana Pérez", html2);
        Assert.Contains("Es esta persona", html2);
        Assert.Contains("Es una persona nueva", html2);
        Assert.Contains("name=\"Command.Client\" value=\"1\"", html2);
        Assert.Contains("name=\"Command.Client\" value=\"new_confirmed\"", html2);

        // Campos ocultos llevan service, slot, etc.
        Assert.Contains("name=\"Command.Service\" value=\"control\"", html2);
        Assert.Contains("name=\"Command.Slot\" value=\"consultorio_1|2026-11-24T10:30:00-05:00\"", html2);

        // Ninguna URL de la respuesta lleva el nombre ni el teléfono
        var linkMatches = Regex.Matches(html2, @"(?:href|action)=""([^""]+)""");
        foreach (Match match in linkMatches)
        {
            Assert.DoesNotContain("Niño", match.Groups[1].Value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("3001111111", match.Groups[1].Value);
        }

        // Verifica que en los eventos esté action_rejected con phone_known
        var eventsAfterReject = desk.EventLines();
        Assert.Contains(eventsAfterReject, e => e.Contains("action_rejected") && e.Contains("phone_known"));

        // 3. Enviamos con client=1 ("Es esta persona")
        var token3 = ServerHarness.ExtractAntiforgeryToken(html2);
        var postRes3 = await adminClient.PostAsync("/agenda?dia=2026-11-24", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-action",
            ["Command.Action"] = "agendar",
            ["Command.Client"] = "1",
            ["Command.Service"] = "control",
            ["Command.Name"] = "Niño Pérez",
            ["Command.Phone"] = "3001111111",
            ["Command.Slot"] = "consultorio_1|2026-11-24T10:30:00-05:00",
            ["__RequestVerificationToken"] = token3,
        }));
        Assert.Equal(HttpStatusCode.Redirect, postRes3.StatusCode);

        // Verifica que en los eventos aparezca action_completed posterior a phone_known
        var eventsAfterBook = desk.EventLines();
        Assert.Contains(eventsAfterBook, e => e.Contains("action_completed") && e.Contains("agendar"));

        // 4. Ahora probamos con new_confirmed para otra cita
        var getRes4 = await adminClient.GetAsync("/agenda?dia=2026-11-24&nueva=control");
        var token4 = ServerHarness.ExtractAntiforgeryToken(await getRes4.Content.ReadAsStringAsync());

        var postRes4 = await adminClient.PostAsync("/agenda?dia=2026-11-24&nueva=control", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-action",
            ["Command.Action"] = "agendar",
            ["Command.Client"] = "new_confirmed",
            ["Command.Service"] = "control",
            ["Command.Name"] = "Hijo Confirmado",
            ["Command.Phone"] = "3001111111",
            ["Command.Slot"] = "consultorio_1|2026-11-24T11:00:00-05:00",
            ["__RequestVerificationToken"] = token4,
        }));
        Assert.Equal(HttpStatusCode.Redirect, postRes4.StatusCode);
    }
}
