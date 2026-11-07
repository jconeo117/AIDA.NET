namespace Aida.Guards.Shape;

/// <summary>Regla C1: ningún archivo pasa de un número de líneas.</summary>
/// <remarks>
/// Las líneas que empiezan por <c>///</c>, tras quitar los espacios, no cuentan: la documentación XML es
/// obligatoria (ADR 0016) y no debe empujar a nadie a partir una clase. Las líneas en blanco y los
/// comentarios <c>//</c> sí cuentan. En Python, un archivo llegó a 2929 líneas porque nada lo medía
/// (ADR 0015).
/// </remarks>
public sealed class FileLengthGuard
{
    private readonly int _maxLines;

    /// <summary>Crea el guardián con un límite.</summary>
    /// <param name="maxLines">Líneas máximas permitidas; en el repositorio, 300.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="maxLines"/> es menor que 1.</exception>
    /// <remarks>El límite se inyecta para probar con números pequeños.</remarks>
    public FileLengthGuard(int maxLines)
    {
        if (maxLines < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLines), maxLines, "El límite de líneas debe ser al menos 1.");
        }

        _maxLines = maxLines;
    }

    /// <summary>Cuenta las líneas que computan para el límite.</summary>
    /// <param name="content">El contenido del archivo.</param>
    /// <returns>Las líneas, sin las de documentación XML.</returns>
    /// <remarks>
    /// Se parte por <c>\n</c> y se quita el <c>\r</c> final de cada línea. Un salto de línea al final del
    /// archivo no añade una línea vacía. Un archivo vacío tiene 0 líneas.
    /// </remarks>
    public int CountLines(string content)
    {
        if (content.Length == 0)
        {
            return 0;
        }

        var lines = content.Split('\n');
        var count = content.EndsWith('\n') ? lines.Length - 1 : lines.Length;
        return lines.Take(count).Count(line => !line.TrimStart().StartsWith("///", StringComparison.Ordinal));
    }

    /// <summary>Comprueba un archivo.</summary>
    /// <param name="path">Ruta relativa, para el informe.</param>
    /// <param name="content">El contenido.</param>
    /// <returns>Vacía si cabe; un <c>C1</c> en la línea 0 si <see cref="CountLines"/> supera el límite.</returns>
    /// <remarks>El mensaje dice cuántas líneas tiene y que se reparta en tipos (ADR 0015).</remarks>
    public IReadOnlyList<Violation> Check(string path, string content)
    {
        var lines = CountLines(content);
        if (lines <= _maxLines)
        {
            return [];
        }

        return [new Violation("C1", path, 0, $"{path} tiene {lines} líneas y el máximo es {_maxLines}. Reparte sus tipos en archivos, uno por responsabilidad (ADR 0015).")];
    }
}
