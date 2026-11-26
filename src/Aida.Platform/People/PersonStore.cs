using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aida.Platform.Sealing;
using Microsoft.Data.Sqlite;

namespace Aida.Platform.People;

/// <summary>Las personas de la plataforma: alta, búsqueda y cambios, dentro de la transacción de quien las usa.</summary>
/// <remarks>
/// <para>Es la única puerta a la tabla <c>people</c>. Un brazo la usa por aquí y nunca con SQL sobre ella (ADR 0038). Trabaja
/// con la conexión y la transacción de quien lo crea, como <c>AuditLog.Record</c>: el cambio de la persona y su hecho de
/// auditoría se confirman juntos, o no se confirma ninguno.</para>
/// <para>Todo va sellado salvo el tipo de documento (ADR 0029). El teléfono y el documento se buscan por una huella HMAC con
/// su clave derivada: sin huella, buscar por teléfono obligaría a abrir a todas las personas.</para>
/// </remarks>
public sealed class PersonStore
{
    private const string _select = "SELECT id, name, phone, document_type, document_number, birth_date, sex FROM people";
    private const string _dateFormat = "yyyy-MM-dd";

    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction _transaction;
    private readonly Sealer _sealer;
    private readonly PersonKeys _keys;

    /// <summary>Crea el almacén sobre una transacción abierta.</summary>
    /// <param name="connection">La conexión de la petición.</param>
    /// <param name="transaction">La transacción de la petición; los cambios se confirman con ella.</param>
    /// <param name="sealer">El sellador de los datos personales.</param>
    /// <param name="keys">Las claves de las huellas del teléfono y del documento.</param>
    public PersonStore(SqliteConnection connection, SqliteTransaction transaction, Sealer sealer, PersonKeys keys)
    {
        _connection = connection;
        _transaction = transaction;
        _sealer = sealer;
        _keys = keys;
    }

    /// <summary>Una persona por su id.</summary>
    /// <param name="id">El identificador.</param>
    /// <returns>La persona, o <see langword="null"/> si no existe.</returns>
    public Person? Find(long id)
    {
        using var command = Command($"{_select} WHERE id = $id;");
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadPerson(reader) : null;
    }

    /// <summary>Todas las personas, por id.</summary>
    /// <returns>Todas; vacío si no hay ninguna.</returns>
    /// <remarks>Abre todos los datos sellados: buscar por nombre o por documento es recorrerlos. A la escala de un mostrador
    /// basta con leerla entera, como en la 0.1.</remarks>
    public IReadOnlyList<Person> All()
    {
        using var command = Command($"{_select} ORDER BY id;");
        return Read(command);
    }

    /// <summary>Las personas con ese teléfono, por id. Un teléfono puede ser de varias personas (un niño y su madre).</summary>
    /// <param name="phone">El teléfono, tal como se escribió; se compara normalizado.</param>
    /// <returns>Las que lo tienen, de la más antigua a la más reciente; vacío si ninguna.</returns>
    public IReadOnlyList<Person> ByPhone(string phone)
    {
        using var command = Command($"{_select} WHERE phone_index = $index ORDER BY id;");
        command.Parameters.AddWithValue("$index", PhoneIndex(phone));
        return Read(command);
    }

    /// <summary>La persona con ese documento.</summary>
    /// <param name="type">El tipo de documento, de <c>people.yaml</c>.</param>
    /// <param name="number">El número, tal como se escribió; se compara normalizado.</param>
    /// <returns>La persona, o <see langword="null"/> si ninguna tiene ese documento.</returns>
    /// <remarks>El documento identifica a una persona y el teléfono no: por eso esta búsqueda da una sola, y la de teléfono
    /// da varias.</remarks>
    public Person? ByDocument(string type, string number)
    {
        using var command = Command($"{_select} WHERE document_index = $index;");
        command.Parameters.AddWithValue("$index", DocumentIndex(type, number));
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadPerson(reader) : null;
    }

    /// <summary>Da de alta a una persona, sin datos opcionales.</summary>
    /// <param name="name">El nombre.</param>
    /// <param name="phone">El teléfono; puede ser el de otra persona.</param>
    /// <returns>La persona, con su id nuevo.</returns>
    public Person Add(string name, string phone) => Insert(null, name, phone);

    /// <summary>Da de alta a una persona con el id que ya tenía. Solo para migraciones.</summary>
    /// <param name="id">El id de la base de la que viene.</param>
    /// <param name="name">El nombre.</param>
    /// <param name="phone">El teléfono.</param>
    /// <returns>La persona, con el id dado.</returns>
    /// <remarks>Lo que guarda un brazo apunta a la persona por su id. Si la migración le diera otro, pasaría a ser de otra persona:
    /// por eso el id se conserva. El alta normal deja que la base lo ponga.</remarks>
    public Person Import(long id, string name, string phone) => Insert(id, name, phone);

