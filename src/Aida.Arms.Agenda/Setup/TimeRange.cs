namespace Aida.Arms.Agenda.Setup;

/// <summary>Un tramo de atención de un día: desde y hasta, en la hora de la clínica.</summary>
/// <remarks>Es el par <c>(time, time)</c> de la 0.1 (<c>setup.py:39</c>). El cargador garantiza que el final es posterior al
/// inicio y que su largo es múltiplo del hueco.</remarks>
/// <param name="Start">Desde.</param>
/// <param name="End">Hasta.</param>
public sealed record TimeRange(TimeOnly Start, TimeOnly End);
