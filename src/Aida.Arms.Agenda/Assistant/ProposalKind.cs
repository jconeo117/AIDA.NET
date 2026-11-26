namespace Aida.Arms.Agenda.Assistant;

/// <summary>Qué propone el asistente de la agenda.</summary>
public enum ProposalKind
{
    /// <summary>La frase no alcanzó ninguna intención.</summary>
    NotUnderstood = 0,

    /// <summary>Cancelar o reprogramar sin ninguna persona que encaje.</summary>
    NoClient = 1,

    /// <summary>Una cita nueva, con el relleno.</summary>
    NewAppointment = 2,

    /// <summary>Los huecos libres de un servicio.</summary>
    FreeSlots = 3,

    /// <summary>Varias personas encajan para una cita nueva: elige el operador.</summary>
    PickClientForNew = 4,

    /// <summary>Una sola persona encaja y ninguna cita suya: su ficha, con el aviso (D23).</summary>
    OpenCard = 6,

    /// <summary>La fecha que se dijo ya pasó (D19): no se abre nada.</summary>
    PastDay = 7,

    /// <summary>Una sola cita encaja para cancelar: se abre su confirmación (D22).</summary>
    Cancel = 8,

    /// <summary>Una sola cita encaja para reprogramar: se abre «mover» en el día de destino (D22).</summary>
    Move = 9,

    /// <summary>Varias citas encajan para cancelar: elige el operador.</summary>
    PickToCancel = 10,

    /// <summary>Varias citas encajan para reprogramar: elige el operador.</summary>
    PickToMove = 11,

    /// <summary>Varias personas encajan, pero ninguna tiene una cita que encaje.</summary>
    NoAppointment = 12,
}
