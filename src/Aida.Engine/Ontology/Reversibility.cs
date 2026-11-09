namespace Aida.Engine.Ontology;

/// <summary>Si lo que hace una acción se puede deshacer, y a qué precio.</summary>
/// <remarks>
/// Es el mismo orden de la 0.1 (<c>engine/ontology.py:828-833</c>). El nombre en el YAML lo da
/// <see cref="OntologyNames"/>; el valor numérico no se escribe nunca.
/// </remarks>
public enum Reversibility
{
    /// <summary>Se puede deshacer sin coste.</summary>
    Reversible,

    /// <summary>Se puede deshacer, pero deshacerlo cuesta.</summary>
    ReversibleWithCost,

    /// <summary>No se puede deshacer.</summary>
    Irreversible,
}
