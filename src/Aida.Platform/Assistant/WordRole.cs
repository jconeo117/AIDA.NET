namespace Aida.Platform.Assistant;

/// <summary>Qué papel tiene una palabra en la frase del operador.</summary>
/// <remarks>Lo decide quien entiende, en un orden fijo (ADR 0037). El brazo lo lee para saber qué palabras le quedan, como
/// el nombre de una persona nueva.</remarks>
public enum WordRole
{
    /// <summary>No se reconoció: puede ser un nombre o un servicio.</summary>
    Free = 0,

    /// <summary>Una palabra vacía del catálogo.</summary>
    Filler = 1,

    /// <summary>Aparece en algún ejemplo de una intención.</summary>
    Intent = 2,

    /// <summary>Una palabra del tiempo.</summary>
    Time = 3,

    /// <summary>Un número que no es teléfono.</summary>
    Number = 4,

    /// <summary>Parte de un teléfono.</summary>
    Phone = 5,
}
