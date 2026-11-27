namespace Aida.Arms.Health.Setup;

/// <summary>Un apartado de la atención: lo que se escribe en él, con su nombre y si es obligatorio.</summary>
/// <remarks>Los apartados vienen de <c>encounter.yaml</c>, en su orden: la pantalla los pinta así y la firma los guarda así.
/// Un apartado obligatorio vacío se firma con el texto «No consta.» (ADR 0040): se marca aquí para que la pantalla avise antes.</remarks>
/// <param name="Id">El identificador del apartado (<c>^[a-z][a-z_]*$</c>); es la clave del campo en el documento.</param>
/// <param name="Label">El nombre que ve la persona que escribe.</param>
/// <param name="Required">Si un apartado vacío no se puede firmar sin el aviso.</param>
public sealed record EncounterSection(string Id, string Label, bool Required);
