using Aida.Arms.Agenda.Assistant;
using Aida.Arms.Agenda.Desk;

namespace Aida.Web.Components.Pages.Agenda;

/// <summary>De la propuesta del asistente, a lo que pinta la página de la agenda.</summary>
/// <remarks>Ninguna rama llama al mostrador para cambiar nada: solo lee la ficha, y solo para una persona que ya encajó.
/// El texto tecleado no sale de aquí, ni hacia ninguna dirección.</remarks>
internal static class AgendaAssistantView
{
    /// <summary>La propuesta, traducida a paneles y mensajes.</summary>
    /// <param name="proposal">Lo que propuso el asistente.</param>
    /// <param name="desk">El mostrador, para el servicio por defecto y la ficha.</param>
    /// <returns>Lo que pinta la página.</returns>
    internal static AssistantOutcome Apply(AgendaProposal proposal, AgendaDesk desk)
    {
        var timeNotice = proposal.MentionsTime && proposal.Kind != ProposalKind.NotUnderstood ? "assistant_no_dates" : null;
        var service = proposal.Service ?? desk.Setup.Services.FirstOrDefault()?.Id;
        return proposal.Kind switch
        {
            ProposalKind.NewAppointment => new AssistantOutcome("nueva", service, proposal.Prefill, null, null, false, null, timeNotice),
            ProposalKind.FreeSlots => new AssistantOutcome("nueva", service, null, null, null, false, null, timeNotice),
            ProposalKind.OpenCard => new AssistantOutcome("ficha", null, null, desk.Card(proposal.Clients[0].Id), null, false, null, timeNotice),
            ProposalKind.PickClientForNew => new AssistantOutcome("elegir", service, null, null, proposal.Clients, false, null, timeNotice),
            ProposalKind.PickClientForCard => new AssistantOutcome("elegir", null, null, null, proposal.Clients, true, null, timeNotice),
            ProposalKind.NoClient => new AssistantOutcome(null, null, null, null, null, false, "assistant_no_client", timeNotice),
            _ => new AssistantOutcome(null, null, null, null, null, false, "assistant_not_understood", null),
        };
    }
}
