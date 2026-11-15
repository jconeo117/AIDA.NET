namespace Aida.Engine.Inbox;

/// <summary>El buzón llegó a su capacidad declarada y no admite más hasta vaciarse.</summary>
/// <remarks>Es <c>QueueFullError</c> de la 0.1 (<c>queue.py:37-38</c>). Revienta en vez de descartar en silencio o crecer sin
/// tope: un buzón sin límite es la forma más barata de tumbar un proceso de fondo que nadie vigila.</remarks>
internal sealed class QueueFullException : InvalidOperationException
{
    /// <summary>Crea el error con el mensaje de la 0.1.</summary>
    /// <param name="capacity">La capacidad del buzón.</param>
    internal QueueFullException(long capacity)
        : base($"cola llena: capacidad={capacity}, no admite un evento más")
    {
    }
}