    /// <summary>Cambia el nombre y el teléfono de una persona. Sus datos opcionales no se tocan.</summary>
    /// <param name="id">El identificador.</param>
    /// <param name="name">El nombre nuevo.</param>
    /// <param name="phone">El teléfono nuevo.</param>
    /// <exception cref="KeyNotFoundException">Si no hay ninguna persona con ese id.</exception>
    public void UpdateContact(long id, string name, string phone)
    {
        using var command = Command("UPDATE people SET name = $name, phone = $phone, phone_index = $index WHERE id = $id;");
        command.Parameters.AddWithValue("$name", _sealer.Seal(name));
        command.Parameters.AddWithValue("$phone", _sealer.Seal(phone));
        command.Parameters.AddWithValue("$index", PhoneIndex(phone));
        command.Parameters.AddWithValue("$id", id);
        if (command.ExecuteNonQuery() == 0)
        {
            throw new KeyNotFoundException($"no hay persona {id}");
        }
    }

    /// <summary>Sustituye los datos opcionales de una persona: el documento, la fecha y el sexo.</summary>
    /// <param name="id">El identificador.</param>
    /// <param name="details">Los datos nuevos; lo que sea <see langword="null"/> se borra.</param>
    /// <exception cref="KeyNotFoundException">Si no hay ninguna persona con ese id.</exception>
    /// <remarks>Un documento repetido deja que salte la restricción <c>UNIQUE</c> de la base: validar es cosa de quien llama,
    /// que puede explicarlo mejor. Tipo y número van juntos, como exige la tabla.</remarks>
    public void UpdateDetails(long id, PersonDetails details)
    {
        using var command = Command("""
            UPDATE people SET document_type = $type, document_number = $number, document_index = $document,
                birth_date = $birth, sex = $sex WHERE id = $id;
            """);
        command.Parameters.AddWithValue("$type", (object?)details.DocumentType ?? DBNull.Value);
        command.Parameters.AddWithValue("$number", Optional(SealOrNull(details.DocumentNumber)));
        command.Parameters.AddWithValue("$document", (object?)DocumentIndexOrNull(details) ?? DBNull.Value);
        command.Parameters.AddWithValue("$birth", Optional(SealOrNull(details.BirthDate?.ToString(_dateFormat, CultureInfo.InvariantCulture))));
        command.Parameters.AddWithValue("$sex", Optional(SealOrNull(details.Sex)));
        command.Parameters.AddWithValue("$id", id);
        if (command.ExecuteNonQuery() == 0)
        {
            throw new KeyNotFoundException($"no hay persona {id}");
        }
    }

    private Person Insert(long? id, string name, string phone)
    {
        using var command = Command("INSERT INTO people (id, name, phone, phone_index) VALUES ($id, $name, $phone, $index) RETURNING id;");
        command.Parameters.AddWithValue("$id", (object?)id ?? DBNull.Value);
        command.Parameters.AddWithValue("$name", _sealer.Seal(name));
        command.Parameters.AddWithValue("$phone", _sealer.Seal(phone));
        command.Parameters.AddWithValue("$index", PhoneIndex(phone));
        return new Person((long)command.ExecuteScalar()!, name, phone, PersonDetails.None);
    }

    private List<Person> Read(SqliteCommand command)
    {
        using var reader = command.ExecuteReader();
        var found = new List<Person>();
        while (reader.Read())
        {
            found.Add(ReadPerson(reader));
        }

        return found;
    }

    private Person ReadPerson(SqliteDataReader reader) => new(
        reader.GetInt64(0),
        _sealer.Open((byte[])reader[1]),
        _sealer.Open((byte[])reader[2]),
        new PersonDetails(
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : _sealer.Open((byte[])reader[4]),
            reader.IsDBNull(5) ? null : DateOnly.ParseExact(_sealer.Open((byte[])reader[5]), _dateFormat, CultureInfo.InvariantCulture),
            reader.IsDBNull(6) ? null : _sealer.Open((byte[])reader[6])));

    private byte[]? SealOrNull(string? text) => text is null ? null : _sealer.Seal(text);

    private string? DocumentIndexOrNull(PersonDetails details) =>
        details.DocumentType is { } type && details.DocumentNumber is { } number ? DocumentIndex(type, number) : null;

    private string PhoneIndex(string phone) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(_keys.Phone, Encoding.UTF8.GetBytes(PhoneNumbers.Normalize(phone))));

    private string DocumentIndex(string type, string number) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(_keys.Document, Encoding.UTF8.GetBytes($"{type}:{PersonDocument.Normalize(number)}")));

    private static object Optional(byte[]? value) => (object?)value ?? DBNull.Value;

    private SqliteCommand Command(string sql)
    {
        var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = sql;
        return command;
    }
}
