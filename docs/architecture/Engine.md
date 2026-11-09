# Aida.Engine

- **Estado:** en N002 recibe los eventos y su registro, la serialización canónica y BLAKE2b. El resto llega en M1.
- **ADR:** [0001](../adr/0001-el-motor-decide.md), [0002](../adr/0002-agnosticismo-de-dominio.md), [0004](../adr/0004-reloj-hibrido.md), [0005](../adr/0005-eventos-y-journal.md), [0006](../adr/0006-owner-y-autoria.md), [0007](../adr/0007-autoridad-y-puerta-de-herramientas.md), [0012](../adr/0012-packs-yaml.md), [0023](../adr/0023-determinismo-y-blake2b-propio.md)

## Responsabilidad
Decide. Ontología (necesidades, rasgos, metas, acciones, autoridad), estado, eventos y journal, aleatoriedad derivada por clave, disposición y urgencia, deliberación por utilidad, bucle y reloj, puerta de acciones, carga y validación de packs, explicación causal.

## Depende de
Nada de la solución. De fuera, solo lo aprobado por ADR (YAML, cuando lleguen los packs). BLAKE2b y la serialización canónica son propios ([ADR 0023](../adr/0023-determinismo-y-blake2b-propio.md)).

## Expone

| Espacio de nombres | Tipo | Para qué |
|---|---|---|
| `Aida.Engine.Ontology` | `AgentId`, `ActionId` | Identificadores con tipo: el dueño de cada evento y la acción a la que se refiere |
| `Aida.Engine.Events` | `EventKind` | El vocabulario cerrado de hechos que emite el motor |
| `Aida.Engine.Events` | `EventValue` | Un valor escalar del payload: texto, entero, decimal o booleano |
| `Aida.Engine.Events` | `Event` | Un hecho inmutable, con su línea JSON canónica (`ToJson`) |
| `Aida.Engine.Events` | `EventLog` | El registro de solo añadir. `Emit` es la única puerta: numera y calcula la huella del contenido |

Son internos, visibles solo para `Aida.Engine.Tests`:
- `Aida.Engine.Serialization`: `CanonicalJson` y `CodePointComparer`, para escribir igual que la 0.1;
- `Aida.Engine.Hashing`: `Blake2b`, según el RFC 7693;
- `Aida.Engine.Events`: `ContentDigest`, `EventKindNames` y `EventValueKind`.

## Lo que no hace
No hace E/S de red, de audio ni de interfaz. No es asíncrono. No conoce ningún dominio ni ningún brazo.
