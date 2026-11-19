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

public sealed class AgendaStoreTests
{
    private sealed class TestContext : IDisposable
    {
        public string DirectoryPath { get; }

        public string DbPath { get; }

        public PlatformDatabase Database { get; }

        public MachineKey Key { get; }

        public Sealer Sealer { get; }

        public byte[] PhoneKey { get; }

        public TestContext(MachineKey? customKey = null)
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), "aida-store-" + Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(DirectoryPath);
            DbPath = Path.Combine(DirectoryPath, "aida.db");
            Database = new PlatformDatabase(DbPath);
            Database.ApplyArmSchema(AgendaDeclarations.ArmId, AgendaSchema.Steps);

            var keyPath = Path.Combine(DirectoryPath, "machine.key");
            Key = customKey ?? MachineKey.OpenOrCreate(keyPath, new DevelopmentKeyProtector());
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
    public void Ida_y_vuelta_de_cliente_y_citas()
    {
        using var ctx = new TestContext();
        long clientId;
        long appointmentId;
        var start = DateTimeOffset.Parse("2026-11-16T10:00:00-05:00");

        using (var work = ctx.Begin())
        {
            var client = work.Store.AddClient("María Pérez", "3005550142");
            clientId = client.Id;
            var appointment = work.Store.Add(clientId, "control", "consultorio_1", start, 30, "prof-1");
            appointmentId = appointment.Id;
            work.Commit();
        }

        using (var work = ctx.Begin())
        {
            var client = work.Store.GetClient(clientId);
            Assert.Equal("María Pérez", client.Name);
            Assert.Equal("3005550142", client.Phone);

            var appointment = work.Store.Get(appointmentId);
            Assert.NotNull(appointment);
            Assert.Equal("control", appointment.Service);
            Assert.Equal("consultorio_1", appointment.Resource);
            Assert.Equal(start, appointment.Start);
            Assert.Equal(30, appointment.Minutes);
            Assert.Equal(AppointmentState.Booked, appointment.State);
            Assert.Equal("prof-1", appointment.CreatedBy);
            Assert.Equal("prof-1", appointment.UpdatedBy);
        }
    }

    [Fact]
    public void El_telefono_se_encuentra_con_otro_formato()
    {
        using var ctx = new TestContext();
        using (var work = ctx.Begin())
        {
            work.Store.AddClient("Juan Gómez", "300 555 0142");
            work.Commit();
        }

        using (var work = ctx.Begin())
        {
            var c1 = work.Store.ClientByPhone("3005550142");
            var c2 = work.Store.ClientByPhone("300-555-0142");
            var c3 = work.Store.ClientByPhone("300.555.0142");

            Assert.NotNull(c1);
            Assert.NotNull(c2);
            Assert.NotNull(c3);
            Assert.Equal("Juan Gómez", c1.Name);
            Assert.Equal("Juan Gómez", c2.Name);
            Assert.Equal("Juan Gómez", c3.Name);
        }
    }

    [Fact]
    public void Between_ordena_por_inicio_e_id()
    {
        using var ctx = new TestContext();
        long id1, id2, id3, id4;

        // Dos citas a la misma hora exacta en UTC con distinto desfase:
        // 10:00 -05:00 == 15:00 UTC
        // 15:00 +00:00 == 15:00 UTC
        var tSameUtc1 = DateTimeOffset.Parse("2026-11-16T10:00:00-05:00");
        var tSameUtc2 = DateTimeOffset.Parse("2026-11-16T15:00:00+00:00");
        var tEarlier = DateTimeOffset.Parse("2026-11-16T09:00:00-05:00");
        var tLater = DateTimeOffset.Parse("2026-11-16T11:00:00-05:00");

        using (var work = ctx.Begin())
        {
            var client = work.Store.AddClient("Cliente", "123");
            var a2 = work.Store.Add(client.Id, "s", "r2", tSameUtc1, 30, "u");
            var a1 = work.Store.Add(client.Id, "s", "r1", tEarlier, 30, "u");
            var a3 = work.Store.Add(client.Id, "s", "r1", tSameUtc2, 30, "u");
            var a4 = work.Store.Add(client.Id, "s", "r1", tLater, 30, "u");

            id1 = a1.Id;
            id2 = a2.Id;
            id3 = a3.Id;
            id4 = a4.Id;
            work.Commit();
        }

        using (var work = ctx.Begin())
        {
            var range = work.Store.Between(DateTimeOffset.Parse("2026-11-16T08:00:00-05:00"), DateTimeOffset.Parse("2026-11-16T10:30:00-05:00"));
            var ids = range.Select(a => a.Id).ToList();

            // Debe estar ordenado por start_utc (tEarlier primero, luego las dos de las 15:00 UTC por id)
            Assert.Equal([id1, id2, id3], ids);
            Assert.DoesNotContain(id4, ids);
        }
    }

