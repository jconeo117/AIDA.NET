namespace Aida.Engine.Ontology;

/// <summary>Un gusto de un agente: cuánto le atrae una acción o un agente.</summary>
/// <remarks>Origen: <c>engine/ontology.py:427-448</c>. El destino se valida contra el catálogo en el cargador, no aquí.</remarks>
public sealed class TasteSpec
{
    internal TasteSpec(string target, double multiplier)
    {
        Target = target;
        Multiplier = multiplier;
    }

    /// <summary>El identificador de la acción o del agente hacia el que apunta el gusto.</summary>
    public string Target { get; }

    /// <summary>Cuánto multiplica el atractivo del destino. Siempre mayor que 0.</summary>
    public double Multiplier { get; }
}
