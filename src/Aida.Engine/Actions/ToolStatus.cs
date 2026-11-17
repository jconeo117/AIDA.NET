namespace Aida.Engine.Actions;

/// <summary>Cómo acabó una acción que pasó por la puerta.</summary>
/// <remarks>Los tres finales de la 0.1 (<c>actions.py:23-30</c>). Rechazar y fallar no son lo mismo: un rechazo es una regla
/// del negocio que se le explica a quien pidió; un fallo es algo ajeno a la regla, que se reintenta o se avisa.</remarks>
public enum ToolStatus
{
    /// <summary>La acción se hizo.</summary>
    Done,

    /// <summary>Una regla dijo que no (hueco ocupado, fuera de horario, por encima del techo). No cambió nada.</summary>
    Rejected,

    /// <summary>Algo ajeno a la regla lo impidió: el disco, la red, una excepción.</summary>
    Failed,
}