    [Fact]
    public void La_base_no_contiene_nombre_ni_telefono_en_claro()
    {
        using var ctx = new TestContext();
        const string name = "Paciente Super Secreto";
        const string phone = "3119876543";

        using (var work = ctx.Begin())
        {
            work.Store.AddClient(name, phone);
            work.Commit();
        }

        SqliteConnection.ClearAllPools();

        var dbBytes = File.ReadAllBytes(ctx.DbPath);
        var nameBytes = Encoding.UTF8.GetBytes(name);
        var phoneBytes = Encoding.UTF8.GetBytes(phone);

        Assert.DoesNotContain(nameBytes, dbBytes);
        Assert.DoesNotContain(phoneBytes, dbBytes);

        var walPath = ctx.DbPath + "-wal";
        if (File.Exists(walPath))
        {
            var walBytes = File.ReadAllBytes(walPath);
            Assert.DoesNotContain(nameBytes, walBytes);
            Assert.DoesNotContain(phoneBytes, walBytes);
        }
    }

    [Fact]
    public void Otra_clave_no_encuentra_al_cliente()
    {
        using var ctx = new TestContext();
        using (var work = ctx.Begin())
        {
            work.Store.AddClient("Lucía", "3120001122");
            work.Commit();
        }

        // Abrimos con otra clave de máquina
        var otherKeyPath = Path.Combine(ctx.DirectoryPath, "other.key");
        var otherKey = MachineKey.OpenOrCreate(otherKeyPath, new DevelopmentKeyProtector());
        var otherSealer = new Sealer(otherKey);
        var otherPhoneKey = otherKey.Derive(AgendaStore.PhoneKeyPurpose);

        using var workOther = AgendaWork.Begin(ctx.Database, otherSealer, otherPhoneKey);
        Assert.Null(workOther.Store.ClientByPhone("3120001122"));
    }

    [Fact]
    public void La_clave_de_la_huella_no_es_la_que_sella()
    {
        var tempKeyPath = Path.Combine(Path.GetTempPath(), "aida-keys-test-" + Guid.NewGuid().ToString("n"), "machine.key");
        try
        {
            var machineKey = MachineKey.OpenOrCreate(tempKeyPath, new DevelopmentKeyProtector());
            var sealKey = machineKey.Derive(Sealer.Purpose);
            var phoneKey = machineKey.Derive(AgendaStore.PhoneKeyPurpose);

            Assert.NotEqual(sealKey, phoneKey);
        }
        finally
        {
            if (File.Exists(tempKeyPath))
            {
                Directory.Delete(Path.GetDirectoryName(tempKeyPath)!, true);
            }
        }
    }

    [Fact]
    public void Sin_commit_no_queda_nada()
    {
        using var ctx = new TestContext();
        using (var work = ctx.Begin())
        {
            var client = work.Store.AddClient("Fantasma", "999999");
            work.Store.Add(client.Id, "control", "consultorio_1", DateTimeOffset.Parse("2026-11-16T10:00:00-05:00"), 30, "u1");
            // No hacemos work.Commit()
        }

        using (var work = ctx.Begin())
        {
            Assert.Null(work.Store.ClientByPhone("999999"));
            var appointments = work.Store.Between(DateTimeOffset.Parse("2026-11-16T00:00:00-05:00"), DateTimeOffset.Parse("2026-11-16T23:59:59-05:00"));
            Assert.Empty(appointments);
        }
    }
}
