using Aida.Arms.Agenda.Desk;
using Aida.Arms.Agenda.Setup;
using Aida.Arms.Agenda.Storage;
using Aida.Engine.Ontology;
using Aida.Platform.Audit;
using Aida.Platform.Identity;
using Aida.Platform.Keys;
using Aida.Platform.Packs;
using Aida.Platform.People;
using Aida.Platform.Roles;
using Aida.Platform.Sealing;
using Aida.Platform.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aida.Arms.Agenda.Tests.Storage;

public sealed class AgendaMigrationTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aida.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }

    private static (PlatformDatabase db, MachineKey key, Sealer sealer, string dir) CreateSchemaV2Database(bool corruptBlob = false)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "aida-migracion-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(tempDir);
        var dbPath = Path.Combine(tempDir, "aida.db");

        var db = new PlatformDatabase(dbPath);
        var keyPath = Path.Combine(tempDir, "machine.key");
        var key = MachineKey.OpenOrCreate(keyPath, new DevelopmentKeyProtector());
        var sealer = new Sealer(key);

        // Crear agenda en esquema 2 (pasos 1 y 2 de SQL)
        db.ApplyArmSchema(AgendaDeclarations.ArmId, [ArmSchemaStep.Sql(AgendaSchema.Version1), ArmSchemaStep.Sql(AgendaSchema.Version2)]);

        // Poblar con contactos (1, 3 y 4 con hueco en el 2 para M1) y citas en cliente 3 y 4
        using (var conn = new SqliteConnection($"Data Source={dbPath};Pooling=False"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();

            if (corruptBlob)
            {
                cmd.CommandText = "INSERT INTO agenda_clients (id, name, phone, phone_index) VALUES (1, X'00010203', X'00010203', 'idx1');";
                cmd.ExecuteNonQuery();
            }
            else
            {
                cmd.CommandText = """
                    INSERT INTO agenda_clients (id, name, phone, phone_index) VALUES
                        (1, $name1, $phone1, 'idx1'),
                        (3, $name3, $phone3, 'idx3'),
                        (4, $name4, $phone4, 'idx4');
                    INSERT INTO agenda_appointments (id, client, service, resource, start, start_utc, minutes, state, created_by, updated_by) VALUES
                        (101, 3, 'control', 'consultorio_1', '2026-11-16T09:00:00-05:00', 1763298000, 30, 'booked', 'admin', 'admin'),
                        (102, 4, 'control', 'consultorio_1', '2026-11-16T10:00:00-05:00', 1763301600, 30, 'booked', 'admin', 'admin');
                    """;
                cmd.Parameters.AddWithValue("$name1", sealer.Seal("Ana"));
                cmd.Parameters.AddWithValue("$phone1", sealer.Seal("3101111111"));
                cmd.Parameters.AddWithValue("$name3", sealer.Seal("Carlos"));
                cmd.Parameters.AddWithValue("$phone3", sealer.Seal("3103333333"));
                cmd.Parameters.AddWithValue("$name4", sealer.Seal("Diana"));
                cmd.Parameters.AddWithValue("$phone4", sealer.Seal("3104444444"));
                cmd.ExecuteNonQuery();
            }
        }

        return (db, key, sealer, tempDir);
    }

    private static AgendaDesk BuildDesk(PlatformDatabase db, MachineKey key)
    {
        var repoRoot = FindRepoRoot();
        var rolesPath = Path.Combine(repoRoot, "packs", "platform", "roles.yaml");
        var roles = RoleCatalog.Load(rolesPath, AgendaDeclarations.Permissions);
        var time = TimeProvider.System;
        var audit = new AuditLog(db, time);
        var hasher = new PasswordHasher(1);
        var users = new UserStore(db, audit, roles, hasher, time);

        var consultorioPath = Path.Combine(repoRoot, "packs", "agenda", "consultorio", "agenda.yaml");
        var setup = AgendaSetupLoader.Parse(PlatformYaml.ReadRoot(consultorioPath), consultorioPath);
        var armPath = Path.Combine(repoRoot, "packs", "agenda", "arm.yaml");
        var catalog = AgendaManifest.Load(armPath).Actions;
        var people = PersonCatalog.Load(Path.Combine(repoRoot, "packs", "platform", "people.yaml"));

        return new AgendaDesk(db, audit, roles, users, setup, people, catalog, Reversibility.Reversible, key, time);
    }

    [Fact]
    public void Migracion_v2_a_v3_conserva_ids_mueve_personas_y_actualiza_esquema()
    {
        var (db, key, sealer, tempDir) = CreateSchemaV2Database(corruptBlob: false);
        var personKeys = PersonKeys.From(key);
        try
        {
            var desk = BuildDesk(db, key);

            // 1. people tiene los ids 1, 3 y 4 con los mismos nombres y teléfonos (conservando el hueco del 2)
            using (var work = AgendaWork.Begin(db, sealer, personKeys))
            {
                var p1 = work.People.Find(1);
                Assert.NotNull(p1);
                Assert.Equal("Ana", p1.Name);
                Assert.Equal("3101111111", p1.Phone);

                Assert.Null(work.People.Find(2));

                var p3 = work.People.Find(3);
                Assert.NotNull(p3);
                Assert.Equal("Carlos", p3.Name);
                Assert.Equal("3103333333", p3.Phone);

                var p4 = work.People.Find(4);
                Assert.NotNull(p4);
                Assert.Equal("Diana", p4.Name);
                Assert.Equal("3104444444", p4.Phone);

                // 2. Las citas apuntan a los mismos ids exactos
                var app101 = work.Store.Get(101);
                Assert.NotNull(app101);
                Assert.Equal(3, app101.ClientId);

                var app102 = work.Store.Get(102);
                Assert.NotNull(app102);
                Assert.Equal(4, app102.ClientId);
            }

            // 3. agenda_clients no existe y arm_schema es 3
            using (var conn = new SqliteConnection($"Data Source={Path.Combine(tempDir, "aida.db")};Pooling=False"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='agenda_clients';";
                Assert.Equal(0L, (long)cmd.ExecuteScalar()!);

                using var cmdVer = conn.CreateCommand();
                cmdVer.CommandText = "SELECT version FROM arm_schema WHERE arm='agenda';";
                Assert.Equal(3L, (long)cmdVer.ExecuteScalar()!);
            }

            // 4. Search("3103333333") encuentra al contacto
            var found = desk.Search("3103333333");
            var item = Assert.Single(found);
            Assert.Equal(3, item.Id);
            Assert.Equal("Carlos", item.Name);

            // 5. Construir dos veces es idempotente y no cambia nada
            var desk2 = BuildDesk(db, key);
            Assert.NotNull(desk2);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void Blob_corrupto_en_contacto_hace_rollback_y_base_queda_en_esquema_2()
    {
        var (db, key, _, tempDir) = CreateSchemaV2Database(corruptBlob: true);
        try
        {
            // Construir AgendaDesk debe lanzar al intentar abrir el blob corrupto
            Assert.ThrowsAny<Exception>(() => BuildDesk(db, key));

            // La base sigue en el esquema 2 y agenda_clients permanece intacta
            using var conn = new SqliteConnection($"Data Source={Path.Combine(tempDir, "aida.db")};Pooling=False");
            conn.Open();

            using var cmdTable = conn.CreateCommand();
            cmdTable.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='agenda_clients';";
            Assert.Equal(1L, (long)cmdTable.ExecuteScalar()!);

            using var cmdVer = conn.CreateCommand();
            cmdVer.CommandText = "SELECT version FROM arm_schema WHERE arm='agenda';";
            Assert.Equal(2L, (long)cmdVer.ExecuteScalar()!);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
