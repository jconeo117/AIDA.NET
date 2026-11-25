namespace Aida.Web.Components.Pages.Agenda;

/// <summary>Lo que llega de la caja del asistente: lo que escribió el operador, por POST y nunca en la URL.</summary>
/// <remarks>Un formulario propio, fuera del de acciones, porque entender no cambia nada y no debe pasar por la puerta. Va por
/// POST porque lleva nombres de personas, y una URL acaba en los registros del servidor (<c>app.py:502-504</c>).</remarks>
public sealed class AgendaAssistantForm
{
    /// <summary>La frase tal como la escribió el operador.</summary>
    public string? Text { get; set; }
}
