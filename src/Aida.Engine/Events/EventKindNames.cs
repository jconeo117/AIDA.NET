namespace Aida.Engine.Events;

/// <summary>Nombres de los tipos de evento en el cable.</summary>
/// <remarks>
/// Los nombres son literales porque se comparan con la 0.1 byte a byte. Una tabla aparte, y no el nombre
/// del miembro en minúsculas, para que renombrar un miembro de <see cref="EventKind"/> no cambie el log.
/// </remarks>
internal static class EventKindNames
{
    /// <summary>El nombre de un tipo de evento en el JSON canónico.</summary>
    /// <param name="kind">El tipo.</param>
    /// <returns>El nombre en minúsculas y guiones bajos, tal como lo escribe la 0.1.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="kind"/> no es un valor de <see cref="EventKind"/>.</exception>
    internal static string ToWire(EventKind kind) => kind switch
    {
        EventKind.ActionStarted => "action_started",
        EventKind.ActionCompleted => "action_completed",
        EventKind.ActionRequested => "action_requested",
        EventKind.ActionRejected => "action_rejected",
        EventKind.ActionFailed => "action_failed",
        EventKind.ActionAborted => "action_aborted",
        EventKind.ActionProposed => "action_proposed",
        EventKind.ActionAuthorized => "action_authorized",
        EventKind.ActionDenied => "action_denied",
        EventKind.ActionExpired => "action_expired",
        EventKind.NeedEscalated => "need_escalated",
        EventKind.InputObserved => "input_observed",
        EventKind.IntentClassified => "intent_classified",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Tipo de evento desconocido."),
    };
}
