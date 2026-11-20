using System.Text;
using Aida.Arms.Agenda.Actions;
using Aida.Arms.Agenda.Desk;
using Aida.Arms.Agenda.Storage;
using Aida.Platform.Keys;
using Aida.Platform.Sealing;
using Aida.Platform.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aida.Arms.Agenda.Tests.Storage;

public sealed class AgendaBlockStoreTests
{
    private sealed class BlockTestContext : IDisposable
    {
        public string DirectoryPath { get; }
        public string DbPath { get; }
        public PlatformDatabase Database { get; }
        public MachineKey Key { get; }
        public Sealer Sealer { get; }
        public byte[] PhoneKey { get; }

        public BlockTestContext(bool applyAllSteps = true)
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), "aida-block-store-" + Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(DirectoryPath);
            DbPath = Path.Combine(DirectoryPath, "aida.db");
            Database = new PlatformDatabase(DbPath);
            if (applyAllSteps)
            {
                Database.ApplyArmSchema(AgendaDeclarations.ArmId, AgendaSchema.Steps);
            }

            var keyPath = Path.Combine(DirectoryPath, "machine.key");
            Key = MachineKey.OpenOrCreate(keyPath, new DevelopmentKeyProtector());
            Sealer = new Sealer(Key);
            PhoneKey = Key.Derive(AgendaStore.PhoneKeyPurpose);
        }

        public AgendaWork Begin() => AgendaWork.Begin(Database, Sealer, PhoneKey);

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            try
            {
                if (Directory.Exists(DirectoryPath))
                {
                    Directory.Delete(DirectoryPath, true);
                }
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void BlocksBetween_Bordes_orden_y_bloqueo_multidia()
    {
        using var ctx = new BlockTestContext();
        using var work = ctx.Begin();

        var t10 = DateTimeOffset.Parse("2026-11-16T10:00:00-05:00");
        var t11 = DateTimeOffset.Parse("2026-11-16T11:00:00-05:00");
        var t12 = DateTimeOffset.Parse("2026-11-16T12:00:00-05:00");

        // Bloqueo 1: 10:00 a 11:00 recurso 2
        var b1 = work.Store.AddBlock("r2", t10, t11, "Motivo 1", "u1");
        // Bloqueo 2: 10:00 a 11:00 recurso 1 (mismo inicio, id mayor)
        var b2 = work.Store.AddBlock("r1", t10, t11, "Motivo 2", "u1");
        // Bloqueo 3: multidia, del 15 al 18
        var bMulti = work.Store.AddBlock(string.Empty, t10.AddDays(-1), t10.AddDays(2), "Vacaciones", "admin");

        work.Commit();

        using var query = ctx.Begin();
        // Consulta exacta de 10:00 a 11:00
        var blocks = query.Store.BlocksBetween(t10, t11);
        Assert.Equal(3, blocks.Count);
        // Orden: bMulti empieza ayer (menor inicio), luego b1 (id menor), luego b2 (id mayor)
        Assert.Equal(bMulti.Id, blocks[0].Id);
        Assert.Equal(b1.Id, blocks[1].Id);
        Assert.Equal(b2.Id, blocks[2].Id);

        // Borde izquierdo tocando: [09:00, 10:00) no incluye b1 ni b2 (pero sí bMulti)
        var leftEdge = query.Store.BlocksBetween(t10.AddHours(-1), t10);
        Assert.Single(leftEdge);
        Assert.Equal(bMulti.Id, leftEdge[0].Id);

        // Borde derecho tocando: [11:00, 12:00) no incluye b1 ni b2 (pero sí bMulti)
        var rightEdge = query.Store.BlocksBetween(t11, t12);
        Assert.Single(rightEdge);
        Assert.Equal(bMulti.Id, rightEdge[0].Id);
    }

    [Fact]
    public void RemoveBlock_Existente_devuelve_true_inexistente_false()
    {
        using var ctx = new BlockTestContext();
        using var work = ctx.Begin();

        var t10 = DateTimeOffset.Parse("2026-11-16T10:00:00-05:00");
        var block = work.Store.AddBlock("r1", t10, t10.AddHours(1), "Capacitación", "u1");
        work.Commit();

        using var workRemove = ctx.Begin();
        Assert.True(workRemove.Store.RemoveBlock(block.Id));
        Assert.False(workRemove.Store.RemoveBlock(block.Id));
        Assert.False(workRemove.Store.RemoveBlock(9999));
        workRemove.Commit();

        using var query = ctx.Begin();
        Assert.Empty(query.Store.BlocksBetween(t10.AddDays(-1), t10.AddDays(1)));
    }

    [Fact]
    public void Motivo_sellado_no_en_utf8_plano()
    {
        using var ctx = new BlockTestContext();
        var secretReason = "Confidencial: Cirugía del paciente Juan Pérez";

        using (var work = ctx.Begin())
        {
            var t10 = DateTimeOffset.Parse("2026-11-16T10:00:00-05:00");
            work.Store.AddBlock("r1", t10, t10.AddHours(1), secretReason, "u1");
            work.Commit();
        }

        // Inspeccionar la base cruda con SQL directo
        using var conn = new SqliteConnection($"Data Source={ctx.DbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT reason FROM agenda_blocks WHERE id = 1";
        var rawBlob = (byte[])cmd.ExecuteScalar()!;

        var rawString = Encoding.UTF8.GetString(rawBlob);
        Assert.DoesNotContain(secretReason, rawString);
        Assert.DoesNotContain("Juan Pérez", rawString);

        // Pero recuperado con el store está abierto
        using var query = ctx.Begin();
        var block = Assert.Single(query.Store.BlocksBetween(DateTimeOffset.MinValue, DateTimeOffset.MaxValue));
        Assert.Equal(secretReason, block.Reason);
    }

    [Fact]
    public void Migracion_v1_a_v2_con_Steps_conserva_citas_y_agrega_bloqueos()
    {
        using var ctx = new BlockTestContext(applyAllSteps: false);

        // Aplicar solo Version1
        ctx.Database.ApplyArmSchema(AgendaDeclarations.ArmId, [AgendaSchema.Version1]);

        long appId;
        var start = DateTimeOffset.Parse("2026-11-16T08:00:00-05:00");
        using (var workV1 = ctx.Begin())
        {
            var client = workV1.Store.AddClient("Carlos", "3001234567");
            var app = workV1.Store.Add(client.Id, "control", "c1", start, 30, "u1");
            appId = app.Id;
            workV1.Commit();
        }

        // Subir a Version2 aplicando todos los Steps
        ctx.Database.ApplyArmSchema(AgendaDeclarations.ArmId, AgendaSchema.Steps);

        // Verificar que la cita y cliente siguen intactos
        using (var workV2 = ctx.Begin())
        {
            var app = workV2.Store.Get(appId);
            Assert.NotNull(app);
            Assert.Equal("control", app.Service);
            Assert.Equal("c1", app.Resource);

            var client = workV2.Store.GetClient(app.ClientId);
            Assert.Equal("Carlos", client.Name);
            Assert.Equal("3001234567", client.Phone);

            // Y ahora se pueden guardar bloqueos
            var block = workV2.Store.AddBlock("c1", start.AddHours(2), start.AddHours(3), "Reunión", "u1");
            Assert.True(block.Id > 0);
            workV2.Commit();
        }

        using (var query = ctx.Begin())
        {
            var blocks = query.Store.BlocksBetween(start, start.AddDays(1));
            Assert.Single(blocks);
        }
    }

    [Fact]
    public void Update_con_servicio_y_minutos()
    {
        using var ctx = new BlockTestContext();
        using var work = ctx.Begin();

        var start = DateTimeOffset.Parse("2026-11-16T08:00:00-05:00");
        var client = work.Store.AddClient("Diana", "3009876543");
        var app = work.Store.Add(client.Id, "control", "c1", start, 30, "u1");

        var updated = work.Store.Update(app.Id, "u2", service: "procedimiento", minutes: 60);
        Assert.Equal("procedimiento", updated.Service);
        Assert.Equal(60, updated.Minutes);
        Assert.Equal("u2", updated.UpdatedBy);
        Assert.Equal("c1", updated.Resource);

        work.Commit();

        using var query = ctx.Begin();
        var reloaded = query.Store.Get(app.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("procedimiento", reloaded.Service);
        Assert.Equal(60, reloaded.Minutes);
        Assert.Equal("u2", reloaded.UpdatedBy);
    }
}
