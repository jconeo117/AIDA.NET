using Aida.Engine.Ontology;

namespace Aida.Engine.Deliberation;

/// <summary>Una acción evaluada, con su utilidad y los factores de los que sale.</summary>
/// <remarks>
/// Es <c>Decision</c> de la 0.1 (<c>decide.py:47-63</c>) sin <c>phase_fit</c> ni <c>situation_fit</c>: en la 0.1 valen
/// siempre 1.0 en el bucle, y la fase no la pasa nadie. La explicación (N007) lee estos valores sin recalcularlos.
/// </remarks>
/// <param name="Action">La acción.</param>
/// <param name="Utility">La utilidad neta, con el gusto ya aplicado.</param>
/// <param name="Benefit">El beneficio ponderado por peso y urgencia.</param>
/// <param name="Cost">El coste ponderado igual.</param>
/// <param name="Novelty">El factor de novedad, <c>1 - saturación × peso</c>.</param>
internal sealed record Decision(ActionId Action, double Utility, double Benefit, double Cost, double Novelty);
