using System.Collections.Frozen;

namespace Aida.Engine.Actions;

/// <summary>El resultado de una acción: cómo acabó, por qué y qué devolvió.</summary>
/// <remarks>Es <c>ToolResult</c> de la 0.1 (<c>actions.py:33-39</c>). El motivo es un código estable
/// (<c>overlap</c>, <c>outside_hours</c>…), no un texto: el texto lo pone el pack, y así el registro no cambia cuando cambia
/// la redacción. Los datos son textos, como los argumentos, porque van tal cual al evento de cierre.</remarks>
public sealed record ToolResult
{
    /// <summary>Crea un resultado.</summary>
    /// <param name="status">Cómo acabó.</param>
    /// <param name="reason">El código del rechazo o del fallo; vacío si se hizo.</param>
    /// <param name="data">Lo que devuelve la acción; se copia.</param>
    /// <exception cref="ArgumentNullException">Si <paramref name="reason"/> o un valor de <paramref name="data"/> es
    /// <see langword="null"/>.</exception>
    public ToolResult(ToolStatus status, string reason, IReadOnlyDictionary<string, string>? data = null)
    {
        ArgumentNullException.ThrowIfNull(reason);
        if (data is not null && data.Values.Any(value => value is null))
        {
            throw new ArgumentNullException(nameof(data), "Un dato del resultado es null.");
        }

        Status = status;
        Reason = reason;
        Data = data is null ? FrozenDictionary<string, string>.Empty : data.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>Cómo acabó.</summary>
    public ToolStatus Status { get; }

    /// <summary>El código del rechazo o del fallo; vacío si se hizo.</summary>
    public string Reason { get; }

    /// <summary>Lo que devuelve la acción (por ejemplo, el identificador de la cita).</summary>
    public IReadOnlyDictionary<string, string> Data { get; }

    /// <summary>Una acción hecha.</summary>
    /// <param name="data">Lo que devuelve.</param>
    /// <returns>El resultado.</returns>
    public static ToolResult Done(IReadOnlyDictionary<string, string> data) => new(ToolStatus.Done, string.Empty, data);

    /// <summary>Una acción rechazada por una regla.</summary>
    /// <param name="reason">El código de la regla.</param>
    /// <param name="data">Lo que acompaña al rechazo, si algo.</param>
    /// <returns>El resultado.</returns>
    public static ToolResult Rejected(string reason, IReadOnlyDictionary<string, string>? data = null) => new(ToolStatus.Rejected, reason, data);
}
