namespace Aida.Engine.Events;

/// <summary>El vocabulario de hechos que registra el motor.</summary>
/// <remarks>
/// Es abstracto: ningún valor nombra un dominio. Es un conjunto cerrado que crece con sus consumidores:
/// cada unidad añade el valor que emite, y nunca uno «por si acaso» (<c>engine/events.py:76-78</c>). En
/// el JSON se escribe con su nombre en minúsculas y guiones bajos, el mismo de la 0.1; el valor numérico
/// del enumerado no se persiste nunca.
/// </remarks>
public enum EventKind
{
    /// <summary>Empezó la ejecución de una acción.</summary>
    ActionStarted,

    /// <summary>La acción terminó y dio su resultado.</summary>
    ActionCompleted,

    /// <summary>Una petición entró en la puerta de acciones, con su autor y sus argumentos, antes de ejecutarse: lo que se pidió queda aunque la herramienta falle.</summary>
    ActionRequested,

    /// <summary>Una regla del negocio dijo que no. No es un fallo: se explica a quien pidió.</summary>
    ActionRejected,

    /// <summary>Algo ajeno a la regla, como una excepción de la herramienta. Se reintenta o se avisa.</summary>
    ActionFailed,

    /// <summary>La acción dejó de ser válida a mitad. Sin esto, depurar algo atascado es adivinar.</summary>
    ActionAborted,

    /// <summary>El asistente quiso empezar algo que su techo de autoridad no le deja empezar solo. No es un fallo: es la mitad asíncrona de la confirmación.</summary>
    ActionProposed,

    /// <summary>Alguien con autoridad dijo que sí. Evento propio y no una nota dentro de <c>ActionStarted</c>: el permiso y la ejecución ocurren en instantes distintos.</summary>
    ActionAuthorized,

    /// <summary>Alguien con autoridad dijo que no.</summary>
    ActionDenied,

    /// <summary>Nadie contestó a tiempo. Se anota como hecho: el silencio también es una respuesta.</summary>
    ActionExpired,

    /// <summary>Un eje lleva varios ticks seguidos hundido. Convierte «esto va mal» en «hay que avisar ahora», sin que el motor sepa qué significa avisar.</summary>
    NeedEscalated,

    /// <summary>Una entrada procedente del exterior se registró con su contenido exacto. Es lo que hace verificable el determinismo.</summary>
    InputObserved,

    /// <summary>Se intentó reconocer una entrada del buzón contra el catálogo del pack. Si no se reconoció, el silencio en el payload es la señal.</summary>
    IntentClassified,
}
