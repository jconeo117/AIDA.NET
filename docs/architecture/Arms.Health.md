# Aida.Arms.Health

- **Estado:** creado vacío en N000.
  - **N016** trae la historia clínica I: la atención como documento firmado de la plataforma, con apartados de texto
    del pack, borrador del autor, firma con contraseña (la primera acción irreversible de AIDA), «No consta.» con aviso
    y registro de cada acceso.
  - Después: N017 (notas aclaratorias y retención), N018 (examen oftalmológico estructurado y fórmula de lentes), N019
    (CIE-10 oficial y CUPS) y, en M5, el dictado.
- **ADR:** [0002](../adr/0002-agnosticismo-de-dominio.md), [0007](../adr/0007-autoridad-y-puerta-de-herramientas.md), [0011](../adr/0011-brazos-y-comunicacion.md), [0013](../adr/0013-python-como-oraculo.md), [0021](../adr/0021-local-primero-y-salidas-autorizadas.md), [0022](../adr/0022-facturacion-con-emisor-externo.md), [0038](../adr/0038-la-persona-en-la-plataforma.md), [0039](../adr/0039-autorizar-lo-irreversible.md), [0040](../adr/0040-el-documento-firmado.md)

## Responsabilidad
La atención clínica:
- **la historia clínica, de la que AIDA es el sistema de registro.** Cada atención es un documento de la plataforma
  ([0040](../adr/0040-el-documento-firmado.md)) con los apartados que declara `packs/health/encounter.yaml`;
- **AIDA registra, ordena y valida; no emite juicio clínico** (ruta 140 del repositorio de proceso, postura
  provisional hasta el abogado);
- **los permisos:**
  - `health.read`: leer lo firmado y ver qué borradores hay;
  - `health.record`: escribir un borrador propio;
  - `health.sign`: firmar lo propio.

  En el pack por defecto los tiene solo el rol profesional;
- más adelante: su envío a destinos clínicos declarados, los RIPS que acompañan a la factura y, en M5, el dictado.

Sus pantallas (componentes Blazor) se montan en `Aida.Web`.

## Depende de
`Aida.Platform` (la persona, el documento, la auditoría y los roles) y `Aida.Engine` (la puerta de acciones). Usa los
puertos de captura por interfaz, nunca `Aida.Capture` directamente.

## Expone
| Espacio de nombres | Tipos públicos | Para qué |
|---|---|---|
| `Aida.Arms.Health` | `HealthDeclarations` | El id del brazo, sus permisos, su clase de hecho (`health_changed`) y su clase de documento |
| `Aida.Arms.Health.Setup` | `HealthManifest`, `EncounterTemplate`, `EncounterSection` | Las acciones de `arm.yaml` y los apartados de la atención, con sus textos |
| `Aida.Arms.Health.Desk` | `HealthDesk`, `EncounterList`, `EncounterView`, `EncounterLine` | Ver las atenciones de una persona, abrir una, escribir el borrador y firmarlo con la contraseña |

## Lo que no hace
- No referencia otro brazo (agenda, facturación, inventario) ni ningún adaptador. La cita atendida de la que sale una
  atención llega como un número de referencia, desde la web.
- No sugiere diagnósticos ni tratamientos.
- No emite facturas.
