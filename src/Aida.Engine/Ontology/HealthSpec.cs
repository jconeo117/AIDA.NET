using System.Collections.Immutable;

namespace Aida.Engine.Ontology;

/// <summary>Qué clase de comportamiento quiere tener el pack: intención de diseño verificable.</summary>
/// <remarks>
/// Origen: <c>engine/ontology.py:953-1028</c>. Todo es opcional: un pack sin salud solo se somete a los invariantes
/// universales. Las acciones de <see cref="MayGoUnused"/> se ordenan por punto de código al volcarlas, no al leerlas.
/// </remarks>
public sealed class HealthSpec
{
    internal HealthSpec(long days, ImmutableArray<long> seeds, double? minCrisis, double? maxCrisis,
        double? maxAbortRatio, double? minDifferentiation, ImmutableArray<ActionId> mayGoUnused)
    {
        Days = days;
        Seeds = seeds;
        MinCrisis = minCrisis;
        MaxCrisis = maxCrisis;
        MaxAbortRatio = maxAbortRatio;
        MinDifferentiation = minDifferentiation;
        MayGoUnused = mayGoUnused;
    }

    /// <summary>Cuántos días dura la prueba de salud.</summary>
    public long Days { get; }

    /// <summary>Las semillas con que se repite la prueba: una corrida es una muestra, no un resultado.</summary>
    public ImmutableArray<long> Seeds { get; }

    /// <summary>Suelo de presión, o <see langword="null"/> si no se fija.</summary>
    public double? MinCrisis { get; }

    /// <summary>Techo de presión, o <see langword="null"/> si no se fija.</summary>
    public double? MaxCrisis { get; }

    /// <summary>Abandonos frente a acciones completadas, o <see langword="null"/> si no se fija.</summary>
    public double? MaxAbortRatio { get; }

    /// <summary>Distancia media mínima entre trayectorias con distinta semilla o distinta ficha, o <see langword="null"/>.</summary>
    public double? MinDifferentiation { get; }

    /// <summary>Acciones que pueden no aparecer nunca sin que sea un fallo, como una red de seguridad.</summary>
    public ImmutableArray<ActionId> MayGoUnused { get; }
}
