namespace Aida.Arms.Agenda.Desk;

/// <summary>Un día de la tira de la semana: cuánto hay agendado y cuánto cabe.</summary>
/// <remarks>Una fila de <c>week_summary</c> en la 0.1 (<c>tool.py:409-438</c>). Las dos cifras van en huecos, así que se
/// comparan entre sí: una cita de una hora en una agenda de media hora ocupa dos.</remarks>
/// <param name="Day">El día.</param>
/// <param name="Booked">Los huecos ocupados por citas no canceladas.</param>
/// <param name="Capacity">Los huecos del horario que no tapa ningún bloqueo.</param>
public sealed record WeekDay(DateOnly Day, int Booked, int Capacity);
