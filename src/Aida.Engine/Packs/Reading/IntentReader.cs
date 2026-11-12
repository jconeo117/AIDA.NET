using System.Collections.Immutable;
using System.Globalization;
using Aida.Engine.Ontology;
using Aida.Engine.Serialization;
using YamlDotNet.RepresentationModel;

namespace Aida.Engine.Packs.Reading;

/// <summary>Lee una intención (<see cref="IntentSpec"/>) y su comprobación de ejemplos en blanco (§A.9).</summary>
internal static class IntentReader
{
    /// <summary>Lee una intención.</summary>
    /// <param name="node">El nodo, que debe ser un mapa.</param>
    /// <param name="path">La ruta de la definición.</param>
    /// <param name="problems">Donde se anotan los problemas.</param>
    /// <param name="value">La intención, si es válida.</param>
    /// <returns><see langword="true"/> si la definición es válida.</returns>
    internal static bool TryRead(YamlNode node, string path, ProblemList problems, out IntentSpec value)
    {
        value = null!;
        var before = problems.Count;
        if (!FieldReader.TryMapping(node, path, problems, out var mapping))
        {
            return false;
        }

        var fields = new MappingFields(mapping, path, problems);
        var id = fields.Required<IntentId>("id", Ids.Intent);
        var label = fields.Required<string>("label", FieldReader.TryText);
        var need = fields.Required<NeedId>("need", Ids.Need);
        var examples = fields.Required("examples", Constraints.AtLeastItems(CollectionReader.Items<string>(FieldReader.TryText), 1));
        fields.RejectExtras();
        if (problems.Count != before)
        {
            return false;
        }

        // ontology.py:675-686: una frase en blanco no compara contra nada. Se informa la primera.
        for (var index = 0; index < examples.Length; index++)
        {
            if (IsBlank(examples[index]))
            {
                problems.Add(path, $"intención {PythonText.Repr(id.Value)}: examples[{index.ToString(CultureInfo.InvariantCulture)}] está en blanco", fromAida: true);
                return false;
            }
        }

        value = new IntentSpec(id, label, need, examples);
        return true;
    }

    // Como str.strip() de Python: vacío o solo espacios, incluidos los separadores de archivo 0x1C-0x1F.
    private static bool IsBlank(string text)
    {
        foreach (var character in text)
        {
            if (!char.IsWhiteSpace(character) && character is not ('\u001c' or '\u001d' or '\u001e' or '\u001f'))
            {
                return false;
            }
        }

        return true;
    }
}
