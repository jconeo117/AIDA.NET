namespace Aida.Arms.Health.Desk;

/// <summary>La lista de atenciones de una persona, para su página de historia.</summary>
/// <remarks>Es lo único que la página de la persona pinta de la historia: ni los campos ni el texto de ninguna atención. Abrir
/// una es otra petición y deja su propia huella en la auditoría.</remarks>
/// <param name="Person">La persona.</param>
/// <param name="PersonName">Su nombre, para el encabezado.</param>
/// <param name="Lines">Las atenciones, la más reciente primero.</param>
public sealed record EncounterList(long Person, string PersonName, IReadOnlyList<EncounterLine> Lines);
