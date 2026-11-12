using System.Globalization;
using Aida.Engine.Ontology;
using Aida.Engine.Serialization;
using YamlDotNet.RepresentationModel;

namespace Aida.Engine.Packs.Reading;

/// <summary>Lee una situación (<see cref="SituationSpec"/>) y sus tres comprobaciones de tiempo (§A.9).</summary>
/// <remarks>Las tres comprobaciones van en su orden: la primera que falla corta (<c>ontology.py:792-809</c>).</remarks>
internal static class SituationReader
{
    /// <summary>Lee una situación.</summary>
    /// <param name="node">El nodo, que debe ser un mapa.</param>
    /// <param name="path">La ruta de la definición.</param>
    /// <param name="problems">Donde se anotan los problemas.</param>
    /// <param name="value">La situación, si es válida.</param>
    /// <returns><see langword="true"/> si la definición es válida.</returns>
    internal static bool TryRead(YamlNode node, string path, ProblemList problems, out SituationSpec value)
    {
        value = null!;
        var before = problems.Count;
        if (!FieldReader.TryMapping(node, path, problems, out var mapping))
        {
            return false;
        }

        var fields = new MappingFields(mapping, path, problems);
        var id = fields.Required<SituationId>("id", Ids.Situation);
        var label = fields.Required<string>("label", FieldReader.TryText);
        var every = fields.Optional<long>("every", Constraints.IntAtLeast(0), 0);
        var lasts = fields.Optional<long>("lasts", Constraints.IntAtLeast(0), 0);
        var startsAt = fields.Optional<long>("starts_at", Constraints.IntAtLeast(0), 0);
        var gain = fields.Optional("gain_multipliers", CollectionReader.Map<NeedId, double>(Ids.Need, Constraints.FloatAbove(0.0)), DeclaredMap<NeedId, double>.Empty);
        var effects = fields.Optional("effects", CollectionReader.Map<NeedId, double>(Ids.Need, FieldReader.TryFloat), DeclaredMap<NeedId, double>.Empty);
        var temperament = fields.Optional("temperament_multipliers",
            CollectionReader.Map<TemperamentKnob, double>(FieldReader.TryTemperamentKnob, Constraints.FloatAbove(0.0)), DeclaredMap<TemperamentKnob, double>.Empty);
        fields.RejectExtras();
        if (problems.Count != before)
        {
            return false;
        }

        var message = PeriodMessage(id, every, lasts);
        if (message is not null)
        {
            problems.Add(path, message, fromAida: true);
            return false;
        }

        value = new SituationSpec(id, label, every, lasts, startsAt, gain, effects, temperament);
        return true;
    }

    // ontology.py:793-809. Devuelve el primer mensaje que aplica, o null si el tiempo es coherente.
    private static string? PeriodMessage(SituationId id, long every, long lasts)
    {
        var name = PythonText.Repr(id.Value);
        if (every != 0 && lasts == 0)
        {
            return $"situación {name}: vuelve cada {Text(every)} ticks pero dura 0, así que no llega a ocurrir nunca";
        }

        if (every != 0 && lasts >= every)
        {
            return $"situación {name}: dura {Text(lasts)} y vuelve cada {Text(every)}, así que no se apaga jamás; déjala eventual si es permanente";
        }

        if (lasts != 0 && every == 0)
        {
            return $"situación {name}: declara `lasts` sin `every`. Una situación eventual dura lo que se le diga al encenderla";
        }

        return null;
    }

    private static string Text(long number) => number.ToString(CultureInfo.InvariantCulture);
}
