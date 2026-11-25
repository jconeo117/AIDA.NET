using Aida.Arms.Agenda.Desk;
using Aida.Arms.Agenda.Storage;

namespace Aida.Arms.Agenda.Assistant;

/// <summary>Lo que propone el asistente: qué panel abrir y con qué relleno. No ejecuta nada.</summary>
/// <param name="Kind">Qué propone.</param>
/// <param name="Day">El día que se abre: el que se dijo, o hoy.</param>
/// <param name="Service">El id del servicio entendido, o el primero de la configuración.</param>
/// <param name="Time">La hora que se marca en los huecos, ya leída con el horario (D12) o sacada de la parte del día (D18);
/// <see langword="null"/> si no se dijo ninguna.</param>
/// <param name="TimeTaken">Si se pidió una hora o una parte del día y el servicio no tiene ningún hueco libre que encaje.</param>
/// <param name="Prefill">Nombre y teléfono para una cita nueva: el contacto que encajó, o uno con id 0 hecho de lo que
/// sobra (D11). <see langword="null"/> si no hay nada que rellenar.</param>
/// <param name="Clients">La persona de la ficha, o las personas para elegir.</param>
/// <param name="Appointments">La cita (una) o las citas para elegir, por inicio y por id.</param>
/// <param name="MoveTo">El día de destino al reprogramar, si se dijo; si no, cada cita se mueve dentro de su propio día.</param>
/// <remarks>Es una propuesta y no una orden: quien la recibe abre un panel con estos datos, y el cambio lo hace el operador
/// con el botón de siempre, que pasa por la puerta (ADR 0037).</remarks>
public sealed record AgendaProposal(
    ProposalKind Kind,
    DateOnly Day,
    string? Service,
    TimeOnly? Time,
    bool TimeTaken,
    Client? Prefill,
    IReadOnlyList<Client> Clients,
    IReadOnlyList<AppointmentView> Appointments,
    DateOnly? MoveTo);
