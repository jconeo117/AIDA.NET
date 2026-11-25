using Aida.Arms.Agenda.Storage;

namespace Aida.Arms.Agenda.Assistant;

/// <summary>Lo que propone el asistente: qué panel abrir y con qué relleno. No ejecuta nada.</summary>
/// <param name="Kind">Qué propone.</param>
/// <param name="Day">El día que se abre; hoy hasta N013.</param>
/// <param name="Service">El id del servicio entendido, o <see langword="null"/>.</param>
/// <param name="Prefill">Nombre y teléfono para una cita nueva: el contacto que encajó, o uno con id 0 hecho de lo que
/// sobra (D11). <see langword="null"/> si no hay nada que rellenar.</param>
/// <param name="Clients">La persona de la ficha, o las personas para elegir.</param>
/// <param name="MentionsTime">Si la frase traía palabras del tiempo, que en N012 no se leen.</param>
/// <remarks>Es una propuesta y no una orden: quien la recibe abre un panel con estos datos, y el cambio lo hace el operador
/// con el botón de siempre, que pasa por la puerta (ADR 0037).</remarks>
public sealed record AgendaProposal(
    ProposalKind Kind,
    DateOnly Day,
    string? Service,
    Client? Prefill,
    IReadOnlyList<Client> Clients,
    bool MentionsTime);
