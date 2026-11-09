using Aida.Engine.Events;

namespace Aida.Engine.Tests.Events;

public sealed class EventKindNamesTests
{
    [Theory]
    [InlineData(EventKind.ActionStarted, "action_started")]
    [InlineData(EventKind.ActionCompleted, "action_completed")]
    [InlineData(EventKind.ActionRequested, "action_requested")]
    [InlineData(EventKind.ActionRejected, "action_rejected")]
    [InlineData(EventKind.ActionFailed, "action_failed")]
    [InlineData(EventKind.ActionAborted, "action_aborted")]
    [InlineData(EventKind.ActionProposed, "action_proposed")]
    [InlineData(EventKind.ActionAuthorized, "action_authorized")]
    [InlineData(EventKind.ActionDenied, "action_denied")]
    [InlineData(EventKind.ActionExpired, "action_expired")]
    [InlineData(EventKind.NeedEscalated, "need_escalated")]
    [InlineData(EventKind.InputObserved, "input_observed")]
    [InlineData(EventKind.IntentClassified, "intent_classified")]
    public void ToWire_convierte_cada_tipo_al_nombre_en_el_cable(EventKind kind, string expectedWire)
    {
        Assert.Equal(expectedWire, EventKindNames.ToWire(kind));
    }

    [Fact]
    public void ToWire_rechaza_un_tipo_desconocido()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EventKindNames.ToWire((EventKind)999));
    }
}
