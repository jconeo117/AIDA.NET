namespace Aida.Engine.Ontology;

/// <summary>Un estado con nombre que va y viene y cambia lo que rinde y lo que apetece hacer.</summary>
/// <remarks>
/// Origen: <c>engine/ontology.py:715-825</c>. Periódica si <see cref="Every"/> es mayor que 0; eventual si no. Las
/// tres comprobaciones de coherencia de tiempo las hace el cargador. Sin <c>periodic</c> ni <c>active_at</c>: los
/// consume el estado, que llega en la unidad siguiente.
/// </remarks>
public sealed class SituationSpec
{
    internal SituationSpec(SituationId id, string label, long every, long lasts, long startsAt,
        DeclaredMap<NeedId, double> gainMultipliers, DeclaredMap<NeedId, double> effects,
        DeclaredMap<TemperamentKnob, double> temperamentMultipliers)
    {
        Id = id;
        Label = label;
        Every = every;
        Lasts = lasts;
        StartsAt = startsAt;
        GainMultipliers = gainMultipliers;
        Effects = effects;
        TemperamentMultipliers = temperamentMultipliers;
    }

    /// <summary>El identificador de la situación.</summary>
    public SituationId Id { get; }

    /// <summary>Nombre legible, solo para logs.</summary>
    public string Label { get; }

    /// <summary>Cada cuántos ticks vuelve. 0 = no es periódica.</summary>
    public long Every { get; }

    /// <summary>Cuántos ticks dura cada vez que vuelve. Solo cuenta si <see cref="Every"/> es mayor que 0.</summary>
    public long Lasts { get; }

    /// <summary>En qué punto del ciclo largo empieza.</summary>
    public long StartsAt { get; }

    /// <summary>Cuánto multiplica lo que rinde una acción mientras dura, por necesidad.</summary>
    public DeclaredMap<NeedId, double> GainMultipliers { get; }

    /// <summary>Lo que hace cada tick a quien está bajo su efecto, por necesidad.</summary>
    public DeclaredMap<NeedId, double> Effects { get; }

    /// <summary>Cómo cambia la decisión mientras dura, por perilla del temperamento.</summary>
    public DeclaredMap<TemperamentKnob, double> TemperamentMultipliers { get; }
}
