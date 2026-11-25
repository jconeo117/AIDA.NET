namespace Aida.Platform.Assistant;

/// <summary>La parte del día que se dijo, sin hora o junto a una hora que ella aclara.</summary>
public enum DayPeriod
{
    /// <summary>No se dijo.</summary>
    Unknown = 0,

    /// <summary>Por la mañana: antes de las 12:00.</summary>
    Morning = 1,

    /// <summary>Por la tarde o por la noche: desde las 12:00.</summary>
    Afternoon = 2,
}
