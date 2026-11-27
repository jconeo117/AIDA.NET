namespace Aida.Web.Components.Pages.Health;

/// <summary>Lo que llega del formulario de una atención: los campos, la contraseña y el paso que pulsó quien escribe.</summary>
/// <remarks>Va por POST y nunca en la URL: el texto clínico y la contraseña no deben quedar en los registros del servidor. La
/// contraseña tampoco se vuelve a pintar en la respuesta: si la firma falla, el campo sale vacío y se pide otra vez.</remarks>
public sealed class EncounterForm
{
    /// <summary>El texto de cada apartado, por su identificador.</summary>
    public Dictionary<string, string> Fields { get; set; } = new(StringComparer.Ordinal);

    /// <summary>La contraseña de quien firma; solo tiene sentido en el paso <c>sign</c>.</summary>
    public string? Password { get; set; }

    /// <summary>El paso que pulsó quien escribe: <c>save</c>, <c>review</c> o <c>sign</c>.</summary>
    public string? Step { get; set; }
}
