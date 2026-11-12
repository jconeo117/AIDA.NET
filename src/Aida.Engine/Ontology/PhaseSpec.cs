namespace Aida.Engine.Ontology;

/// <summary>Un tramo con nombre dentro del ciclo. Cuántos hay y cómo se llaman es contenido del pack.</summary>
/// <remarks>
/// Origen: <c>engine/ontology.py:692-712</c>. Lo anterior al primer tramo pertenece al último: un tramo que cruza el
/// punto de vuelta del ciclo no se parte en dos.
/// </remarks>
public sealed class PhaseSpec
{
    internal PhaseSpec(PhaseId id, string label, long startsAt)
    {
        Id = id;
        Label = label;
        StartsAt = startsAt;
    }

    /// <summary>El identificador del tramo.</summary>
    public PhaseId Id { get; }

    /// <summary>Nombre legible, solo para logs.</summary>
    public string Label { get; }

    /// <summary>El tick del ciclo en que empieza el tramo.</summary>
    public long StartsAt { get; }
}
