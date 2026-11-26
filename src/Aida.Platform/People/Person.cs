namespace Aida.Platform.People;

/// <summary>Una persona: la misma para todos los brazos (ADR 0038).</summary>
/// <param name="Id">El identificador. Lo que guarda un brazo lo referencia por este id; no cambia nunca, ni al migrar.</param>
/// <param name="Name">El nombre, tal como se escribió.</param>
/// <param name="Phone">El teléfono, tal como se escribió. Puede ser compartido por varias personas.</param>
/// <param name="Details">El documento, la fecha de nacimiento y el sexo, si se conocen.</param>
public sealed record Person(long Id, string Name, string Phone, PersonDetails Details);
