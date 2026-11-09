namespace Aida.Engine.Serialization;

/// <summary>Ordena cadenas por punto de código Unicode, como <c>sorted()</c> de Python.</summary>
/// <remarks>
/// <see cref="StringComparer.Ordinal"/> compara unidades UTF-16 y coloca los caracteres de fuera del
/// plano básico (pares sustitutos, 0xD800–0xDFFF) antes que los de 0xE000–0xFFFF; Python, al revés. Se
/// corrige moviendo los sustitutos al final del rango (ADR 0023). Una cadena con sustitutos sueltos queda
/// fuera del contrato.
/// </remarks>
internal sealed class CodePointComparer : IComparer<string>
{
    internal static readonly CodePointComparer Instance = new();

    /// <summary>Compara dos cadenas por punto de código.</summary>
    /// <param name="x">La primera; <see langword="null"/> va antes que cualquier otra.</param>
    /// <param name="y">La segunda.</param>
    /// <returns>Negativo, cero o positivo, como <see cref="IComparer{T}.Compare"/>.</returns>
    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        var length = Math.Min(x.Length, y.Length);
        for (var i = 0; i < length; i++)
        {
            int left = x[i];
            int right = y[i];
            if (left != right)
            {
                return Fix(left).CompareTo(Fix(right));
            }
        }

        return x.Length.CompareTo(y.Length);
    }

    // 0xE000–0xFFFF baja a 0xD800–0xF7FF; los sustitutos (0xD800–0xDFFF) suben a 0xF800–0xFFFF.
    private static int Fix(int unit) => unit >= 0xE000 ? unit - 0x800 : unit >= 0xD800 ? unit + 0x2000 : unit;
}
