using System.Globalization;
using System.Text.Json;
using Aida.Platform.Sealing;
using Microsoft.Data.Sqlite;

namespace Aida.Platform.Records;

/// <summary>Los documentos de la plataforma: borrador, firma y lectura, dentro de la transacción de quien los usa.</summary>
/// <remarks>
/// <para>Es la única puerta a la tabla <c>records</c>. Trabaja con la conexión y la transacción de quien lo crea, como
/// <c>PersonStore</c>: el cambio del documento y su hecho de auditoría se confirman juntos, o no se confirma ninguno.</para>
/// <para>El contenido va sellado como la forma canónica de los campos (ADR 0029, ADR 0040): en los bytes de la base no queda
/// el texto de ningún campo. Lo firmado es inalterable por los triggers del esquema; este código no lo protege solo, y por
/// eso la firma es un único <c>UPDATE</c> que solo cambia de borrador a firmado.</para>
/// </remarks>
public sealed class RecordStore
{
    private const string _select = "SELECT id, kind, person, status, author, reference, created_at, updated_at, signed_at FROM records";

    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction _transaction;
    private readonly Sealer _sealer;

    /// <summary>Crea el almacén sobre una transacción abierta.</summary>
    /// <param name="connection">La conexión de la petición.</param>
    /// <param name="transaction">La transacción de la petición; los cambios se confirman con ella.</param>
    /// <param name="sealer">El sellador del contenido.</param>
    public RecordStore(SqliteConnection connection, SqliteTransaction transaction, Sealer sealer)
    {
        _connection = connection;
        _transaction = transaction;
        _sealer = sealer;
    }

    /// <summary>Crea un borrador vacío.</summary>
    /// <param name="kind">La clase de documento.</param>
    /// <param name="person">La persona a la que es.</param>
    /// <param name="author">Quién lo escribe; solo él lo verá como borrador.</param>
    /// <param name="reference">Un texto opaco para enlazarlo con algo de fuera; o <see langword="null"/>.</param>
    /// <param name="at">El instante de la creación.</param>
    /// <returns>El identificador del borrador.</returns>
    /// <remarks>Empieza sin campos: los apartados se rellenan después, con <see cref="SaveDraft"/>. Así crear el documento y
    /// escribirlo son pasos separados, y cada uno sale en su hecho de la puerta.</remarks>
    public long CreateDraft(string kind, long person, string author, string? reference, DateTimeOffset at)
    {
        using var command = Command("""
            INSERT INTO records (kind, person, status, content, author, reference, created_at, updated_at)
            VALUES ($kind, $person, 'draft', $content, $author, $reference, $at, $at) RETURNING id;
            """);
        command.Parameters.AddWithValue("$kind", kind);
        command.Parameters.AddWithValue("$person", person);
        command.Parameters.AddWithValue("$content", _sealer.Seal(RecordFields.Canonical(new Dictionary<string, string>(StringComparer.Ordinal))));
        command.Parameters.AddWithValue("$author", author);
        command.Parameters.AddWithValue("$reference", (object?)reference ?? DBNull.Value);
        command.Parameters.AddWithValue("$at", Stamp(at));
        return (long)command.ExecuteScalar()!;
    }

    /// <summary>Sustituye los campos de un borrador.</summary>
    /// <param name="id">El documento.</param>
    /// <param name="fields">Los campos nuevos; los que no estén se pierden.</param>
    /// <param name="at">El instante del guardado.</param>
    /// <exception cref="KeyNotFoundException">Si no hay ningún documento con ese id.</exception>
    /// <exception cref="InvalidOperationException">Si el documento ya está firmado.</exception>
    /// <remarks>Se comprueba que es borrador en el propio <c>UPDATE</c>, no antes: así no hay una ventana entre leer el estado y
    /// escribir en la que alguien lo firme.</remarks>
    public void SaveDraft(long id, IReadOnlyDictionary<string, string> fields, DateTimeOffset at)
    {
        using var command = Command("UPDATE records SET content = $content, updated_at = $at WHERE id = $id AND status = 'draft';");
        command.Parameters.AddWithValue("$content", _sealer.Seal(RecordFields.Canonical(fields)));
        command.Parameters.AddWithValue("$at", Stamp(at));
        command.Parameters.AddWithValue("$id", id);
        if (command.ExecuteNonQuery() == 0)
        {
            throw Refused(id);
        }
    }

