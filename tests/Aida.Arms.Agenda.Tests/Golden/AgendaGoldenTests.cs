using System.Globalization;
using System.Text.Json;
using Aida.Arms.Agenda.Actions;
using Aida.Arms.Agenda.Desk;
using Aida.Arms.Agenda.Setup;
using Aida.Arms.Agenda.Storage;
using Aida.Engine.Actions;
using Aida.Engine.Events;
using Aida.Engine.Ontology;
using Aida.Platform.Keys;
using Aida.Platform.Packs;
using Aida.Platform.Sealing;
using Aida.Platform.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aida.Arms.Agenda.Tests.Golden;

public sealed class AgendaGoldenTests
{
    [Theory]
    [Trait("Category", "Golden")]
    [InlineData("consultorio-dia")]
    [InlineData("consultorio-bordes")]
    [InlineData("peluqueria-sabado")]
    [InlineData("consultorio-bloqueos")]
    [InlineData("consultorio-edicion")]
    public void El_oraculo_de_la_agenda_coincide_con_la_01(string caseName)
    {
        var repoRoot = FindRepoRoot();
        var casesPath = Path.Combine(repoRoot, "tests", "golden", "agenda", "cases.jsonl");
        Assert.True(File.Exists(casesPath), $"No existe el archivo de casos dorados: {casesPath}");

        var lines = File.ReadAllLines(casesPath);
        JsonElement targetCase = default;
        var found = false;
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var doc = JsonDocument.Parse(line);
            if (doc.RootElement.GetProperty("name").GetString() == caseName)
            {
                targetCase = doc.RootElement.Clone();
                found = true;
                break;
            }
        }

        Assert.True(found, $"No se encontró el caso dorado '{caseName}'");

        var template = targetCase.GetProperty("template").GetString()!;
        var setupPath = Path.Combine(repoRoot, "packs", "agenda", template, "agenda.yaml");
        var armPath = Path.Combine(repoRoot, "packs", "agenda", "arm.yaml");

        var setup = AgendaSetupLoader.Parse(PlatformYaml.ReadRoot(setupPath), setupPath);
        var manifest = AgendaManifest.Load(armPath);

        var tempDir = Path.Combine(Path.GetTempPath(), "aida-golden-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var dbPath = Path.Combine(tempDir, "aida.db");
            var db = new PlatformDatabase(dbPath);
            db.ApplyArmSchema(AgendaDeclarations.ArmId, AgendaSchema.Steps);

            var keyPath = Path.Combine(tempDir, "machine.key");
            var machineKey = MachineKey.OpenOrCreate(keyPath, new DevelopmentKeyProtector());
            var sealer = new Sealer(machineKey);
            var phoneKey = machineKey.Derive(AgendaStore.PhoneKeyPurpose);

            var time = new ManualTime();
            var log = new EventLog();
            long tick = 0;
            long NextTick() => ++tick;

            foreach (var reqElem in targetCase.GetProperty("requests").EnumerateArray())
            {
                var action = reqElem.GetProperty("action").GetString()!;
                var nowText = reqElem.GetProperty("now").GetString()!;
                time.Now = DateTimeOffset.Parse(nowText, CultureInfo.InvariantCulture);

                var args = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["by"] = "u1",
                };
                foreach (var prop in reqElem.GetProperty("arguments").EnumerateObject())
                {
                    args[prop.Name] = prop.Value.GetString()!;
                }

                using var work = AgendaWork.Begin(db, sealer, phoneKey);
                var tool = new AgendaTool(work.Store, setup, time);
                var gate = new ActionGate(manifest.Actions, Reversibility.Reversible, tool, log, new AgentId("agenda"), NextTick);
                gate.Submit(new ActionRequest(new ActionId(action), args, "u1"));
                work.Commit();
            }

            // 5. Comparaciones
            // 5.1 Eventos
            var actualEvents = log.Select(e => e.ToJson()).ToList();
            var expectedEvents = targetCase.GetProperty("events").EnumerateArray().Select(e => e.GetString()!).ToList();
            Assert.Equal(expectedEvents.Count, actualEvents.Count);
            for (var i = 0; i < expectedEvents.Count; i++)
            {
                Assert.True(expectedEvents[i] == actualEvents[i],
                    $"Caso '{caseName}', evento {i + 1}:\nEsperado:\n{expectedEvents[i]}\nObtenido:\n{actualEvents[i]}");
            }

