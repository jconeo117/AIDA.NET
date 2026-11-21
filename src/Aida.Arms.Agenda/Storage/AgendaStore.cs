using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aida.Platform.Sealing;
using Microsoft.Data.Sqlite;

namespace Aida.Arms.Agenda.Storage;

/// <summary>Las citas, las personas y los bloqueos de la agenda, dentro de una transacción.</summary>
/// <remarks>
/// <para>Es <c>SqliteAgendaStore</c> de la 0.1 (<c>store.py:147-305</c>), con sus bloqueos y las consultas de la ficha.
/// Trabaja dentro de la transacción de quien lo crea: el cambio y su hecho de auditoría se confirman juntos
/// (ADR 0032).</para>
/// <para>El nombre y el teléfono van sellados (ADR 0029). El teléfono lleva además una huella HMAC con una clave derivada
/// distinta de la que sella, para buscarlo exacto sin abrir a todos (<c>store.py:150, 163-166</c>). El motivo de un bloqueo
/// va sellado por la misma razón: es texto libre y puede nombrar a un paciente.</para>
/// </remarks>
internal sealed class AgendaStore
{
    internal const string PhoneKeyPurpose = "aida-agenda-phone";
    internal const string StartFormat = "yyyy-MM-dd'T'HH:mm:sszzz";

    private const string _select = "SELECT id, client, service, resource, start, minutes, state, created_by, updated_by FROM agenda_appointments";

    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction _transaction;
    private readonly Sealer _sealer;
    private readonly byte[] _phoneKey;

    internal AgendaStore(SqliteConnection connection, SqliteTransaction transaction, Sealer sealer, byte[] phoneKey)
    {
        _connection = connection;
        _transaction = transaction;
        _sealer = sealer;
        _phoneKey = phoneKey;
    }

    internal Client? ClientByPhone(string phone)
    {
        using var command = Command("SELECT id, name, phone FROM agenda_clients WHERE phone_index = $index;");
        command.Parameters.AddWithValue("$index", Index(phone));
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadClient(reader) : null;
    }

    internal Client AddClient(string name, string phone)
    {
        using var command = Command("INSERT INTO agenda_clients (name, phone, phone_index) VALUES ($name, $phone, $index) RETURNING id;");
        command.Parameters.AddWithValue("$name", _sealer.Seal(name));
        command.Parameters.AddWithValue("$phone", _sealer.Seal(phone));
        command.Parameters.AddWithValue("$index", Index(phone));
        return new Client((long)command.ExecuteScalar()!, name, phone);
    }

    internal void UpdateClient(long client, string name, string phone)
    {
        using var command = Command("UPDATE agenda_clients SET name = $name, phone = $phone, phone_index = $index WHERE id = $id;");
        command.Parameters.AddWithValue("$name", _sealer.Seal(name));
        command.Parameters.AddWithValue("$phone", _sealer.Seal(phone));
        command.Parameters.AddWithValue("$index", Index(phone));
        command.Parameters.AddWithValue("$id", client);
        if (command.ExecuteNonQuery() == 0)
        {
            throw new KeyNotFoundException($"no hay contacto {client}");
        }
    }

    /// <summary>Un contacto por su id, o <see langword="null"/> si no existe.</summary>
    internal Client? FindClient(long client)
    {
        using var command = Command("SELECT id, name, phone FROM agenda_clients WHERE id = $id;");
        command.Parameters.AddWithValue("$id", client);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadClient(reader) : null;
    }

    internal Client GetClient(long client) => FindClient(client) ?? throw new KeyNotFoundException($"no hay contacto {client}");

    /// <summary>Todos los contactos, ya abiertos, por id (<c>store.py:316-319</c>).</summary>
    /// <remarks>Los nombres van sellados: buscar por nombre es abrirlos todos. A la escala de un mostrador cabe en memoria,
    /// como en la 0.1.</remarks>
    internal IReadOnlyList<Client> Clients()
    {
        using var command = Command("SELECT id, name, phone FROM agenda_clients ORDER BY id;");
        using var reader = command.ExecuteReader();
        var found = new List<Client>();
        while (reader.Read())
        {
            found.Add(ReadClient(reader));
        }

        return found;
    }

