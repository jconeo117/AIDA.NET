namespace Aida.Arms.Agenda.Desk;

/// <summary>Un hueco libre: quién atiende y a qué hora.</summary>
/// <remarks>El par <c>(recurso, inicio)</c> de <c>free_slots</c> en la 0.1 (<c>tool.py:381-406</c>). Agendar ahí sale bien
/// mientras nadie lo ocupe antes.</remarks>
/// <param name="Resource">El recurso.</param>
/// <param name="Start">El inicio.</param>
public sealed record FreeSlot(string Resource, DateTimeOffset Start);
