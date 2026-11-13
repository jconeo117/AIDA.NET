namespace Aida.Engine.Randomness;

/// <summary><c>random.choices(población, weights=pesos, k=1)</c> de CPython 3.12, devolviendo el índice elegido.</summary>
/// <remarks>
/// Acumula los pesos de izquierda a derecha (<c>itertools.accumulate</c>), multiplica un <c>random()</c> por el total y
/// busca con <c>bisect_right</c> entre 0 y n-1. Con <c>bisect_left</c> el resultado cambia cuando el valor cae
/// exactamente en un acumulado. Los errores son los de Python, en el mismo orden.
/// </remarks>
internal static class WeightedChoice
{
    /// <summary>Sortea un índice con probabilidad proporcional a su peso.</summary>
    /// <param name="rng">El generador; consume un <c>random()</c>.</param>
    /// <param name="weights">Los pesos, al menos uno.</param>
    /// <returns>El índice elegido.</returns>
    /// <exception cref="ArgumentException">Si no hay pesos, si suman cero o menos, o si su suma no es finita
    /// (<c>ValueError</c> en Python).</exception>
    internal static int Pick(MersenneTwister rng, ReadOnlySpan<double> weights)
    {
        if (weights.IsEmpty)
        {
            throw new ArgumentException("No weights to choose from", nameof(weights));
        }

        var cumulative = new double[weights.Length];
        double accumulated = weights[0];
        cumulative[0] = accumulated;
        for (int i = 1; i < weights.Length; i++)
        {
            accumulated += weights[i];
            cumulative[i] = accumulated;
        }

        double total = cumulative[^1] + 0.0;
        if (total <= 0.0)
        {
            throw new ArgumentException("Total of weights must be greater than zero", nameof(weights));
        }

        if (!double.IsFinite(total))
        {
            throw new ArgumentException("Total of weights must be finite", nameof(weights));
        }

        double target = rng.NextDouble() * total;
        int lo = 0, hi = weights.Length - 1; // bisect_right(cumulative, target, 0, n - 1)
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (target < cumulative[mid])
            {
                hi = mid;
            }
            else
            {
                lo = mid + 1;
            }
        }

        return lo;
    }
}
