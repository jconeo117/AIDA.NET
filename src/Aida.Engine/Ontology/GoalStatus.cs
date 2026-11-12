namespace Aida.Engine.Ontology;

/// <summary>En qué anda una meta del agente. Cuatro estados y no hay más.</summary>
/// <remarks>
/// <para>Lo logrado y lo perdido son distintos aunque los dos dejen de empujar: mueven rasgos opuestos
/// (<c>engine/ontology.py:450-480</c>). En la 0.1 solo se asignan <see cref="Locked"/> y <see cref="Active"/>, al
/// nacer el agente (<c>state.py:274-279</c>); los otros dos los asignará quien cierre metas.</para>
/// </remarks>
public enum GoalStatus
{
    /// <summary>Espera a que se cumpla la meta que la abre (<c>requires_goal</c>). No empuja nada.</summary>
    Locked,

    /// <summary>En curso: empuja su eje con su <c>weight_multiplier</c>.</summary>
    Active,

    /// <summary>Lograda. Deja de empujar.</summary>
    Achieved,

    /// <summary>Perdida. Deja de empujar.</summary>
    Failed,
}
