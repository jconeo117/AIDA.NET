using Aida.Engine.Ontology;

namespace Aida.Engine.Inbox;

/// <summary>Algo que llegó de fuera y todavía no se ha procesado.</summary>
/// <remarks>
/// Es <c>IncomingEvent</c> de la 0.1 (<c>queue.py:18-34</c>). El contenido es exacto, sin resumir ni normalizar: es dato, nunca
/// instrucción. <see cref="Seq"/> es el orden de llegada, un entero: desempata dos eventos del mismo tick sin depender de nada
/// accidental.
/// </remarks>
/// <param name="Tick">El instante en que se recibió, que puede ser anterior al que se procesa.</param>
/// <param name="Owner">A quién va dirigido.</param>
/// <param name="Content">El contenido exacto.</param>
/// <param name="Seq">El orden de llegada.</param>
internal sealed record IncomingEvent(long Tick, AgentId Owner, string Content, long Seq);