    /// <summary>Las citas de un contacto, en orden de inicio y, a igualdad, de id (<c>store.py:321-327</c>).</summary>
    /// <param name="client">El contacto.</param>
    /// <returns>Todas sus citas, también las canceladas.</returns>
    internal IReadOnlyList<Appointment> AppointmentsFor(long client)
    {
        using var command = Command($"{_select} WHERE client = $client ORDER BY start_utc, id;");
        command.Parameters.AddWithValue("$client", client);
        using var reader = command.ExecuteReader();
        var found = new List<Appointment>();
        while (reader.Read())
        {
            found.Add(ReadAppointment(reader));
        }

        return found;
    }

    internal Appointment Add(long client, string service, string resource, DateTimeOffset start, int minutes, string by)
    {
        using var command = Command("""
            INSERT INTO agenda_appointments (client, service, resource, start, start_utc, minutes, state, created_by, updated_by)
            VALUES ($client, $service, $resource, $start, $start_utc, $minutes, 'booked', $by, $by) RETURNING id;
            """);
        command.Parameters.AddWithValue("$client", client);
        command.Parameters.AddWithValue("$service", service);
        command.Parameters.AddWithValue("$resource", resource);
        command.Parameters.AddWithValue("$start", start.ToString(StartFormat, CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$start_utc", start.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("$minutes", (long)minutes);
        command.Parameters.AddWithValue("$by", by);
        return Get((long)command.ExecuteScalar()!)!;
    }

    internal Appointment Update(long appointment, string by, DateTimeOffset? start = null, string? resource = null, AppointmentState? state = null, string? service = null, int? minutes = null)
    {
        using var command = Command("""
            UPDATE agenda_appointments SET updated_by = $by,
                start = coalesce($start, start), start_utc = coalesce($start_utc, start_utc),
                resource = coalesce($resource, resource), state = coalesce($state, state),
                service = coalesce($service, service), minutes = coalesce($minutes, minutes)
            WHERE id = $id;
            """);
        command.Parameters.AddWithValue("$by", by);
        command.Parameters.AddWithValue("$start", (object?)start?.ToString(StartFormat, CultureInfo.InvariantCulture) ?? DBNull.Value);
        command.Parameters.AddWithValue("$start_utc", (object?)start?.ToUnixTimeSeconds() ?? DBNull.Value);
        command.Parameters.AddWithValue("$resource", (object?)resource ?? DBNull.Value);
        command.Parameters.AddWithValue("$state", state is { } value ? AppointmentStates.ToWire(value) : DBNull.Value);
        command.Parameters.AddWithValue("$service", (object?)service ?? DBNull.Value);
        command.Parameters.AddWithValue("$minutes", minutes is { } length ? (long)length : DBNull.Value);
        command.Parameters.AddWithValue("$id", appointment);
        return command.ExecuteNonQuery() == 0
            ? throw new KeyNotFoundException($"no hay cita {appointment}")
            : Get(appointment)!;
    }

    internal Appointment? Get(long appointment)
    {
        using var command = Command($"{_select} WHERE id = $id;");
        command.Parameters.AddWithValue("$id", appointment);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadAppointment(reader) : null;
    }

    /// <summary>Las citas que empiezan en <c>[start, end)</c>, en orden de inicio y, a igualdad, de id.</summary>
    internal IReadOnlyList<Appointment> Between(DateTimeOffset start, DateTimeOffset end)
    {
        using var command = Command($"{_select} WHERE start_utc >= $from AND start_utc < $to ORDER BY start_utc, id;");
        command.Parameters.AddWithValue("$from", start.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("$to", end.ToUnixTimeSeconds());
        using var reader = command.ExecuteReader();
        var found = new List<Appointment>();
        while (reader.Read())
        {
            found.Add(ReadAppointment(reader));
        }

        return found;
    }

    /// <summary>Guarda un bloqueo, con el motivo sellado (<c>store.py:329-348</c>).</summary>
    /// <param name="resource">El recurso, o vacío si el bloqueo es de todos.</param>
    /// <param name="start">Desde cuándo.</param>
    /// <param name="end">Hasta cuándo.</param>
    /// <param name="reason">El motivo, ya recortado.</param>
    /// <param name="by">Quién lo pone.</param>
    /// <returns>El bloqueo, con su identificador.</returns>
    internal Block AddBlock(string resource, DateTimeOffset start, DateTimeOffset end, string reason, string by)
    {
        using var command = Command("""
            INSERT INTO agenda_blocks (resource, start, start_utc, ends, ends_utc, reason, created_by)
            VALUES ($resource, $start, $start_utc, $ends, $ends_utc, $reason, $by) RETURNING id;
            """);
        command.Parameters.AddWithValue("$resource", resource);
        command.Parameters.AddWithValue("$start", start.ToString(StartFormat, CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$start_utc", start.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("$ends", end.ToString(StartFormat, CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$ends_utc", end.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("$reason", _sealer.Seal(reason));
        command.Parameters.AddWithValue("$by", by);
        return new Block((long)command.ExecuteScalar()!, resource, start, end, reason, by);
    }

    /// <summary>Borra un bloqueo (<c>store.py:350-354</c>).</summary>
    /// <param name="block">El identificador del bloqueo.</param>
    /// <returns><see langword="false"/> si no existía.</returns>
    internal bool RemoveBlock(long block)
    {
        using var command = Command("DELETE FROM agenda_blocks WHERE id = $id;");
        command.Parameters.AddWithValue("$id", block);
        return command.ExecuteNonQuery() > 0;
    }

    /// <summary>Los bloqueos que se cruzan con <c>[start, end)</c>, en orden de inicio y, a igualdad, de id
    /// (<c>store.py:361-367</c>).</summary>
    /// <param name="start">Desde cuándo.</param>
    /// <param name="end">Hasta cuándo.</param>
    /// <returns>Los bloqueos; vacío si ninguno se cruza.</returns>
    internal IReadOnlyList<Block> BlocksBetween(DateTimeOffset start, DateTimeOffset end)
    {
        using var command = Command("""
            SELECT id, resource, start, ends, reason, created_by FROM agenda_blocks
            WHERE start_utc < $to AND ends_utc > $from ORDER BY start_utc, id;
            """);
        command.Parameters.AddWithValue("$from", start.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("$to", end.ToUnixTimeSeconds());
        using var reader = command.ExecuteReader();
        var found = new List<Block>();
        while (reader.Read())
        {
            found.Add(ReadBlock(reader));
        }

        return found;
    }

    private static Appointment ReadAppointment(SqliteDataReader reader) => new(
        reader.GetInt64(0),
        reader.GetInt64(1),
        reader.GetString(2),
        reader.GetString(3),
        DateTimeOffset.ParseExact(reader.GetString(4), StartFormat, CultureInfo.InvariantCulture),
        reader.GetInt32(5),
        AppointmentStates.Parse(reader.GetString(6)),
        reader.GetString(7),
        reader.GetString(8));

    private Client ReadClient(SqliteDataReader reader) =>
        new(reader.GetInt64(0), _sealer.Open((byte[])reader[1]), _sealer.Open((byte[])reader[2]));

    private Block ReadBlock(SqliteDataReader reader) => new(
        reader.GetInt64(0),
        reader.GetString(1),
        DateTimeOffset.ParseExact(reader.GetString(2), StartFormat, CultureInfo.InvariantCulture),
        DateTimeOffset.ParseExact(reader.GetString(3), StartFormat, CultureInfo.InvariantCulture),
        _sealer.Open((byte[])reader[4]),
        reader.GetString(5));

    private string Index(string phone) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(_phoneKey, Encoding.UTF8.GetBytes(PhoneNumbers.Normalize(phone))));

    private SqliteCommand Command(string sql)
    {
        var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = sql;
        return command;
    }
}
