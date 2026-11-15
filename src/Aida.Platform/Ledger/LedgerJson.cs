using System.Globalization;
using System.Text;

namespace Aida.Platform.Ledger;

/// <summary>Escribe un mapa de textos como <c>json.dumps(..., ensure_ascii=False, sort_keys=True)</c> de Python.</summary>
/// <remarks>
/// Es el formato de la cadena de la 0.1 (<c>platform/ledger.py:26-28</c>): separadores <c>", "</c> y <c>": "</c>,
/// claves ordenadas y sin escapar lo que no es ASCII. Se conserva para que la huella de una línea sea la misma que
/// en la 0.1, y eso da un oráculo byte a byte. Las claves son ASCII: así el orden ordinal de .NET y el de puntos de
/// código de Python coinciden sin más.
/// </remarks>
internal static class LedgerJson
{
    internal static string Write(IEnumerable<KeyValuePair<string, string>> fields)
    {
        var ordered = fields.OrderBy(field => field.Key, StringComparer.Ordinal).ToList();
        var builder = new StringBuilder("{");
        for (var index = 0; index < ordered.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            if (!Ascii.IsValid(ordered[index].Key))
            {
                throw new ArgumentException($"la clave «{ordered[index].Key}» no es ASCII", nameof(fields));
            }

            AppendString(builder, ordered[index].Key);
            builder.Append(": ");
            AppendString(builder, ordered[index].Value);
        }

        return builder.Append('}').ToString();
    }

    private static void AppendString(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (var character in value)
        {
            _ = character switch
            {
                '"' => builder.Append("\\\""),
                '\\' => builder.Append("\\\\"),
                '\n' => builder.Append("\\n"),
                '\r' => builder.Append("\\r"),
                '\t' => builder.Append("\\t"),
                '\b' => builder.Append("\\b"),
                '\f' => builder.Append("\\f"),
                < ' ' => builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture)),
                _ => builder.Append(character),
            };
        }

        builder.Append('"');
    }
}
