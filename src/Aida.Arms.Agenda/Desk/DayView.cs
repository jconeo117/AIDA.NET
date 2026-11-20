using Aida.Arms.Agenda.Setup;

namespace Aida.Arms.Agenda.Desk;

/// <summary>Lo que la pantalla necesita para pintar un día.</summary>
/// <remarks>Es lo que devolvía <c>GET dia/&lt;fecha&gt;</c> en la 0.1 (<c>app.py:268-338</c>). Los huecos libres van por
/// servicio, ya calculados con la misma regla que agendar, así que ya excluyen los bloqueos.</remarks>
/// <param name="Day">El día.</param>
/// <param name="Now">La hora del servidor al pedirlo; su desfase es el de las horas que escribe la pantalla.</param>
/// <param name="Setup">La configuración: servicios, recursos y hueco.</param>
/// <param name="Appointments">Las citas que empiezan ese día, también las canceladas, en orden de inicio y de id.</param>
/// <param name="Blocks">Los bloqueos que se cruzan con el día, en orden de inicio y de id.</param>
/// <param name="Free">Los huecos libres de cada servicio, en orden de inicio y de recurso.</param>
/// <param name="Week">La semana del día, de lunes a sábado.</param>
public sealed record DayView(DateOnly Day, DateTimeOffset Now, AgendaSetup Setup, IReadOnlyList<AppointmentView> Appointments, IReadOnlyList<BlockView> Blocks, IReadOnlyDictionary<string, IReadOnlyList<FreeSlot>> Free, IReadOnlyList<WeekDay> Week)
{
    /// <summary>El primer bloqueo que cubre un tramo de un recurso, para pintar la rejilla.</summary>
    /// <param name="resource">El recurso de la columna.</param>
    /// <param name="start">El inicio de la celda.</param>
    /// <param name="end">El fin de la celda.</param>
    /// <returns>El bloqueo, o <see langword="null"/> si la celda está abierta.</returns>
    /// <remarks>Es <c>bloqueoEn</c> de la 0.1 (<c>script.py:102-107</c>), con las mismas dos reglas que el hueco.</remarks>
    public BlockView? BlockAt(string resource, DateTimeOffset start, DateTimeOffset end) =>
        Blocks.FirstOrDefault(view => view.Block.AppliesTo(resource) && view.Block.Crosses(start, end));
}
