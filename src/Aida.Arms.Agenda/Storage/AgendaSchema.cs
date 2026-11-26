using Aida.Platform.People;
using Aida.Platform.Sealing;
using Aida.Platform.Storage;
using Microsoft.Data.Sqlite;

namespace Aida.Arms.Agenda.Storage;

/// <summary>Las tablas de la agenda, por versión (ADR 0032), y la migración que saca a las personas a la plataforma (ADR 0038).</summary>
/// <remarks>Las de la 0.1 (<c>store.py:110-140</c>), con tres cambios: el prefijo <c>agenda_</c>, para que ningún otro brazo
/// las pise; <c>start_utc</c>, para buscar por rango con un índice; y, desde la versión 2, la tabla de los bloqueos
/// (<c>store.py:131-138</c>). La 0.1 filtraba en Python porque las fechas con distinto desfase no se comparan como texto
/// (<c>store.py:299</c>). La versión 3 saca a las personas (<c>agenda_clients</c>) a la plataforma, y su migración es el único
/// sitio que nombra esa tabla: el resto del brazo habla con <c>PersonStore</c>.</remarks>
internal static class AgendaSchema
{
    /// <summary>La versión de este esquema.</summary>
    internal const int Version = 3;

    internal const string Version1 = """
        CREATE TABLE agenda_clients (
            id INTEGER PRIMARY KEY,
            name BLOB NOT NULL,
            phone BLOB NOT NULL,
            phone_index TEXT NOT NULL UNIQUE
        ) STRICT;
        CREATE TABLE agenda_appointments (
            id INTEGER PRIMARY KEY,
            client INTEGER NOT NULL REFERENCES agenda_clients(id),
            service TEXT NOT NULL,
            resource TEXT NOT NULL,
            start TEXT NOT NULL,
            start_utc INTEGER NOT NULL,
            minutes INTEGER NOT NULL CHECK (minutes > 0),
            state TEXT NOT NULL CHECK (state IN ('booked', 'cancelled', 'attended', 'no_show')),
            created_by TEXT NOT NULL,
            updated_by TEXT NOT NULL
        ) STRICT;
        CREATE INDEX agenda_appointments_start ON agenda_appointments(start_utc);
        """;

    internal const string Version2 = """
        CREATE TABLE agenda_blocks (
            id INTEGER PRIMARY KEY,
            resource TEXT NOT NULL,
            start TEXT NOT NULL,
            start_utc INTEGER NOT NULL,
            ends TEXT NOT NULL,
            ends_utc INTEGER NOT NULL,
            reason BLOB NOT NULL,
            created_by TEXT NOT NULL,
            CHECK (ends_utc > start_utc)
        ) STRICT;
        CREATE INDEX agenda_blocks_start ON agenda_blocks(start_utc);
        """;

    /// <summary>La parte en SQL del paso 3: rehace las citas sin la clave a <c>agenda_clients</c> y borra esa tabla.</summary>
    /// <remarks>Va después de copiar las personas: si se borrara antes, las citas quedarían sin persona. Todo el paso va en
    /// una transacción, así que un fallo a mitad no deja ni las personas ni las citas a medias.</remarks>
    internal const string Version3 = """
        CREATE TABLE agenda_appointments_v3 (
            id INTEGER PRIMARY KEY,
            client INTEGER NOT NULL,
            service TEXT NOT NULL,
            resource TEXT NOT NULL,
            start TEXT NOT NULL,
            start_utc INTEGER NOT NULL,
            minutes INTEGER NOT NULL CHECK (minutes > 0),
            state TEXT NOT NULL CHECK (state IN ('booked', 'cancelled', 'attended', 'no_show')),
            created_by TEXT NOT NULL,
            updated_by TEXT NOT NULL
        ) STRICT;
        INSERT INTO agenda_appointments_v3 SELECT id, client, service, resource, start, start_utc, minutes, state, created_by, updated_by FROM agenda_appointments;
        DROP TABLE agenda_appointments;
        ALTER TABLE agenda_appointments_v3 RENAME TO agenda_appointments;
        CREATE INDEX agenda_appointments_start ON agenda_appointments(start_utc);
        CREATE INDEX agenda_appointments_client ON agenda_appointments(client);
        DROP TABLE agenda_clients;
        """;

    /// <summary>Los pasos del esquema de la agenda, en orden.</summary>
    /// <param name="sealer">El sellador del equipo, con el que se abren los contactos de la versión 2.</param>
    /// <param name="personKeys">Las claves de la plataforma, con las que se calculan las huellas de las personas.</param>
    /// <returns>Los tres pasos: dos de SQL y la migración.</returns>
    /// <remarks>El paso 3 no necesita la clave de la huella de la versión 2: los nombres y los teléfonos se abren con el
    /// sellador, y las huellas nuevas salen del teléfono abierto. Por eso ya no hay clave de la agenda.</remarks>
    internal static IReadOnlyList<ArmSchemaStep> Steps(Sealer sealer, PersonKeys personKeys) =>
    [
        ArmSchemaStep.Sql(Version1),
        ArmSchemaStep.Sql(Version2),
        ArmSchemaStep.Code((connection, transaction) => MigrateContacts(connection, transaction, sealer, personKeys)),
    ];

    private static void MigrateContacts(SqliteConnection connection, SqliteTransaction transaction, Sealer sealer, PersonKeys personKeys)
    {
        var contacts = ReadContacts(connection, transaction, sealer);
        var people = new PersonStore(connection, transaction, sealer, personKeys);
        foreach (var (id, name, phone) in contacts)
        {
            people.Import(id, name, phone);
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Version3;
        command.ExecuteNonQuery();
    }

    private static List<(long Id, string Name, string Phone)> ReadContacts(SqliteConnection connection, SqliteTransaction transaction, Sealer sealer)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT id, name, phone FROM agenda_clients ORDER BY id;";
        using var reader = command.ExecuteReader();
        var contacts = new List<(long Id, string Name, string Phone)>();
        while (reader.Read())
        {
            contacts.Add((reader.GetInt64(0), sealer.Open((byte[])reader[1]), sealer.Open((byte[])reader[2])));
        }

        return contacts;
    }
}
