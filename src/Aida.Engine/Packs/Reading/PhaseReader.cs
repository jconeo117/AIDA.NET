using Aida.Engine.Ontology;
using YamlDotNet.RepresentationModel;

namespace Aida.Engine.Packs.Reading;

/// <summary>Lee un tramo del ciclo (<see cref="PhaseSpec"/>). No tiene comprobación propia.</summary>
internal static class PhaseReader
{
    /// <summary>Lee un tramo.</summary>
    /// <param name="node">El nodo, que debe ser un mapa.</param>
    /// <param name="path">La ruta de la definición.</param>
    /// <param name="problems">Donde se anotan los problemas.</param>
    /// <param name="value">El tramo, si es válido.</param>
    /// <returns><see langword="true"/> si la definición es válida.</returns>
    internal static bool TryRead(YamlNode node, string path, ProblemList problems, out PhaseSpec value)
    {
        value = null!;
        var before = problems.Count;
        if (!FieldReader.TryMapping(node, path, problems, out var mapping))
        {
            return false;
        }

        var fields = new MappingFields(mapping, path, problems);
        var id = fields.Required<PhaseId>("id", Ids.Phase);
        var label = fields.Required<string>("label", FieldReader.TryText);
        var startsAt = fields.Required<long>("starts_at", Constraints.IntAtLeast(0));
        fields.RejectExtras();
        if (problems.Count != before)
        {
            return false;
        }

        value = new PhaseSpec(id, label, startsAt);
        return true;
    }
}
