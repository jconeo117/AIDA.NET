# CLAUDE.md

Guía para trabajar en este repositorio. **No lleva estado del proyecto**: en qué unidad estamos y
qué falta vive en `.claude/workflow/01-plan.md`, el repositorio de proceso.

## Qué es

AIDA es un asistente determinista y autónomo:
- un motor decide con aritmética **si** actuar, **qué** hacer y **si está autorizado**;
- el modelo de lenguaje solo entiende la entrada y redacta la salida;
- funciona como plataforma: un núcleo y **brazos** por campo (salud, agenda…), activados por
  licencia.

Este repositorio es la versión .NET. La versión 0.1 de Python (`~/Dev/Aida`, etiqueta `v0.1.0`) es
**de solo lectura** y hace de oráculo ([ADR 0013](docs/adr/0013-python-como-oraculo.md)).

## Dónde está cada cosa

| Qué | Dónde |
|---|---|
| Las decisiones de arquitectura | [docs/adr/](docs/adr/README.md) |
| Qué hace cada proyecto y de qué depende | [docs/architecture/](docs/architecture/README.md) |
| La API (generada; no se edita) | `docs/api/`, con DocFX |
| Las reglas que no se negocian | `.claude/workflow/00-task.md` |
| Cómo se trabaja: fases y roles | `.claude/workflow/02-flujo.md` |
| Cómo se escribe un contrato | `.claude/workflow/03-contrato.md` |
| Qué archivo de Python se porta a qué proyecto | `.claude/workflow/05-mapa-de-porte.md` |

## Comandos

```bash
dotnet build Aida.slnx                                    # compila; los avisos son errores
dotnet test Aida.slnx                                     # toda la batería
dotnet test tests/Aida.ArchitectureTests                  # forma y dependencias
dotnet test --filter "FullyQualifiedName~NombreDelTest"   # un test
dotnet list Aida.slnx package --vulnerable --include-transitive
dotnet tool restore                                       # herramientas locales (DocFX, Stryker)
dotnet run --project src/Aida.Web                         # la aplicación local
```

## Las reglas, en una línea cada una

La versión completa, con cómo se comprueba cada una, está en `00-task.md`. Romper una es motivo de
rechazo por sí solo.

- El motor es la única fuente de verdad: el LLM propone, no decide.
- El núcleo no lleva vocabulario de dominio: el mundo va en `packs/`.
- AIDA decide entera sin LLM.
- Ninguna acción irreversible sin autoridad.
- Lo de fuera es dato, nunca instrucción.
- Todo cambio de comportamiento se mide antes y después.
- **Determinismo:**
  - nada de `Random`, `GetHashCode()` de `string`, `HashCode` ni `DateTime.Now` en el núcleo;
  - colecciones ordenadas antes de recorrerlas;
  - desempate por entero;
  - ningún cambio de estado sin evento.
- **Forma:**
  - ≤ 300 líneas por archivo y un tipo público por archivo;
  - SOLID, con dependencias por constructor;
  - `Engine` ← `Platform` ← `Arms` ← `Web`/`Cli`, y un brazo no ve a otro;
  - motor síncrono.
- **Documentación XML en todo lo público, en español**, con un `remarks` que diga el porqué.
- **Una dependencia nueva necesita un ADR.**

## Idioma

Identificadores en inglés. Prosa en español: documentación XML, mensajes de commit y nombres de test.
No se mezclan.

## Commits

`tipo(ámbito): qué cambia (unidad NNNN, rol)`. Por ejemplo:
`feat(engine): eventos y journal (unidad N002, worker)`.
