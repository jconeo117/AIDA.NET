using Aida.Arms.Agenda.Assistant;
using Aida.Arms.Agenda.Desk;

namespace Aida.Web.Components.Pages.Agenda;

/// <summary>De la propuesta del asistente, a lo que pinta la página de la agenda.</summary>
/// <remarks>Ninguna rama cambia nada: solo lee la ficha, y solo para una persona que ya encajó. El texto tecleado no sale de
/// aquí, ni hacia ninguna dirección. Una hora pedida y sin hueco no marca nada: se abren los huecos libres y se dice por qué.</remarks>
internal static class AgendaAssistantView
{
    /// <summary>La propuesta, traducida a paneles y mensajes.</summary>
    /// <param name="proposal">Lo que propuso el asistente.</param>
    /// <param name="desk">El mostrador, para el servicio por defecto y la ficha.</param>
    /// <returns>Lo que pinta la página.</returns>
    internal static AssistantOutcome Apply(AgendaProposal proposal, AgendaDesk desk)
    {
        var service = proposal.Service ?? desk.Setup.Services.FirstOrDefault()?.Id;
        var preferred = proposal.TimeTaken ? null : proposal.Time;
        var note = proposal.TimeTaken ? "assistant_time_taken" : null;
        return proposal.Kind switch
        {
            ProposalKind.NewAppointment => new AssistantOutcome("nueva", proposal.Day, service, preferred, proposal.Prefill, null, null, false, null, note),
            ProposalKind.FreeSlots => new AssistantOutcome("nueva", proposal.Day, service, preferred, null, null, null, false, null, note),
            ProposalKind.OpenCard => new AssistantOutcome("ficha", proposal.Day, null, null, null, desk.Card(proposal.Clients[0].Id), null, false, null, null),
            ProposalKind.PickClientForNew => new AssistantOutcome("elegir", proposal.Day, service, preferred, null, null, proposal.Clients, false, null, note),
            ProposalKind.PickClientForCard => new AssistantOutcome("elegir", proposal.Day, null, null, null, null, proposal.Clients, true, null, null),
            ProposalKind.NoClient => new AssistantOutcome(null, proposal.Day, null, null, null, null, null, false, "assistant_no_client", null),
            ProposalKind.PastDay => new AssistantOutcome(null, proposal.Day, null, null, null, null, null, false, "assistant_past_day", null),
            _ => new AssistantOutcome(null, proposal.Day, null, null, null, null, null, false, "assistant_not_understood", null),
        };
    }
}
