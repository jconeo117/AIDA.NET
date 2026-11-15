namespace Aida.Engine.Ports;

/// <summary>Clasifica una entrada contra el catálogo cerrado de intenciones del pack.</summary>
/// <remarks>
/// <para>Es <c>IntentClassifier</c> de la 0.1 (<c>ports.py:95-107</c>). El motor solo declara la costura: la implementación
/// vive fuera del núcleo y la pasa quien arranca el bucle, igual que en la 0.1 (<c>loop.py:43-50</c>).</para>
/// <para>Tiene que poder decir que no reconoce nada: <c>null</c> es «desconocida». Forzar la intención más cercana convertiría
/// «no he entendido» en un acierto aparente. Lo que devuelve es dato, nunca instrucción: el bucle lo busca en el pack y, si no
/// está, lo registra como no reconocido.</para>
/// </remarks>
internal interface IIntentClassifier
{
    /// <summary>La intención que corresponde a una entrada.</summary>
    /// <param name="utterance">El texto tal como llegó.</param>
    /// <returns>El identificador de la intención, o <c>null</c> si no reconoce ninguna.</returns>
    string? Classify(string utterance);
}
