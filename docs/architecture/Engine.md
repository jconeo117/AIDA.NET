# Aida.Engine

- **Estado:** creado vacío en N000; se llena en M1.
- **ADR:** [0001](../adr/0001-el-motor-decide.md), [0002](../adr/0002-agnosticismo-de-dominio.md), [0003](../adr/0003-determinismo.md), [0004](../adr/0004-reloj-hibrido.md), [0005](../adr/0005-eventos-y-journal.md), [0006](../adr/0006-owner-y-autoria.md), [0007](../adr/0007-autoridad-y-puerta-de-herramientas.md), [0012](../adr/0012-packs-yaml.md)

## Responsabilidad
Decide. Ontología (necesidades, rasgos, metas, acciones, autoridad), estado, eventos y journal, aleatoriedad derivada por clave, disposición y urgencia, deliberación por utilidad, bucle y reloj, puerta de acciones, carga y validación de packs, explicación causal.

## Depende de
Nada de la solución. De fuera, solo lo aprobado por ADR (BLAKE2b, YAML).

## Expone
_Se completa en la unidad que lo construye: las interfaces públicas y los puntos de extensión._

## Lo que no hace
No hace E/S de red, de audio ni de interfaz. No es asíncrono. No conoce ningún dominio ni ningún brazo.
