namespace Aida.Platform.Audit;

/// <summary>Quién hizo algo: una persona identificada.</summary>
/// <remarks>Obligatorio en todo hecho de auditoría (ADR 0006). Va por identificador y por nombre, como
/// <c>actor</c> y <c>actor_name</c> en la 0.1 (<c>audit.py:96-97</c>): el identificador no cambia y el nombre es lo que
/// lee una persona.</remarks>
/// <param name="Id">El identificador del usuario.</param>
/// <param name="Name">El nombre con el que se muestra.</param>
public sealed record Actor(string Id, string Name);
