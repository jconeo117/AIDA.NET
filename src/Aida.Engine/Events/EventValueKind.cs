namespace Aida.Engine.Events;

/// <summary>Qué clase de valor guarda un <see cref="EventValue"/>; <c>None</c> es el valor por defecto, que no es válido.</summary>
internal enum EventValueKind
{
    /// <summary>El valor por defecto de la estructura: no válido.</summary>
    None = 0,

    /// <summary>Un texto.</summary>
    Text,

    /// <summary>Un entero.</summary>
    Integer,

    /// <summary>Un decimal.</summary>
    Number,

    /// <summary>Un booleano.</summary>
    Boolean,
}
