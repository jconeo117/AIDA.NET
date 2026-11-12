using System.Collections.Immutable;
using Aida.Engine.Ontology;
using YamlDotNet.RepresentationModel;

namespace Aida.Engine.Packs.Reading;

/// <summary>Convierte listas y mapas del YAML, con las rutas de pydantic (§A.8).</summary>
/// <remarks>
/// Las listas se recorren por índice (<c>needs.0</c>) y los mapas por clave, con la clave antes que el valor: si
/// falla la clave, la ruta termina en <c>.[key]</c>; si falla el valor, en <c>.&lt;clave&gt;</c>.
/// </remarks>
internal static class CollectionReader
{
    /// <summary>Un conversor de lista: el nodo debe ser una secuencia, y cada elemento se convierte con su índice.</summary>
    /// <typeparam name="T">El tipo de cada elemento.</typeparam>
    /// <param name="item">El conversor del elemento.</param>
    /// <returns>Un conversor de la lista, en el orden del YAML.</returns>
    internal static NodeReader<ImmutableArray<T>> Items<T>(NodeReader<T> item) =>
        (YamlNode node, string path, ProblemList problems, out ImmutableArray<T> value) =>
        {
            if (node is not YamlSequenceNode sequence)
            {
                value = default;
                problems.Add(path, "Input should be a valid array");
                return false;
            }

            var builder = ImmutableArray.CreateBuilder<T>(sequence.Children.Count);
            var ok = true;
            for (var index = 0; index < sequence.Children.Count; index++)
            {
                if (item(sequence.Children[index], ProblemList.Join(path, index.ToString(System.Globalization.CultureInfo.InvariantCulture)), problems, out var converted))
                {
                    builder.Add(converted);
                }
                else
                {
                    ok = false;
                }
            }

            value = builder.ToImmutable();
            return ok;
        };

    /// <summary>Un conversor de mapa de clave a valor, en el orden en que el pack declaró las claves.</summary>
    /// <typeparam name="TKey">El tipo de la clave.</typeparam>
    /// <typeparam name="TValue">El tipo del valor.</typeparam>
    /// <param name="key">El conversor de la clave.</param>
    /// <param name="value">El conversor del valor.</param>
    /// <returns>Un conversor del mapa.</returns>
    internal static NodeReader<DeclaredMap<TKey, TValue>> Map<TKey, TValue>(NodeReader<TKey> key, NodeReader<TValue> value)
        where TKey : notnull =>
        (YamlNode node, string path, ProblemList problems, out DeclaredMap<TKey, TValue> map) =>
        {
            map = DeclaredMap<TKey, TValue>.Empty;
            if (!FieldReader.TryMapping(node, path, problems, out var mapping))
            {
                return false;
            }

            var entries = new List<KeyValuePair<TKey, TValue>>();
            var ok = true;
            foreach (var pair in mapping.Children)
            {
                var keyText = pair.Key is YamlScalarNode scalar ? YamlScalar.TextOf(scalar) : string.Empty;
                var entryPath = ProblemList.Join(path, keyText);
                if (!key(pair.Key, ProblemList.Join(entryPath, "[key]"), problems, out var converted))
                {
                    ok = false;
                    continue;
                }

                if (value(pair.Value, entryPath, problems, out var converted2))
                {
                    entries.Add(new KeyValuePair<TKey, TValue>(converted, converted2));
                }
                else
                {
                    ok = false;
                }
            }

            map = new DeclaredMap<TKey, TValue>(entries);
            return ok;
        };
}
