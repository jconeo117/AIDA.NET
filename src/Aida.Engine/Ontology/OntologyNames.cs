namespace Aida.Engine.Ontology;

/// <summary>Los nombres con que los enumerados de la ontología se escriben en el YAML del pack.</summary>
/// <remarks>
/// Los nombres son literales porque el pack los escribe a mano y la 0.1 los lee así. Una tabla aparte evita que
/// renombrar un miembro del enumerado cambie lo que aceptan los packs.
/// </remarks>
internal static class OntologyNames
{
    /// <summary>El nombre de una reversibilidad en el YAML.</summary>
    /// <param name="value">El valor.</param>
    /// <returns>El nombre tal como lo escribe el pack.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="value"/> no es un valor de <see cref="Reversibility"/>.</exception>
    internal static string ToWire(Reversibility value) => value switch
    {
        Reversibility.Reversible => "reversible",
        Reversibility.ReversibleWithCost => "reversible_with_cost",
        Reversibility.Irreversible => "irreversible",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Reversibilidad desconocida."),
    };

    /// <summary>El nombre de una perilla del temperamento en el YAML.</summary>
    /// <param name="value">El valor.</param>
    /// <returns>El nombre tal como lo escribe el pack.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="value"/> no es un valor de <see cref="TemperamentKnob"/>.</exception>
    internal static string ToWire(TemperamentKnob value) => value switch
    {
        TemperamentKnob.Temperature => "temperature",
        TemperamentKnob.LossAversion => "loss_aversion",
        TemperamentKnob.CriticalThreshold => "critical_need_threshold",
        TemperamentKnob.SwitchCost => "switch_cost",
        TemperamentKnob.Novelty => "novelty_weight",
        TemperamentKnob.LearningRate => "learning_rate",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Perilla desconocida."),
    };

    /// <summary>Busca una reversibilidad por su nombre en el YAML.</summary>
    /// <param name="wire">El texto del YAML.</param>
    /// <param name="value">La reversibilidad, si el nombre existe.</param>
    /// <returns><see langword="true"/> si el nombre existe.</returns>
    internal static bool TryParse(string wire, out Reversibility value)
    {
        foreach (var candidate in Enum.GetValues<Reversibility>())
        {
            if (ToWire(candidate) == wire)
            {
                value = candidate;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <summary>Busca una perilla del temperamento por su nombre en el YAML.</summary>
    /// <param name="wire">El texto del YAML.</param>
    /// <param name="value">La perilla, si el nombre existe.</param>
    /// <returns><see langword="true"/> si el nombre existe.</returns>
    internal static bool TryParse(string wire, out TemperamentKnob value)
    {
        foreach (var candidate in Enum.GetValues<TemperamentKnob>())
        {
            if (ToWire(candidate) == wire)
            {
                value = candidate;
                return true;
            }
        }

        value = default;
        return false;
    }
}
