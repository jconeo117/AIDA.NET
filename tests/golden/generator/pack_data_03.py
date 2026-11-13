"""Definición del pack sintetico-03 (N004)."""

SINTETICO_03 = """id: sintetico-03
label: Sintético 03
needs:
  - id: calma
    label: Calma
    initial: 0.3
    decay_per_tick: 0.15
    max_debt: 0.5
    escalates_after: 3
    strain_weight: 1.0
    urgency_exponent: 2.0
    cap: 0.75
  - id: foco
    label: Foco
    initial: 0.8
    decay_per_tick: 0.1
    max_debt: 0.2
    escalates_after: 2
    strain_weight: 1.5
    urgency_exponent: 3.5
    cap: 0.9
traits:
  - id: meticuloso
    label: Meticuloso
    need_weight_multipliers:
      calma: 1.2
    gain_multipliers:
      calma: 1.3
    cost_multipliers:
      foco: 0.7
    cap_multipliers:
      calma: 1.4
    temperament_multipliers:
      temperature: 0.8
      loss_aversion: 1.5
situations:
  - id: presion
    label: Presión
    temperament_multipliers:
      temperature: 1.3
goals:
  - id: g_m1
    label: Meta M1
    need: calma
    at_least: 0.6
    weight_multiplier: 1.5
  - id: g_m2
    label: Meta M2
    need: foco
    at_least: 0.8
    requires_goal: g_m1
    weight_multiplier: 2.0
actions:
  - id: meditar
    label: Meditar
    reversibility: reversible
    duration_ticks: 2
    saturation_gain: 0.3
    satisfies:
      calma: 0.8
    effects:
      calma: 0.6
      foco: 0.3
  - id: resolver
    label: Resolver
    reversibility: reversible
    duration_ticks: 1
    saturation_gain: 0.2
    satisfies:
      foco: 0.9
    costs:
      calma: 0.2
    effects:
      calma: -0.2
      foco: 0.7
tuning:
  learning_rate: 0.08
  strain_multipliers:
    temperature: 1.2
    loss_aversion: 1.4
    switch_cost: 1.1
agents:
  - id: robot_01
    label: Robot 01
    traits:
      meticuloso: 1.0
    initial_needs:
      calma: 0.1
      foco: 0.2
    tastes:
      - target: meditar
        multiplier: 1.3
      - target: meditar
        multiplier: 0.7
    goals:
      - id: g_m1
        label: Meta Propia Robot
        need: foco
        at_least: 0.6
        weight_multiplier: 2.8
  - id: robot_02
    label: Robot 02
    traits:
      meticuloso: -1.0
    initial_needs:
      calma: 0.0
      foco: 0.5
    goals:
      - id: g_huerfana
        label: Meta Huérfana
        need: calma
        at_least: 0.5
  - id: robot_03
    label: Robot 03
    traits:
      meticuloso: 0.0
    goals:
      - id: g_r1
        label: Meta R1
        need: calma
        at_least: 0.4
      - id: g_r2
        label: Meta R2
        need: foco
        at_least: 0.6
        requires_goal: g_r1
  - id: robot_04
    label: Robot 04
    traits:
      meticuloso: 0.3
  - id: robot_05
    label: Robot 05
    traits:
      meticuloso: -0.3
  - id: robot_06
    label: Robot 06
    traits:
      meticuloso: 0.8
"""
