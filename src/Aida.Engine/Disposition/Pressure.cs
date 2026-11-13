using Aida.Engine.Numerics;
using Aida.Engine.Ontology;
using Aida.Engine.Packs;

namespace Aida.Engine.Disposition;

/// <summary>La única puerta de urgencia y de desgaste del motor.</summary>
/// <remarks>
/// La urgencia es una curva convexa <c>(1 - valor / tope) ** exponente</c>: el desgaste de un sistema sano queda cerca
/// de cero y reserva el tramo alto para el deterioro (<c>engine/disposition.py:177-258</c>). Un solo sitio para las dos
/// cosas: si la deliberación y el desgaste la calcularan por separado, podrían divergir.
/// </remarks>
internal static class Pressure
{
    /// <summary>El apremio de una necesidad según su nivel respecto a su tope.</summary>
    /// <param name="value">El nivel de la necesidad.</param>
    /// <param name="exponent">El exponente de la curva.</param>
    /// <param name="cap">El tope efectivo del agente en ese eje. Con tope 0 o menos, la urgencia es 0.</param>
    /// <returns>Un valor en <c>[0, 1]</c>: 1 cuando está en el suelo, 0 cuando alcanza su tope.</returns>
    /// <remarks>Normaliza respecto al tope del agente, no a 1.0: quien llega a su límite efectivo debe sentirlo saciado.</remarks>
    internal static double Urgency(double value, double exponent, double cap = 1.0)
    {
        if (cap <= 0.0)
        {
            return 0.0;
        }

        return PyFloat.Pow(PyFloat.Max(0.0, 1.0 - (value / cap)), exponent);
    }

    /// <summary>El desgaste global: media ponderada convexa de las urgencias de los ejes que lo causan.</summary>
    /// <param name="pack">El pack, de donde salen los pesos de desgaste.</param>
    /// <param name="needs">El nivel actual de cada necesidad.</param>
    /// <param name="caps">Los techos efectivos del agente, o <see langword="null"/> para usar los nominales.</param>
    /// <returns>El desgaste, o 0.0 si ningún eje declara <c>strain_weight</c> mayor que 0.</returns>
    /// <remarks>
    /// Recorre las necesidades por identificador: la suma en coma flotante no es asociativa y un orden distinto
    /// cambiaría el último bit (<c>disposition.py:219-258</c>).
    /// </remarks>
    internal static double ComputeStrain(ContentPack pack, IReadOnlyDictionary<NeedId, double> needs,
        IReadOnlyDictionary<NeedId, double>? caps)
    {
        var total = 0.0;
        var weights = 0.0;
        foreach (var need in pack.Needs.OrderBy(n => n.Id, IdOrder.Instance))
        {
            if (need.StrainWeight <= 0.0)
            {
                continue;
            }

            var cap = caps is null ? need.Cap : (caps.TryGetValue(need.Id, out var effective) ? effective : need.Cap);
            var pressure = Urgency(needs[need.Id], need.UrgencyExponent, cap);
            total += need.StrainWeight * pressure;
            weights += need.StrainWeight;
        }

        return weights != 0.0 ? total / weights : 0.0;
    }
}
