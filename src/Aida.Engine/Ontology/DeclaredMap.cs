using System.Collections;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Aida.Engine.Ontology;

/// <summary>Un mapa inmutable que recorre sus entradas en el orden en que el pack las declaró.</summary>
/// <typeparam name="TKey">La clave: un identificador del pack.</typeparam>
/// <typeparam name="TValue">El valor.</typeparam>
/// <remarks>
/// En la 0.1 un <c>dict</c> recorre en orden de inserción, y varias comprobaciones suman decimales en ese
/// orden (<c>engine/pack.py:390-394</c>). Sumar en otro orden puede cambiar el último bit y, con él, un
/// veredicto. Por eso no es un <see cref="Dictionary{TKey, TValue}"/>, cuyo recorrido no tiene un orden
/// garantizado (ADR 0023). Las claves son únicas: el cargador rechaza las repetidas (ADR 0024).
/// </remarks>
public sealed class DeclaredMap<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    where TKey : notnull
{
    private readonly ImmutableArray<KeyValuePair<TKey, TValue>> _entries;
    private readonly FrozenDictionary<TKey, TValue> _index;

    /// <summary>Crea el mapa con sus entradas, en el orden de declaración.</summary>
    /// <param name="entries">Las entradas, sin claves repetidas, en el orden en que se declararon.</param>
    /// <remarks>Solo el cargador lo construye: un mapa con claves repetidas no tendría un significado único.</remarks>
    internal DeclaredMap(IEnumerable<KeyValuePair<TKey, TValue>> entries)
    {
        _entries = [.. entries];
        _index = _entries.ToFrozenDictionary(entry => entry.Key, entry => entry.Value);
    }

    /// <summary>Un mapa vacío.</summary>
    public static DeclaredMap<TKey, TValue> Empty { get; } = new([]);

    /// <summary>Cuántas entradas tiene.</summary>
    public int Count => _entries.Length;

    /// <summary>Las claves, en el orden de declaración.</summary>
    public IEnumerable<TKey> Keys => _entries.Select(entry => entry.Key);

    /// <summary>Los valores, en el orden de declaración.</summary>
    public IEnumerable<TValue> Values => _entries.Select(entry => entry.Value);

    /// <summary>El valor de una clave.</summary>
    /// <param name="key">La clave.</param>
    /// <returns>El valor.</returns>
    /// <exception cref="KeyNotFoundException">Si la clave no existe.</exception>
    public TValue this[TKey key] => _index[key];

    /// <summary>Indica si existe una clave.</summary>
    /// <param name="key">La clave.</param>
    /// <returns><see langword="true"/> si existe.</returns>
    public bool ContainsKey(TKey key) => _index.ContainsKey(key);

    /// <summary>Busca el valor de una clave.</summary>
    /// <param name="key">La clave.</param>
    /// <param name="value">El valor, si existe.</param>
    /// <returns><see langword="true"/> si existe.</returns>
    public bool TryGetValue(TKey key, out TValue value) => _index.TryGetValue(key, out value!);

    /// <summary>Recorre las entradas en el orden de declaración.</summary>
    /// <returns>El enumerador.</returns>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => ((IEnumerable<KeyValuePair<TKey, TValue>>)_entries).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
