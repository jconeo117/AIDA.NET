using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Aida.Platform.Records;

/// <summary>La forma canónica de los campos de un documento y las huellas que se calculan sobre ella.</summary>
/// <remarks>
/// <para>Una huella depende de cada byte. Si dos ejecuciones escribieran los mismos campos con distinto orden o con otro
/// escape, la huella de una firma no se recalcularía igual y la verificación fallaría sin que nadie hubiera tocado nada. Por
/// eso la forma es fija: las claves en orden ordinal, sin espacios y con un solo escape.</para>
/// </remarks>
public static class RecordFields
{
    /// <summary>El formato de las fechas que se guardan y se firman: con su desfase, para que se reconstruyan igual.</summary>
    internal const string DateFormat = "yyyy-MM-dd'T'HH:mm:sszzz";

    /// <summary>Los campos en JSON canónico: un objeto con las claves en orden ordinal, solo textos, sin espacios.</summary>
    /// <param name="fields">Los campos, por su nombre.</param>
    /// <returns>La forma canónica, como texto.</returns>
    /// <remarks>El orden ordinal y no el de la cultura: en otra cultura o sistema, «ñ» iría después de «z», y la huella
    /// cambiaría. El escape por defecto de la biblioteca, que deja los caracteres no ASCII escritos como <c>\uXXXX</c>, es el
    /// que fija la forma: cambiarlo cambia cada huella ya firmada.</remarks>
    public static string Canonical(IReadOnlyDictionary<string, string> fields)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = JavaScriptEncoder.Default, Indented = false }))
        {
            writer.WriteStartObject();
            foreach (var key in fields.Keys.Order(StringComparer.Ordinal))
            {
                writer.WriteString(key, fields[key]);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>La huella de los campos: SHA-256 de su forma canónica, en hexadecimal minúscula.</summary>
    /// <param name="fields">Los campos, por su nombre.</param>
    /// <returns>La huella, de 64 caracteres.</returns>
    /// <remarks>La huella del brazo viaja en los argumentos de la puerta en lugar del texto (ADR 0029): permite comprobar que la
    /// herramienta recibió lo mismo que se escribió, sin que el texto de los campos salga por la auditoría.</remarks>
    public static string Digest(IReadOnlyDictionary<string, string> fields) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(fields))));

    /// <summary>La huella de la firma: quién, qué clase, de quién, cuándo, y el contenido exacto.</summary>
    /// <param name="author">Quién firma, que es el autor.</param>
    /// <param name="kind">La clase de documento.</param>
    /// <param name="person">La persona a la que es el documento.</param>
    /// <param name="canonicalFields">La forma canónica de los campos, tal como se sella.</param>
    /// <param name="signedAt">El instante de la firma.</param>
    /// <returns>La huella, de 64 caracteres.</returns>
    /// <remarks>Cubre también al autor, la clase, la persona y la fecha: si alguien cambiara cualquiera de ellos en la base, la
    /// huella que se recalcula ya no coincide, aunque el contenido siguiera igual.</remarks>
    internal static string Signature(string author, string kind, long person, string canonicalFields, DateTimeOffset signedAt) =>
        Digest(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["author"] = author,
            ["fields"] = canonicalFields,
            ["kind"] = kind,
            ["person"] = person.ToString(CultureInfo.InvariantCulture),
            ["signed_at"] = signedAt.ToString(DateFormat, CultureInfo.InvariantCulture),
        });
}
