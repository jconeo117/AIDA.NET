using System.Collections.Immutable;

namespace Aida.Engine.Packs.Reading;

/// <summary>Las restricciones de la tabla de §A.3 (<c>ge</c>, <c>gt</c>, <c>le</c>, <c>min_length</c>), aplicadas a un conversor.</summary>
/// <remarks>
/// Las comprueba el lector, no la clase: la clase solo guarda valores. Un NaN no cumple ninguna cota numérica, como
/// en pydantic (<c>NaN</c> no compara como cierto con nada). Los mensajes son propios de .NET: solo se compara la ruta.
/// </remarks>
internal static class Constraints
{
    /// <summary>Un decimal mayor o igual que <paramref name="min"/> (<c>ge</c>).</summary>
    internal static NodeReader<double> FloatAtLeast(double min) =>
        FieldReader.Constrained<double>(FieldReader.TryFloat, value => value >= min, "Input should be greater than or equal to the minimum");

    /// <summary>Un decimal estrictamente mayor que <paramref name="min"/> (<c>gt</c>).</summary>
    internal static NodeReader<double> FloatAbove(double min) =>
        FieldReader.Constrained<double>(FieldReader.TryFloat, value => value > min, "Input should be greater than the minimum");

    /// <summary>Un decimal en <c>[low, high]</c>.</summary>
    internal static NodeReader<double> FloatIn(double low, double high) =>
        FieldReader.Constrained<double>(FieldReader.TryFloat, value => value >= low && value <= high, "Input is out of range");

    /// <summary>Un decimal en <c>(low, high]</c>.</summary>
    internal static NodeReader<double> FloatInHalfOpen(double low, double high) =>
        FieldReader.Constrained<double>(FieldReader.TryFloat, value => value > low && value <= high, "Input is out of range");

    /// <summary>Un entero mayor o igual que <paramref name="min"/> (<c>ge</c>).</summary>
    internal static NodeReader<long> IntAtLeast(long min) =>
        FieldReader.Constrained<long>(FieldReader.TryInt, value => value >= min, "Input should be greater than or equal to the minimum");

    /// <summary>Una lista con al menos <paramref name="min"/> elementos (<c>min_length</c>).</summary>
    /// <typeparam name="T">El tipo de cada elemento.</typeparam>
    /// <param name="items">El conversor de la lista.</param>
    /// <param name="min">El mínimo de elementos.</param>
    /// <returns>Un conversor de la lista con la restricción.</returns>
    internal static NodeReader<ImmutableArray<T>> AtLeastItems<T>(NodeReader<ImmutableArray<T>> items, int min) =>
        FieldReader.Constrained(items, value => value.Length >= min, "List should have at least the minimum number of items");
}
