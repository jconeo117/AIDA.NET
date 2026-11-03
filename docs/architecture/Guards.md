# Aida.Guards

- **Estado:** se crea en N001.
- **ADR:** [0002](../adr/0002-agnosticismo-de-dominio.md), [0015](../adr/0015-forma-del-codigo.md), [0017](../adr/0017-cadena-de-suministro.md), [0019](../adr/0019-guardianes-propios.md)

## Responsabilidad
Comprobar las reglas del repositorio. Lee el código, los proyectos y los ensamblados, y devuelve la lista de incumplimientos con qué está mal, dónde y cómo se arregla. Es **solo de desarrollo**: no viaja en el instalador.

| Espacio de nombres | Regla | Qué mira |
|---|---|---|
| `Aida.Guards.Shape` | C1 | Las líneas de cada archivo |
| `Aida.Guards.Dependencies` | C3, A1, D4 | Los `.csproj` y los `packages.lock.json` |
| `Aida.Guards.Reflection` | C4, C6 | Los ensamblados compilados |
| `Aida.Guards.Vocabulary` | A2 | El árbol de sintaxis de `Aida.Engine` y `Aida.Platform` |

## Depende de
Nada de la solución. De fuera, `Microsoft.CodeAnalysis.CSharp` ([ADR 0019](../adr/0019-guardianes-propios.md)).

## Expone
Cada guardián devuelve una lista de `Violation`. La política (`DependencyPolicy`, `PackagePolicy`) y las listas de vocabulario (`Vocabulary/*.txt`) son datos.

**Puntos de extensión:**
- un proyecto nuevo se añade a `DependencyPolicy`;
- un brazo nuevo amplía las listas de vocabulario;
- una familia nueva de paquetes restringidos se añade a `PackagePolicy`.

## Lo que no hace
- No lo referencia ningún proyecto de `src/`.
- No decide nada del producto.
- No ejecuta las reglas sobre el repositorio: eso lo hace `tests/Aida.ArchitectureTests`.
