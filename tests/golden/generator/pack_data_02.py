"""Definición del pack sintetico-02 (N004)."""

SINTETICO_02 = """id: sintetico-02
label: Sintético 02
needs:
  - id: energia
    label: Energía
    initial: 0.4
    decay_per_tick: 0.12
    max_debt: 0.6
    escalates_after: 4
    strain_weight: 1.2
    urgency_exponent: 3.5
    cap: 0.85
  - id: animo
    label: Ánimo
    initial: 0.6
    decay_per_tick: 0.09
    max_debt: 0.4
    escalates_after: 5
    strain_weight: 0.8
    urgency_exponent: 1.0
    cap: 0.95
traits:
  - id: valiente
    label: Valiente
    need_weight_multipliers:
      energia: 1.3
    gain_multipliers:
      energia: 1.1
    cost_multipliers:
      animo: 0.9
    cap_multipliers:
      energia: 1.2
    temperament_multipliers:
      temperature: 1.1
      novelty_weight: 1.2
situations:
  - id: alerta
    label: Alerta
    temperament_multipliers:
      switch_cost: 1.4
goals:
  - id: g_p1
    label: Meta Pack 1
    need: energia
    at_least: 0.7
    weight_multiplier: 1.8
  - id: g_p2
    label: Meta Pack 2
    need: animo
    at_least: 0.8
    requires_goal: g_p1
    weight_multiplier: 2.2
actions:
  - id: descansar
    label: Descansar
    reversibility: reversible
    duration_ticks: 3
    saturation_gain: 0.25
    satisfies:
      energia: 0.6
    effects:
      energia: 0.5
      animo: 0.2
  - id: entrenar
    label: Entrenar
    reversibility: reversible
    duration_ticks: 2
    saturation_gain: 0.15
    satisfies:
      animo: 0.7
    costs:
      energia: 0.3
    effects:
      energia: -0.3
      animo: 0.6
tuning:
  learning_rate: 0.02
  strain_multipliers:
    temperature: 1.4
    switch_cost: 1.3
    novelty_weight: 0.8
agents:
  - id: agente_01
    label: Agente 01
    traits:
      valiente: 1.0
    initial_needs:
      energia: 0.1
      animo: 0.2
    tastes:
      - target: entrenar
        multiplier: 1.5
      - target: entrenar
        multiplier: 0.8
    goals:
      - id: g_p1
        label: Meta Propia Repite
        need: animo
        at_least: 0.5
        weight_multiplier: 3.0
  - id: agente_02
    label: Agente 02
    traits:
      valiente: -1.0
    initial_needs:
      energia: 0.0
      animo: 0.1
    goals:
      - id: g_suelta
        label: Meta Suelta
        need: energia
        at_least: 0.6
  - id: agente_03
    label: Agente 03
    traits:
      valiente: 0.0
    goals:
      - id: g_a
        label: Meta A
        need: energia
        at_least: 0.5
      - id: g_b
        label: Meta B
        need: animo
        at_least: 0.5
        requires_goal: g_a
  - id: agente_04
    label: Agente 04
    traits:
      valiente: 0.4
  - id: agente_05
    label: Agente 05
    traits:
      valiente: -0.4
  - id: agente_06
    label: Agente 06
    traits:
      valiente: 0.7
"""
