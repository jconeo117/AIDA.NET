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

    /// <summary>Varias personas encajan para cancelar o reprogramar: elige el operador y se abre su ficha.</summary>
    PickClientForCard = 5,

    /// <summary>La ficha de la persona, para cancelar o mover una cita suya con los botones de siempre.</summary>
    OpenCard = 6,
}
