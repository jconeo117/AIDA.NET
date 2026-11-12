namespace Aida.Engine.Packs.Reading;

/// <summary>Donde se acumulan los problemas de una lectura, en el orden en que se encuentran.</summary>
/// <remarks>
/// Se recogen todos en vez de parar en el primero: la 0.1 los informa todos (<c>engine/pack.py:1-14</c>). Una
/// lista propia, y no una excepción, permite que las comprobaciones sigan y que el orden sea el de la lectura.
/// </remarks>
internal sealed class ProblemList
{
    private readonly List<PackProblem> _items = [];

    /// <summary>Cuántos problemas hay.</summary>
    internal int Count => _items.Count;

    /// <summary>Los problemas, en orden.</summary>
    internal IReadOnlyList<PackProblem> Items => _items;

    /// <summary>Añade un problema.</summary>
    /// <param name="path">La ruta del nodo; vacía si es del pack entero.</param>
    /// <param name="message">Qué está mal.</param>
    /// <param name="fromAida">Si el mensaje es de AIDA y se compara con la 0.1.</param>
    internal void Add(string path, string message, bool fromAida = false) =>
        _items.Add(new PackProblem(path, message, fromAida));

    /// <summary>Une una ruta con un segmento, como pydantic une su <c>loc</c>.</summary>
    /// <param name="parent">La ruta del padre; vacía en la raíz.</param>
    /// <param name="child">El segmento.</param>
    /// <returns>La ruta unida por puntos.</returns>
    internal static string Join(string parent, string child) =>
        parent.Length == 0 ? child : $"{parent}.{child}";
}
