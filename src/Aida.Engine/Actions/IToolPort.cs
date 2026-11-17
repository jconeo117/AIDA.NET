using Aida.Engine.Ontology;

namespace Aida.Engine.Actions;

/// <summary>La herramienta de un brazo: ejecuta una acción de su catálogo.</summary>
/// <remarks>
/// Es <c>ToolPort</c> de la 0.1 (<c>engine/ports.py:133-152</c>). Síncrona a propósito: el motor no espera nada con
/// <c>await</c> (ADR 0004). Solo la llama <see cref="ActionGate"/>, que ya dejó la petición en el registro; la herramienta
/// no ve quién pidió salvo por sus argumentos, ni registra nada. Una regla del negocio que dice que no se devuelve como
/// <see cref="ToolStatus.Rejected"/>, no se lanza.
/// </remarks>
public interface IToolPort
{
    /// <summary>Ejecuta una acción.</summary>
    /// <param name="action">La acción, ya conocida en el catálogo.</param>
    /// <param name="arguments">Sus argumentos, en orden de clave.</param>
    /// <returns>Cómo acabó.</returns>
    ToolResult Execute(ActionId action, IReadOnlyDictionary<string, string> arguments);
}
