# Aida.Engine

- **Estado:** N002 trajo los eventos y su registro, la serialización canónica y BLAKE2b. N003 trajo la ontología y el pack: tipos, carga del YAML y validación. N004 trae la disposición y el estado del agente, con su propio `pow`. El resto llega en M1.
- **ADR:** [0001](../adr/0001-el-motor-decide.md), [0002](../adr/0002-agnosticismo-de-dominio.md), [0004](../adr/0004-reloj-hibrido.md), [0025](../adr/0025-eventos-y-lo-derivado.md), [0006](../adr/0006-owner-y-autoria.md), [0007](../adr/0007-autoridad-y-puerta-de-herramientas.md), [0012](../adr/0012-packs-yaml.md), [0023](../adr/0023-determinismo-y-blake2b-propio.md), [0024](../adr/0024-yaml-con-yamldotnet.md), [0026](../adr/0026-pow-propio.md)

## Responsabilidad
Decide. Ontología (necesidades, rasgos, metas, acciones, autoridad), estado, eventos y journal, aleatoriedad derivada por clave, disposición y urgencia, deliberación por utilidad, bucle y reloj, puerta de acciones, carga y validación de packs, explicación causal.

## Depende de
Nada de la solución. De fuera, solo YamlDotNet ([ADR 0024](../adr/0024-yaml-con-yamldotnet.md)), y solo en `Aida.Engine.Packs`. BLAKE2b y la serialización canónica son propios ([ADR 0023](../adr/0023-determinismo-y-blake2b-propio.md)). El `pow` es un port de ARM optimized-routines (MIT), sin paquete ([ADR 0026](../adr/0026-pow-propio.md)).

## Expone

| Espacio de nombres | Tipo | Para qué |
|---|---|---|
| `Aida.Engine.Ontology` | `AgentId`, `ActionId` | Identificadores con tipo: el dueño de cada evento y la acción a la que se refiere |
| `Aida.Engine.Events` | `EventKind` | El vocabulario cerrado de hechos que emite el motor |
| `Aida.Engine.Events` | `EventValue` | Un valor escalar del payload: texto, entero, decimal o booleano |
| `Aida.Engine.Events` | `Event` | Un hecho inmutable, con su línea JSON canónica (`ToJson`) |
| `Aida.Engine.Events` | `EventLog` | El registro de solo añadir. `Emit` es la única puerta: numera y calcula la huella del contenido |
| `Aida.Engine.Ontology` | `NeedId`, `TraitId`, `GoalId`, `PhaseId`, `SituationId`, `IntentId`, `Tag` | Los demás identificadores con tipo del pack |
| `Aida.Engine.Ontology` | `NeedSpec`, `TraitSpec`, `TasteSpec`, `GoalSpec`, `AgentSpec`, `IntentSpec`, `PhaseSpec`, `SituationSpec`, `ActionSpec`, `AuthoritySpec`, `HealthSpec`, `TuningSpec` | Las definiciones del pack. Inmutables: solo las construye el cargador, ya validadas |
| `Aida.Engine.Ontology` | `Reversibility`, `TemperamentKnob` | Los dos vocabularios cerrados del pack |
| `Aida.Engine.Ontology` | `DeclaredMap<TKey, TValue>` | Un mapa que conserva el orden en que el pack declaró sus claves, que es el orden de las sumas de la 0.1 |
| `Aida.Engine.Packs` | `ContentPack` | El pack cargado y válido, con sus índices por identificador (necesidades, rasgos, situaciones y metas) |
| `Aida.Engine.Packs` | `PackLoader` | Lee un pack desde un archivo o un texto YAML. Es la única puerta: lo que devuelve ya pasó todas las comprobaciones |
| `Aida.Engine.Packs` | `PackException`, `PackProblem` | El fallo de carga, con la lista completa de problemas y la ruta de cada uno |
| `Aida.Engine.Ontology` | `GoalStatus` | En qué anda una meta del agente: bloqueada, activa, lograda o fallida |
| `Aida.Engine.Disposition` | `Temperament` | Los seis parámetros de decisión ya compuestos para un agente, que lee la deliberación |
| `Aida.Engine.State` | `PerformingAction` | Lo que el agente está haciendo y cuánto le falta |
| `Aida.Engine.State` | `AgentState` | Cómo está el agente y qué deriva de su personalidad. Lo construye solo `FromSpec`; su API pública es de solo lectura y lo muta el motor ([ADR 0025](../adr/0025-eventos-y-lo-derivado.md)) |

Son internos, visibles solo para `Aida.Engine.Tests`:
- `Aida.Engine.Serialization`: `CanonicalJson` y `CodePointComparer`, para escribir igual que la 0.1;
- `Aida.Engine.Hashing`: `Blake2b`, según el RFC 7693;
- `Aida.Engine.Events`: `ContentDigest`, `EventKindNames` y `EventValueKind`;
- `Aida.Engine.Packs`: los lectores de cada parte del pack, las 15 comprobaciones y la resolución de escalares de PyYAML;
- `Aida.Engine.Serialization`: `PythonText`, para escribir los mensajes como `repr` y `round` de Python;
- `Aida.Engine.Numerics`: `PyFloat` (la única puerta de `pow`, con los casos especiales de Python, y `min` y `max` con su semántica), `AorPow` y sus dos tablas;
- `Aida.Engine.Disposition`: la composición de pesos, aptitud, temple, techos, gustos y temperamento, y la urgencia y el desgaste (`Pressure`, la única puerta de urgencia);
- `Aida.Engine.State`: `NeedDynamics`, el gasto, la reposición con deuda y las rachas en el suelo;
- `Aida.Engine.Ontology`: `IdOrder`, el orden por punto de código de los identificadores.

## Lo que no hace
No hace E/S de red, de audio ni de interfaz. No es asíncrono. No conoce ningún dominio ni ningún brazo.
