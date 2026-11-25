namespace Aida.Platform.Assistant;

/// <summary>Un momento dicho en la frase: un día, una hora, una parte del día o varias de esas cosas.</summary>
/// <param name="Day">El día, ya resuelto contra hoy (D13, D19); puede ser anterior a hoy si la fecha pasó hace poco.</param>
/// <param name="Time">La hora; con «de la tarde» ya va sumada (3 → 15:00). Sin parte del día, tal como se dijo.</param>
/// <param name="Period">La parte del día que se dijo.</param>
/// <remarks>Lo produce quien entiende y lo lee el brazo, que es quien sabe el horario y decide la hora ambigua (D12).
/// N014 le añade el día de la semana con que se dijo, para buscar el registro de origen.</remarks>
public sealed record WhenMention(DateOnly? Day, TimeOnly? Time, DayPeriod Period);
