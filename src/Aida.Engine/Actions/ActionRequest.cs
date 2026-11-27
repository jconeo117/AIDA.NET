using Aida.Engine.Ontology;

namespace Aida.Engine.Actions;

/// <summary>Una petición de acción: qué se pide, con qué argumentos y quién la pide.</summary>
/// <remarks>Es <c>ActionRequest</c> de la 0.1 (<c>actions.py:42-46</c>). Quién pide no lo elige el texto de la petición: lo
/// pone quien la arma a partir de la sesión. Una petición sin autor no entra (<c>I-156</c>).</remarks>
/// <param name="Action">La acción.</param>
/// <param name="Arguments">Sus argumentos, todos texto.</param>
/// <param name="RequestedBy">El identificador de la persona que la pide.</param>
/// <param name="AuthorizedBy">Quién confirmó la petición en este momento, verificada su credencial por quien la arma; o
/// <see langword="null"/>. Solo con ella pasa una acción por encima del techo (ADR 0039).</param>
public sealed record ActionRequest(ActionId Action, IReadOnlyDictionary<string, string> Arguments, string RequestedBy, string? AuthorizedBy = null);
