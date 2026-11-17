using Aida.Engine.Ontology;

namespace Aida.Engine.Authorization;

/// <summary>El orden de la reversibilidad: cuánto cuesta deshacer una acción.</summary>
/// <remarks>Es <c>_RANK</c> de la 0.1 (<c>authority.py:24-28</c>), explícito y no el orden de declaración del enum, para que
/// reordenar el enum no cambie quién necesita permiso. Lo comparten <see cref="Authority"/> y la puerta de acciones: dos
/// copias del orden podrían decir cosas distintas.</remarks>
internal static class ReversibilityRank
{
    /// <summary>El rango de una reversibilidad.</summary>
    /// <param name="reversibility">La reversibilidad.</param>
    /// <returns>0 si es reversible, 1 si cuesta deshacerla y 2 si no se puede deshacer.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Si no es un valor de <see cref="Reversibility"/>.</exception>
    internal static int Of(Reversibility reversibility) => reversibility switch
    {
        Reversibility.Reversible => 0,
        Reversibility.ReversibleWithCost => 1,
        Reversibility.Irreversible => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(reversibility), reversibility, "Reversibilidad desconocida."),
    };

    /// <summary>Si una reversibilidad queda por encima de un techo.</summary>
    /// <param name="reversibility">La de la acción.</param>
    /// <param name="ceiling">El techo.</param>
    /// <returns><see langword="true"/> si deshacerla cuesta más de lo que el techo deja hacer sin permiso.</returns>
    internal static bool Exceeds(Reversibility reversibility, Reversibility ceiling) => Of(reversibility) > Of(ceiling);
}
