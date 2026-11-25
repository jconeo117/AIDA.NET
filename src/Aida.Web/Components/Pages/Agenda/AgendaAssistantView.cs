using Aida.Arms.Agenda.Assistant;
using Aida.Arms.Agenda.Desk;
using Aida.Arms.Agenda.Storage;

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
        var appointment = proposal.Appointments.FirstOrDefault();
        return proposal.Kind switch
        {
            ProposalKind.NewAppointment => Panel("nueva", proposal.Day, service, preferred, proposal.Prefill, note: note),
            ProposalKind.FreeSlots => Panel("nueva", proposal.Day, service, preferred, note: note),
            ProposalKind.OpenCard => Panel("ficha", proposal.Day, card: desk.Card(proposal.Clients[0].Id), note: "assistant_no_appointment"),
            ProposalKind.PickClientForNew => Panel("elegir", proposal.Day, service, preferred, choices: proposal.Clients, note: note),
            ProposalKind.Cancel => Panel("cancelar", proposal.Day, appointment: appointment),
            ProposalKind.Move => Panel("mover", proposal.Day, service, preferred, appointment: appointment, moveTo: proposal.MoveTo, note: note),
            ProposalKind.PickToCancel => Panel("elegir", proposal.Day, appointmentChoices: proposal.Appointments),
            ProposalKind.PickToMove => Panel("elegir", proposal.Day, preferred: proposal.Time, appointmentChoices: proposal.Appointments, toMove: true, moveTo: proposal.MoveTo),
            ProposalKind.NoClient => Message(proposal.Day, "assistant_no_client"),
            ProposalKind.NoAppointment => Message(proposal.Day, "assistant_no_appointment"),
            ProposalKind.PastDay => Message(proposal.Day, "assistant_past_day"),
            _ => Message(proposal.Day, "assistant_not_understood"),
        };
    }

    private static AssistantOutcome Panel(
        string panel,
        DateOnly day,
        string? service = null,
        TimeOnly? preferred = null,
        Client? prefill = null,
        ClientCard? card = null,
        AppointmentView? appointment = null,
        IReadOnlyList<Client>? choices = null,
        IReadOnlyList<AppointmentView>? appointmentChoices = null,
        bool toMove = false,
        DateOnly? moveTo = null,
        string? note = null) =>
        new(panel, day, service, preferred, prefill, card, appointment, choices, appointmentChoices, toMove, moveTo, null, note);

    private static AssistantOutcome Message(DateOnly day, string key) =>
        new(null, day, null, null, null, null, null, null, null, false, null, key, null);
}
