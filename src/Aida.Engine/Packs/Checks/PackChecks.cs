using System.Collections.Immutable;

namespace Aida.Engine.Packs.Checks;

/// <summary>Las 15 comprobaciones del pack entero, en el orden de <c>ContentPack._validate</c> (<c>pack.py:591-613</c>).</summary>
/// <remarks>
/// Solo corren si las dos capas anteriores no dieron ningún problema: sobre un pack con campos rotos no tiene sentido
/// preguntar por sus referencias. El orden importa: el mensaje de cada comprobación sale en el mismo sitio que en la 0.1.
/// </remarks>
internal static class PackChecks
{
    /// <summary>Ejecuta las 15 comprobaciones.</summary>
    /// <param name="pack">El contenido leído y sin problemas de campo.</param>
    /// <returns>Los mensajes de AIDA, en orden. Vacío si el pack es válido.</returns>
    internal static ImmutableArray<string> Run(PackContents pack)
    {
        var problems = new List<string>();
        problems.AddRange(IdentityChecks.UniqueIds(pack));
        problems.AddRange(IdentityChecks.GlobalIdCollisions(pack));
        problems.AddRange(ReferenceChecks.NeedRefs(pack));
        problems.AddRange(ReferenceChecks.TraitRefs(pack));
        problems.AddRange(ReferenceChecks.PhaseRefs(pack));
        problems.AddRange(ReferenceChecks.SituationRefs(pack));
        problems.AddRange(GoalChecks.GoalRefs(pack));
        problems.AddRange(GoalChecks.GoalsAreReachable(pack));
        problems.AddRange(ActionChecks.IntentsAreActionable(pack));
        problems.AddRange(ReferenceChecks.TasteRefs(pack));
        problems.AddRange(ActionChecks.CostsAreDeclared(pack));
        problems.AddRange(ActionChecks.RequirementsCoverWhatTheySpend(pack));
        problems.AddRange(ActionChecks.ActionsAreReachable(pack));
        problems.AddRange(SettingsChecks.HealthRefs(pack));
        problems.AddRange(SettingsChecks.DeclaredBounds(pack));
        return [.. problems];
    }
}