    /// <summary>Firma un borrador: calcula su huella y lo deja inalterable.</summary>
    /// <param name="id">El documento.</param>
    /// <param name="workstation">El puesto desde el que se firma, para la auditoría del documento.</param>
    /// <param name="at">El instante de la firma.</param>
    /// <returns>La huella de la firma, la que <see cref="Open"/> devuelve después como <c>ContentHash</c>.</returns>
    /// <exception cref="KeyNotFoundException">Si no hay ningún documento con ese id.</exception>
    /// <exception cref="InvalidOperationException">Si el documento ya está firmado.</exception>
    /// <remarks>Abre el contenido para calcular la huella, y después un único <c>UPDATE</c> pasa el estado de borrador a
    /// firmado con la huella, la fecha y el puesto. El contenido no se reescribe: lo firmado es lo que ya estaba escrito.</remarks>
    public string Sign(long id, string workstation, DateTimeOffset at)
    {
        var content = Open(id) ?? throw new KeyNotFoundException($"no hay documento {id}");
        if (content.Summary.Status != RecordStatus.Draft)
        {
            throw Refused(id);
        }

        var hash = RecordFields.Signature(content.Summary.Author, content.Summary.Kind, content.Summary.Person,
            RecordFields.Canonical(content.Fields), at);
        using var command = Command("""
            UPDATE records SET status = 'signed', content_hash = $hash, signed_at = $at, signed_workstation = $workstation
            WHERE id = $id AND status = 'draft';
            """);
        command.Parameters.AddWithValue("$hash", hash);
        command.Parameters.AddWithValue("$at", Stamp(at));
        command.Parameters.AddWithValue("$workstation", workstation);
        command.Parameters.AddWithValue("$id", id);
        if (command.ExecuteNonQuery() == 0)
        {
            throw Refused(id);
        }

        return hash;
    }

    /// <summary>El resumen de un documento, sin abrir su contenido.</summary>
    /// <param name="id">El documento.</param>
    /// <returns>El resumen, o <see langword="null"/> si no existe.</returns>
    public RecordSummary? Summary(long id)
    {
        using var command = Command($"{_select} WHERE id = $id;");
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadSummary(reader) : null;
    }

    /// <summary>Un documento abierto: su resumen, sus campos y su huella.</summary>
    /// <param name="id">El documento.</param>
    /// <returns>El documento, o <see langword="null"/> si no existe.</returns>
    /// <remarks>Descifra el contenido. Quien lo llama es responsable de la auditoría de la lectura y de quién puede verlo.</remarks>
    public RecordContent? Open(long id)
    {
        using var command = Command("""
            SELECT id, kind, person, status, author, reference, created_at, updated_at, signed_at, content, content_hash
            FROM records WHERE id = $id;
            """);
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var summary = ReadSummary(reader);
        var fields = ReadFields(_sealer.Open((byte[])reader[9]));
        var hash = reader.IsDBNull(10) ? null : reader.GetString(10);
        return new RecordContent(summary, fields, hash);
    }

    /// <summary>Los documentos de una persona de una clase, el más reciente primero.</summary>
    /// <param name="person">La persona.</param>
    /// <param name="kind">La clase de documento.</param>
    /// <returns>Los resúmenes, ordenados por la fecha de creación descendente y, a igualdad, por id descendente; vacío si no hay ninguno.</returns>
    /// <remarks>El desempate por id es el que hace el orden estable: dos documentos creados en el mismo instante no cambian de sitio
    /// de una lectura a otra.</remarks>
    public IReadOnlyList<RecordSummary> ListFor(long person, string kind)
    {
        using var command = Command($"{_select} WHERE person = $person AND kind = $kind ORDER BY created_at DESC, id DESC;");
        command.Parameters.AddWithValue("$person", person);
        command.Parameters.AddWithValue("$kind", kind);
        using var reader = command.ExecuteReader();
        var found = new List<RecordSummary>();
        while (reader.Read())
        {
            found.Add(ReadSummary(reader));
        }

        return found;
    }

    private static Dictionary<string, string> ReadFields(string canonical)
    {
        using var document = JsonDocument.Parse(canonical);
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            fields[property.Name] = property.Value.GetString()!;
        }

        return fields;
    }

    private static RecordSummary ReadSummary(SqliteDataReader reader) => new(
        reader.GetInt64(0),
        reader.GetString(1),
        reader.GetInt64(2),
        reader.GetString(3) == "signed" ? RecordStatus.Signed : RecordStatus.Draft,
        reader.GetString(4),
        reader.IsDBNull(5) ? null : reader.GetString(5),
        ParseStamp(reader.GetString(6)),
        ParseStamp(reader.GetString(7)),
        reader.IsDBNull(8) ? null : ParseStamp(reader.GetString(8)));

    private static string Stamp(DateTimeOffset at) => at.ToString(RecordFields.DateFormat, CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseStamp(string text) =>
        DateTimeOffset.ParseExact(text, RecordFields.DateFormat, CultureInfo.InvariantCulture);

    private Exception Refused(long id) => Summary(id) is null
        ? new KeyNotFoundException($"no hay documento {id}")
        : new InvalidOperationException($"el documento {id} ya está firmado");

    private SqliteCommand Command(string sql)
    {
        var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = sql;
        return command;
    }
}
