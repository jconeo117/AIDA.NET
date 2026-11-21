namespace Aida.Web.Components.Pages.Agenda;

/// <summary>Lo que llega del formulario de búsqueda: lo tecleado, por POST y nunca en la URL.</summary>
/// <remarks>Un formulario propio, fuera del de acciones, porque buscar no cambia nada y no debe pasar por la puerta. Va por
/// POST porque una URL acaba en los registros del servidor (<c>app.py:502-504</c>).</remarks>
public sealed class AgendaSearchForm
{
    /// <summary>Un nombre, o un teléfono, o parte de cualquiera de los dos.</summary>
    public string? Query { get; set; }
}
