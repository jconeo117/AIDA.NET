namespace Aida.Arms.Agenda.Setup;

/// <summary>Quien atiende (un profesional, un consultorio): qué servicios da y en qué horario.</summary>
/// <remarks>Es <c>ResourceSpec</c> de la 0.1 (<c>setup.py:33-40</c>). No es una cuenta de usuario: el enlace con quien
/// entra en AIDA llega con la historia clínica (debate de N008, D7).</remarks>
/// <param name="Id">El identificador.</param>
/// <param name="Label">El nombre que ve el personal.</param>
/// <param name="Place">Dónde atiende.</param>
/// <param name="Services">Los identificadores de los servicios que da, en el orden del archivo.</param>
/// <param name="Hours">Los tramos de cada día; un día que no está no tiene atención.</param>
public sealed record ResourceSpec(string Id, string Label, string Place, IReadOnlyList<string> Services, IReadOnlyDictionary<DayOfWeek, IReadOnlyList<TimeRange>> Hours)
{
    /// <summary>Los tramos de un día.</summary>
    /// <param name="day">El día de la semana.</param>
    /// <returns>Los tramos, en el orden del archivo; vacío si ese día no atiende.</returns>
    public IReadOnlyList<TimeRange> HoursOn(DayOfWeek day) => Hours.TryGetValue(day, out var ranges) ? ranges : [];
}
