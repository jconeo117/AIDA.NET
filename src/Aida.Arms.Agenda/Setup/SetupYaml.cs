using System.Globalization;
using Aida.Platform.Packs;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Aida.Arms.Agenda.Setup;

/// <summary>Los escalares de los archivos de la agenda: textos obligatorios, enteros positivos y horas.</summary>
/// <remarks>Sobre <see cref="PlatformYaml"/> (ADR 0033). Los mensajes dicen la ruta del archivo, como en la 0.1
/// (<c>setup.py:57-122</c>): un horario mal escrito se arregla en el archivo, no buscando en el código.</remarks>
internal static class SetupYaml
{
    internal static string RequiredText(YamlMappingNode map, string key, string path) =>
        PlatformYaml.Text(PlatformYaml.Child(map, key)) ?? throw new InvalidDataException($"{path}: falta '{key}'");

    /// <summary>Un entero positivo escrito sin comillas, como exige <c>isinstance(x, int)</c> en la 0.1.</summary>
    internal static bool TryPositive(YamlNode? node, out int value)
    {
        value = 0;
        return node is YamlScalarNode { Style: ScalarStyle.Plain, Value: { Length: > 0 } text }
            && text.All(char.IsAsciiDigit)
            && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value)
            && value > 0;
    }

    /// <summary>Una hora «H:MM»: dos partes enteras, como <c>_time</c> de la 0.1 (<c>setup.py:57-64</c>).</summary>
    internal static TimeOnly Time(YamlNode? node, string path)
    {
        var text = PlatformYaml.Text(node) ?? throw new InvalidDataException($"{path}: una hora debe ser texto «HH:MM»");
        var parts = text.Split(':');
        if (parts.Length == 2
            && parts.All(part => part.Length > 0 && part.All(char.IsAsciiDigit))
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hour)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minute)
            && hour <= 23
            && minute <= 59)
        {
            return new TimeOnly(hour, minute);
        }

        throw new InvalidDataException($"{path}: hora inválida «{text}»");
    }
}
