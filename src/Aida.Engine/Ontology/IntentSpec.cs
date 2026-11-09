using System.Collections.Immutable;

namespace Aida.Engine.Ontology;

/// <summary>Una entrada del catálogo cerrado de lo que alguien puede pedir. Dice qué necesidad atender, no qué acción.</summary>
/// <remarks>
/// Origen: <c>engine/ontology.py:645-686</c>. Reconocerla empuja al agente a deliberar con esa necesidad; no ejecuta
/// nada por sí sola. El cargador comprueba que los ejemplos no estén en blanco.
/// </remarks>
public sealed class IntentSpec
{
    internal IntentSpec(IntentId id, string label, NeedId need, ImmutableArray<string> examples)
    {
        Id = id;
        Label = label;
        Need = need;
        Examples = examples;
    }

    /// <summary>El identificador de la intención.</summary>
    public IntentId Id { get; }

    /// <summary>Nombre legible, solo para logs.</summary>
    public string Label { get; }

    /// <summary>La necesidad que se pide atender al reconocer esta intención.</summary>
    public NeedId Need { get; }

    /// <summary>Frases de referencia contra las que se compara lo que escribe quien pide algo.</summary>
    public ImmutableArray<string> Examples { get; }
}
