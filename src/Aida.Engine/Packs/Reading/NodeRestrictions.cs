using YamlDotNet.RepresentationModel;

namespace Aida.Engine.Packs.Reading;

/// <summary>Las restricciones del ADR 0024 que la 0.1 acepta en silencio y aquí fallan, con la ruta de su nodo.</summary>
/// <remarks>
/// Son cinco: un ancla o un alias, una clave <c>&lt;&lt;</c>, una etiqueta explícita y un entero fuera de 64 bits.
/// La clave repetida la detecta el lector del texto, que no llega a dar nodos (ADR 0024). Se comprueban antes que los
/// campos, en orden de documento. Un alias apunta al mismo nodo que su ancla, así que se detecta por el ancla.
/// </remarks>
internal static class NodeRestrictions
{
    /// <summary>Anota las restricciones que incumple un árbol de nodos.</summary>
    /// <param name="node">El nodo, normalmente la raíz del documento.</param>
    /// <param name="path">La ruta del nodo.</param>
    /// <param name="problems">Donde se anotan los problemas.</param>
    internal static void Check(YamlNode node, string path, ProblemList problems) =>
        Walk(node, path, problems, new HashSet<YamlNode>(ReferenceEqualityComparer.Instance));

    private static void Walk(YamlNode node, string path, ProblemList problems, HashSet<YamlNode> active)
    {
        // Un alias de un ancla que contiene a su propio alias forma un ciclo: el ancla ya se anotó al entrar.
        if (!active.Add(node))
        {
            return;
        }

        if (!node.Anchor.IsEmpty)
        {
            problems.Add(path, "Anchors and aliases are not allowed (ADR 0024)");
        }

        if (!node.Tag.IsEmpty)
        {
            problems.Add(path, "Explicit tags are not allowed (ADR 0024)");
        }

        switch (node)
        {
            case YamlScalarNode scalar:
                if (YamlScalar.KindOf(scalar) == ScalarKind.Int && !YamlScalar.TryIntOf(YamlScalar.TextOf(scalar), out _))
                {
                    problems.Add(path, "Integer does not fit in 64 bits (ADR 0024)");
                }

                break;
            case YamlSequenceNode sequence:
                for (var index = 0; index < sequence.Children.Count; index++)
                {
                    Walk(sequence.Children[index], ProblemList.Join(path, index.ToString(System.Globalization.CultureInfo.InvariantCulture)), problems, active);
                }

                break;
            case YamlMappingNode mapping:
                foreach (var pair in mapping.Children)
                {
                    var key = pair.Key is YamlScalarNode keyScalar ? YamlScalar.TextOf(keyScalar) : string.Empty;
                    var keyPath = ProblemList.Join(path, key);
                    // La clave de fusión ya lleva la etiqueta de YAML que la identifica: se anota una vez, como clave de fusión.
                    if (pair.Key is YamlScalarNode { Style: YamlDotNet.Core.ScalarStyle.Plain } plainKey && YamlScalar.TextOf(plainKey) == "<<")
                    {
                        problems.Add(keyPath, "Merge keys are not allowed (ADR 0024)");
                    }
                    else
                    {
                        Walk(pair.Key, keyPath, problems, active);
                    }

                    Walk(pair.Value, keyPath, problems, active);
                }

                break;
        }

        active.Remove(node);
    }
}