            // 5.2 Citas
            var day = DateOnly.Parse(targetCase.GetProperty("day").GetString()!, CultureInfo.InvariantCulture);
            var dayStart = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), time.LocalTimeZone.BaseUtcOffset);

            using var queryWork = AgendaWork.Begin(db, sealer, phoneKey);
            var actualAppointments = queryWork.Store.Between(dayStart.AddDays(-7), dayStart.AddDays(7));
            var expectedAppointments = targetCase.GetProperty("appointments").EnumerateArray().ToList();
            Assert.Equal(expectedAppointments.Count, actualAppointments.Count);
            for (var i = 0; i < expectedAppointments.Count; i++)
            {
                var app = actualAppointments[i];
                var client = queryWork.Store.GetClient(app.ClientId);
                var exp = expectedAppointments[i];

                Assert.True(exp.GetProperty("id").GetInt64() == app.Id, $"Caso '{caseName}', cita {i + 1} id distinto");
                Assert.True(exp.GetProperty("name").GetString() == client.Name, $"Caso '{caseName}', cita {i + 1} name distinto");
                Assert.True(exp.GetProperty("phone").GetString() == client.Phone, $"Caso '{caseName}', cita {i + 1} phone distinto");
                Assert.True(exp.GetProperty("service").GetString() == app.Service, $"Caso '{caseName}', cita {i + 1} service distinto");
                Assert.True(exp.GetProperty("resource").GetString() == app.Resource, $"Caso '{caseName}', cita {i + 1} resource distinto");
                Assert.True(exp.GetProperty("start").GetString() == StartArgument.Format(app.Start), $"Caso '{caseName}', cita {i + 1} start distinto");
                Assert.True(exp.GetProperty("minutes").GetInt32() == app.Minutes, $"Caso '{caseName}', cita {i + 1} minutes distinto");
                Assert.True(exp.GetProperty("state").GetString() == AppointmentStates.ToWire(app.State), $"Caso '{caseName}', cita {i + 1} state distinto");
                Assert.True(exp.GetProperty("created_by").GetString() == app.CreatedBy, $"Caso '{caseName}', cita {i + 1} created_by distinto");
                Assert.True(exp.GetProperty("updated_by").GetString() == app.UpdatedBy, $"Caso '{caseName}', cita {i + 1} updated_by distinto");
            }

            // 5.3 Huecos libres
            var expectedFree = targetCase.GetProperty("free");
            foreach (var service in setup.Services)
            {
                var actualSlots = FreeSlots.For(queryWork.Store, setup, day, service.Id, time.Now);
                var actualPairs = actualSlots.Select(s => new[] { s.Resource, StartArgument.Format(s.Start) }).ToList();

                var expSlots = expectedFree.GetProperty(service.Id).EnumerateArray()
                    .Select(slotElem => new[] { slotElem[0].GetString()!, slotElem[1].GetString()! })
                    .ToList();

                Assert.True(expSlots.Count == actualPairs.Count,
                    $"Caso '{caseName}', servicio '{service.Id}': cantidad de huecos distinta ({expSlots.Count} vs {actualPairs.Count})");

                for (var i = 0; i < expSlots.Count; i++)
                {
                    Assert.True(expSlots[i][0] == actualPairs[i][0] && expSlots[i][1] == actualPairs[i][1],
                        $"Caso '{caseName}', servicio '{service.Id}', hueco {i + 1}:\nEsperado: {expSlots[i][0]} {expSlots[i][1]}\nObtenido: {actualPairs[i][0]} {actualPairs[i][1]}");
                }
            }

            // 5.4 Bloqueos
            var actualBlocks = queryWork.Store.BlocksBetween(dayStart.AddDays(-7), dayStart.AddDays(7));
            var expectedBlocks = targetCase.GetProperty("blocks").EnumerateArray().ToList();
            Assert.Equal(expectedBlocks.Count, actualBlocks.Count);
            for (var i = 0; i < expectedBlocks.Count; i++)
            {
                var block = actualBlocks[i];
                var exp = expectedBlocks[i];

                Assert.True(exp.GetProperty("id").GetInt64() == block.Id, $"Caso '{caseName}', bloqueo {i + 1} id distinto");
                Assert.True(exp.GetProperty("resource").GetString() == block.Resource, $"Caso '{caseName}', bloqueo {i + 1} resource distinto");
                Assert.True(exp.GetProperty("start").GetString() == StartArgument.Format(block.Start), $"Caso '{caseName}', bloqueo {i + 1} start distinto");
                Assert.True(exp.GetProperty("end").GetString() == StartArgument.Format(block.End), $"Caso '{caseName}', bloqueo {i + 1} end distinto");
                Assert.True(exp.GetProperty("reason").GetString() == block.Reason, $"Caso '{caseName}', bloqueo {i + 1} reason distinto");
                Assert.True(exp.GetProperty("created_by").GetString() == block.CreatedBy, $"Caso '{caseName}', bloqueo {i + 1} created_by distinto");
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempDir))
            {
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch
                {
                }
            }
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aida.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }
}
