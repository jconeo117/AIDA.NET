using YamlDotNet.RepresentationModel;

namespace Aida.Engine.Packs.Reading;

/// <summary>Convierte un nodo YAML en un valor tipado, anotando los problemas con su ruta.</summary>
/// <typeparam name="T">El tipo del valor.</typeparam>
/// <param name="node">El nodo.</param>
/// <param name="path">La ruta del nodo, para los problemas.</param>
/// <param name="problems">Donde se anotan los problemas.</param>
/// <param name="value">El valor, si se pudo convertir.</param>
/// <returns><see langword="true"/> si el nodo se convirtió sin problemas.</returns>
internal delegate bool NodeReader<T>(YamlNode node, string path, ProblemList problems, out T value);
