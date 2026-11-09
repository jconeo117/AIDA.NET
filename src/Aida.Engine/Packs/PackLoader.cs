using System.Collections.Immutable;
using System.Text;
using Aida.Engine.Packs.Checks;
using Aida.Engine.Packs.Reading;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Aida.Engine.Packs;

/// <summary>Lee un pack. Es la única puerta: lo que devuelve ya pasó todas las comprobaciones.</summary>
/// <remarks>
/// Lee como la 0.1: PyYAML decide qué es cada escalar y pydantic lo convierte al tipo del campo (ADR 0024). Si algo
/// falla, no se detiene en el primer error: los reúne todos, porque diseñar un pack es un ciclo de ajuste y prueba
/// (<c>engine/pack.py:1-14</c>). Tres capas, en este orden: el YAML a nodos, los nodos a tipos y las 15 comprobaciones.
/// </remarks>
public static class PackLoader
{
    /// <summary>Lee un pack desde un archivo UTF-8.</summary>
    /// <param name="path">La ruta del archivo.</param>
    /// <returns>El pack.</returns>
    /// <exception cref="PackException">Si no se puede leer o no es válido. El problema de lectura va con la ruta vacía.</exception>
    public static ContentPack Load(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path, Encoding.UTF8);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new PackException([new PackProblem(string.Empty, $"no se pudo leer el archivo: {exception.Message}", false)]);
        }

        return Parse(text);
    }

    /// <summary>Lee un pack desde su texto YAML.</summary>
    /// <param name="yaml">El texto.</param>
    /// <returns>El pack.</returns>
    /// <exception cref="PackException">Si no es válido.</exception>
    /// <remarks>Existe para que los tests y los dorados lean packs sin pasar por el disco.</remarks>
    public static ContentPack Parse(string yaml)
    {
        var root = ReadRoot(yaml, out var early);
        var problems = new ProblemList();
        foreach (var problem in early.Items)
        {
            problems.Add(problem.Path, problem.Message, problem.FromAida);
        }

        if (root is not null)
        {
            NodeRestrictions.Check(root, string.Empty, problems);
            if (PackReader.TryRead(root, problems, out var contents) && problems.Count == 0)
            {
                var checks = PackChecks.Run(contents);
                if (checks.IsEmpty)
                {
                    return new ContentPack(contents);
                }

                throw new PackException([.. checks.Select(message => new PackProblem(string.Empty, message, true))]);
            }
        }

        throw new PackException([.. problems.Items]);
    }

    // Del texto a la raíz: un error de sintaxis, un documento vacío o varios dan un problema con ruta vacía.
    private static YamlNode? ReadRoot(string yaml, out ProblemList problems)
    {
        problems = new ProblemList();
        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(yaml));
        }
        catch (YamlException exception)
        {
            problems.Add(string.Empty, exception.Message);
            return null;
        }

        if (stream.Documents.Count != 1)
        {
            problems.Add(string.Empty, stream.Documents.Count == 0
                ? "Input should be a valid dictionary: el documento está vacío"
                : "expected a single document in the stream");
            return null;
        }

        return stream.Documents[0].RootNode;
    }
}
