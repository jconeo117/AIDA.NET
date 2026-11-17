namespace Aida.Arms.Agenda.Storage;

/// <summary>El nombre de cada estado en la base, el mismo de la 0.1 (<c>store.py:33-36</c>).</summary>
/// <remarks>Una tabla aparte para que renombrar un miembro del enum no cambie lo que hay guardado.</remarks>
internal static class AppointmentStates
{
    internal static string ToWire(AppointmentState state) => state switch
    {
        AppointmentState.Booked => "booked",
        AppointmentState.Cancelled => "cancelled",
        AppointmentState.Attended => "attended",
        AppointmentState.NoShow => "no_show",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "estado de cita desconocido"),
    };

    internal static AppointmentState Parse(string wire) => wire switch
    {
        "booked" => AppointmentState.Booked,
        "cancelled" => AppointmentState.Cancelled,
        "attended" => AppointmentState.Attended,
        "no_show" => AppointmentState.NoShow,
        _ => throw new InvalidDataException($"estado de cita desconocido: «{wire}»"),
    };
}
