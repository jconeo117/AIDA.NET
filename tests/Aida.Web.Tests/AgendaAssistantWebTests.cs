using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Aida.Arms.Agenda.Desk;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aida.Web.Tests;

public sealed class AgendaAssistantWebTests
{
    [Fact]
    public async Task Con_agenda_manage_el_POST_del_asistente_abre_dia_y_marca_hueco_preferido_sin_Location()
    {
        await using var harness = await ServerHarness.CreateAsync();
        harness.Time.Now = DateTimeOffset.Parse("2026-11-24T09:00:00-05:00");
        var adminClient = await AgendaAssistantWebHelper.CreateFirstAdminAsync(harness);
        await AgendaAssistantWebHelper.SeedClientsAsync(adminClient);

        var desk = harness.App.Services.GetRequiredService<AgendaDesk>();
        var eventsBefore = desk.EventLines().Count;
        var auditBefore = harness.Audit.Lines().Count;

        var getRes = await adminClient.GetAsync("/agenda");
        var getHtml = await getRes.Content.ReadAsStringAsync();
        Assert.Contains("name=\"Assistant.Text\"", getHtml);

        var token = ServerHarness.ExtractAntiforgeryToken(getHtml);

        var postContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-assistant",
            ["Assistant.Text"] = "cita para Ana Pérez mañana a las 10",
            ["__RequestVerificationToken"] = token,
        });

        var postRes = await adminClient.PostAsync("/agenda", postContent);
        Assert.Equal(HttpStatusCode.OK, postRes.StatusCode);
        Assert.Null(postRes.Headers.Location);

        var postHtml = WebUtility.HtmlDecode(await postRes.Content.ReadAsStringAsync());
        Assert.Contains("value=\"Ana Pérez\"", postHtml);
        Assert.Contains("2026-11-25", postHtml);
        Assert.Contains("class=\"preferred\"", postHtml);

        // Ningún enlace o formulario de la respuesta contiene el nombre tecleado
        var linkMatches = Regex.Matches(postHtml, @"(?:href|action)=""([^""]+)""");
        foreach (Match match in linkMatches)
        {
            Assert.DoesNotContain("Ana", match.Groups[1].Value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Pérez", match.Groups[1].Value, StringComparison.OrdinalIgnoreCase);
        }

        // El asistente no ejecuta nada: eventos y auditoría intactos
        Assert.Equal(eventsBefore, desk.EventLines().Count);
        Assert.Equal(auditBefore, harness.Audit.Lines().Count);
    }

    [Fact]
    public async Task Con_agenda_manage_el_POST_de_cancelar_abre_panel_cancelar_sin_Location_y_no_cambia_eventos()
    {
        await using var harness = await ServerHarness.CreateAsync();
        harness.Time.Now = DateTimeOffset.Parse("2026-11-24T09:00:00-05:00");
        var adminClient = await AgendaAssistantWebHelper.CreateFirstAdminAsync(harness);
        await AgendaAssistantWebHelper.SeedClientsAsync(adminClient);

        var desk = harness.App.Services.GetRequiredService<AgendaDesk>();
        var eventsBefore = desk.EventLines().Count;
        var auditBefore = harness.Audit.Lines().Count;

        var getRes = await adminClient.GetAsync("/agenda");
        var token = ServerHarness.ExtractAntiforgeryToken(await getRes.Content.ReadAsStringAsync());

        var postContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-assistant",
            ["Assistant.Text"] = "cancela la cita de Ana Pérez del martes",
            ["__RequestVerificationToken"] = token,
        });

        var postRes = await adminClient.PostAsync("/agenda", postContent);
        Assert.Equal(HttpStatusCode.OK, postRes.StatusCode);
        Assert.Null(postRes.Headers.Location);

        var postHtml = WebUtility.HtmlDecode(await postRes.Content.ReadAsStringAsync());
        Assert.Contains("name=\"Command.Action\" value=\"cancelar\"", postHtml);

        Assert.Equal(eventsBefore, desk.EventLines().Count);
        Assert.Equal(auditBefore, harness.Audit.Lines().Count);
    }

    [Fact]
    public async Task Query_string_mover_abre_cita_de_otro_dia_en_dia_de_destino()
    {
        await using var harness = await ServerHarness.CreateAsync();
        harness.Time.Now = DateTimeOffset.Parse("2026-11-24T09:00:00-05:00");
        var adminClient = await AgendaAssistantWebHelper.CreateFirstAdminAsync(harness);
        var apptId = await AgendaAssistantWebHelper.SeedClientsAsync(adminClient);

        var getRes = await adminClient.GetAsync($"/agenda?dia=2026-11-26&mover={apptId}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        var html = WebUtility.HtmlDecode(await getRes.Content.ReadAsStringAsync());
        Assert.Contains("name=\"Command.Action\" value=\"reprogramar\"", html);
        Assert.Contains($"value=\"{apptId}\"", html);
    }

    [Fact]
    public async Task PickToMove_enlaces_solo_llevan_mover_dia_y_hora_sin_nombres()
    {
        await using var harness = await ServerHarness.CreateAsync();
        harness.Time.Now = DateTimeOffset.Parse("2026-11-24T09:00:00-05:00");
        var adminClient = await AgendaAssistantWebHelper.CreateFirstAdminAsync(harness);
        await AgendaAssistantWebHelper.SeedClientsAsync(adminClient);

        var getRes = await adminClient.GetAsync("/agenda");
        var token = ServerHarness.ExtractAntiforgeryToken(await getRes.Content.ReadAsStringAsync());

        var postContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-assistant",
            ["Assistant.Text"] = "mueve la cita de Ana al jueves a las 11",
            ["__RequestVerificationToken"] = token,
        });

        var postRes = await adminClient.PostAsync("/agenda", postContent);
        Assert.Equal(HttpStatusCode.OK, postRes.StatusCode);

        var postHtml = WebUtility.HtmlDecode(await postRes.Content.ReadAsStringAsync());
        var moveLinks = Regex.Matches(postHtml, @"href=""([^""]*mover=[^""]*)""")
            .Cast<Match>()
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.True(moveLinks.Count >= 2);
        foreach (var link in moveLinks)
        {
            Assert.Contains("dia=2026-11-26", link);
            Assert.Contains("hora=11:00", link);
            Assert.Matches(@"mover=\d+", link);
            Assert.DoesNotContain("Ana", link, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Gómez", link, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Pérez", link, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Parametro_hora_en_query_string_marca_hueco_y_hora_invalida_se_ignora()
    {
        await using var harness = await ServerHarness.CreateAsync();
        harness.Time.Now = DateTimeOffset.Parse("2026-11-24T09:00:00-05:00");
        var adminClient = await AgendaAssistantWebHelper.CreateFirstAdminAsync(harness);

        // Con hora válida HH:mm marca el hueco con class="preferred"
        var getResValid = await adminClient.GetAsync("/agenda?dia=2026-11-25&nueva=control&hora=10:00");
        Assert.Equal(HttpStatusCode.OK, getResValid.StatusCode);
        var htmlValid = await getResValid.Content.ReadAsStringAsync();
        Assert.Contains("class=\"preferred\"", htmlValid);

        // Con hora inválida se ignora sin error
        var getResInvalid = await adminClient.GetAsync("/agenda?dia=2026-11-25&nueva=control&hora=xx");
        Assert.Equal(HttpStatusCode.OK, getResInvalid.StatusCode);
        var htmlInvalid = await getResInvalid.Content.ReadAsStringAsync();
        Assert.DoesNotContain("class=\"preferred\"", htmlInvalid);
    }

    [Fact]
    public async Task PastDay_muestra_assistant_past_day_y_TimeTaken_assistant_time_taken()
    {
        await using var harness = await ServerHarness.CreateAsync();
        harness.Time.Now = DateTimeOffset.Parse("2026-11-24T09:00:00-05:00");
        var adminClient = await AgendaAssistantWebHelper.CreateFirstAdminAsync(harness);

        var getRes = await adminClient.GetAsync("/agenda");
        var token = ServerHarness.ExtractAntiforgeryToken(await getRes.Content.ReadAsStringAsync());

        // PastDay con fecha de hace 4 días (20/11)
        var postPast = await adminClient.PostAsync("/agenda", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-assistant",
            ["Assistant.Text"] = "cita para Ana Pérez el 20/11",
            ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.OK, postPast.StatusCode);
        var htmlPast = WebUtility.HtmlDecode(await postPast.Content.ReadAsStringAsync());
        Assert.Contains("Esa fecha ya pasó", htmlPast);

        // TimeTaken con hora fuera de horario para el miércoles 25 (tarde cerrada)
        var postTaken = await adminClient.PostAsync("/agenda", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-assistant",
            ["Assistant.Text"] = "cita para Ana Pérez mañana a las 15:00",
            ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.OK, postTaken.StatusCode);
        var htmlTaken = WebUtility.HtmlDecode(await postTaken.Content.ReadAsStringAsync());
        Assert.Contains("A esa hora no hay hueco", htmlTaken);
    }

    [Fact]
    public async Task Sin_agenda_manage_caja_no_ve_caja_y_POST_da_403()
    {
        await using var harness = await ServerHarness.CreateAsync();
        var adminClient = await AgendaAssistantWebHelper.CreateFirstAdminAsync(harness);
        await AgendaAssistantWebHelper.CreateUserAsync(harness, adminClient, "cajero", "Cajero 1", "caja", "contrasena123");

        var cashierClient = harness.CreateClient();
        await AgendaAssistantWebHelper.LoginAsync(cashierClient, "cajero", "contrasena123");

        var desk = harness.App.Services.GetRequiredService<AgendaDesk>();
        var eventsBefore = desk.EventLines().Count;
        var auditBefore = harness.Audit.Lines().Count;

        var getRes = await cashierClient.GetAsync("/agenda");
        var getHtml = await getRes.Content.ReadAsStringAsync();
        Assert.DoesNotContain("name=\"Assistant.Text\"", getHtml);

        var token = ServerHarness.ExtractAntiforgeryToken(getHtml);

        var postContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-assistant",
            ["Assistant.Text"] = "cita para Ana Pérez, control",
            ["__RequestVerificationToken"] = token,
        });

        var postRes = await cashierClient.PostAsync("/agenda", postContent);
        Assert.Equal(HttpStatusCode.Forbidden, postRes.StatusCode);

        Assert.Equal(eventsBefore, desk.EventLines().Count);
        Assert.Equal(auditBefore, harness.Audit.Lines().Count);
    }

    [Fact]
    public async Task Con_varias_personas_lista_todas_y_los_enlaces_solo_llevan_el_id()
    {
        await using var harness = await ServerHarness.CreateAsync();
        harness.Time.Now = DateTimeOffset.Parse("2026-11-24T09:00:00-05:00");
        var adminClient = await AgendaAssistantWebHelper.CreateFirstAdminAsync(harness);
        await AgendaAssistantWebHelper.SeedClientsAsync(adminClient);

        var getRes = await adminClient.GetAsync("/agenda");
        var token = ServerHarness.ExtractAntiforgeryToken(await getRes.Content.ReadAsStringAsync());

        var postContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "agenda-assistant",
            ["Assistant.Text"] = "cita para Ana",
            ["__RequestVerificationToken"] = token,
        });

        var postRes = await adminClient.PostAsync("/agenda", postContent);
        Assert.Equal(HttpStatusCode.OK, postRes.StatusCode);

        var postHtml = WebUtility.HtmlDecode(await postRes.Content.ReadAsStringAsync());
        Assert.Contains("Ana Gómez", postHtml);
        Assert.Contains("Ana Pérez", postHtml);

        // Los enlaces de la lista de personas para elegir solo llevan el id numérico
        var choicesMatches = Regex.Matches(postHtml, @"<a href=""([^""]+)""[^>]*>([^<]+)</a>");
        var clientLinks = choicesMatches
            .Cast<Match>()
            .Where(m => m.Groups[1].Value.Contains("contacto="))
            .ToList();

        Assert.True(clientLinks.Count >= 2);
        foreach (var link in clientLinks)
        {
            var href = link.Groups[1].Value;
            Assert.Matches(@"contacto=\d+", href);
            Assert.DoesNotContain("Ana", href, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Gómez", href, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Pérez", href, StringComparison.OrdinalIgnoreCase);
        }
    }
}
