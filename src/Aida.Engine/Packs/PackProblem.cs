namespace Aida.Engine.Packs;

/// <summary>Un problema de un pack, con su ruta.</summary>
/// <param name="Path">Dónde, con el formato de pydantic unido por puntos: <c>needs.0.decay_per_tick</c>; <c>traits.0.need_weight_multipliers.x.[key]</c> si falla la clave de un mapa; vacía si es del pack entero.</param>
/// <param name="Message">Qué. Literal de la 0.1 cuando el mensaje es de AIDA (§A.9 y §A.10).</param>
/// <param name="FromAida">Si el mensaje es de AIDA (y se compara con la 0.1) o propio de .NET (y solo cuenta la ruta).</param>
/// <remarks>
/// Los problemas de campo llevan un mensaje propio de .NET, con <paramref name="FromAida"/> en falso: lo que se
/// compara con la 0.1 es su ruta, no su redacción. Así un error de tipo no obliga a copiar el texto de pydantic.
/// </remarks>
public sealed record PackProblem(string Path, string Message, bool FromAida);
