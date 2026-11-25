using Aida.Platform.Assistant;

namespace Aida.Cognition.Interpretation;

/// <summary>Lo que lee el lector de fechas de una frase: los momentos y las palabras que consumió.</summary>
/// <param name="Mentions">Los momentos, en el orden de la frase.</param>
/// <param name="Consumed">Las posiciones de las palabras que forman parte de un momento: su papel será <c>Time</c>.</param>
internal sealed record WhenResult(IReadOnlyList<WhenMention> Mentions, IReadOnlySet<int> Consumed);
