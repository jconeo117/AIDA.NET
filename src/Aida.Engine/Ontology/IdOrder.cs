using Aida.Engine.Serialization;

namespace Aida.Engine.Ontology;

/// <summary>El orden de los identificadores del pack: el <c>sorted()</c> de Python sobre texto.</summary>
/// <remarks>
/// Compara por punto de código (<see cref="CodePointComparer"/>), no por unidad UTF-16: es el orden en que la 0.1
/// recorre todo <c>dict</c> y todo <c>set</c> que se ordena. Sumar en coma flotante no es asociativo, así que sin este
/// orden fijo dos ejecuciones con la misma semilla podrían divergir en el último bit.
/// </remarks>
internal sealed class IdOrder : IComparer<NeedId>, IComparer<TraitId>, IComparer<ActionId>, IComparer<GoalId>, IComparer<SituationId>
{
    /// <summary>La única instancia: no tiene estado.</summary>
    internal static readonly IdOrder Instance = new();

    /// <summary>Compara dos identificadores de necesidad.</summary>
    /// <param name="x">El primero.</param>
    /// <param name="y">El segundo.</param>
    /// <returns>Negativo, cero o positivo, por punto de código.</returns>
    int IComparer<NeedId>.Compare(NeedId x, NeedId y) => CodePointComparer.Instance.Compare(x.Value, y.Value);

    /// <summary>Compara dos identificadores de rasgo.</summary>
    /// <param name="x">El primero.</param>
    /// <param name="y">El segundo.</param>
    /// <returns>Negativo, cero o positivo, por punto de código.</returns>
    int IComparer<TraitId>.Compare(TraitId x, TraitId y) => CodePointComparer.Instance.Compare(x.Value, y.Value);

    /// <summary>Compara dos identificadores de acción.</summary>
    /// <param name="x">El primero.</param>
    /// <param name="y">El segundo.</param>
    /// <returns>Negativo, cero o positivo, por punto de código.</returns>
    int IComparer<ActionId>.Compare(ActionId x, ActionId y) => CodePointComparer.Instance.Compare(x.Value, y.Value);

    /// <summary>Compara dos identificadores de meta.</summary>
    /// <param name="x">El primero.</param>
    /// <param name="y">El segundo.</param>
    /// <returns>Negativo, cero o positivo, por punto de código.</returns>
    int IComparer<GoalId>.Compare(GoalId x, GoalId y) => CodePointComparer.Instance.Compare(x.Value, y.Value);

    /// <summary>Compara dos identificadores de situación.</summary>
    /// <param name="x">El primero.</param>
    /// <param name="y">El segundo.</param>
    /// <returns>Negativo, cero o positivo, por punto de código.</returns>
    int IComparer<SituationId>.Compare(SituationId x, SituationId y) => CodePointComparer.Instance.Compare(x.Value, y.Value);
}
