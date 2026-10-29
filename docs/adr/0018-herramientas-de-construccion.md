# 0018 — Herramientas de construcción y prueba

- **Estado:** Aceptado
- **Fecha:** 2026-10-28
- **Relacionados:** [0003](0003-determinismo.md), [0015](0015-forma-del-codigo.md), [0016](0016-documentacion-de-la-api.md), [0017](0017-cadena-de-suministro.md)

## Contexto
Las reglas de forma, documentación, determinismo y mutación se comprueban con herramientas. Cada una
es una dependencia externa ([0017](0017-cadena-de-suministro.md)), aunque solo viva en desarrollo y
nunca viaje en el instalador.

## Decisión

| Herramienta | Para qué | Dónde | Alternativa descartada |
|---|---|---|---|
| xUnit | La batería de tests | `tests/*` | NUnit, MSTest: equivalentes; xUnit es la plantilla de la SDK |
| ArchUnitNET (`TngTech.ArchUnitNET.xUnit`) | Dirección de las dependencias, motor síncrono, dónde vive lo nativo | `tests/Aida.ArchitectureTests` | NetArchTest: menos expresivo con las reglas entre ensamblados |
| `Microsoft.CodeAnalysis.BannedApiAnalyzers` | Las API prohibidas en el núcleo (`BannedSymbols.txt`) | `Aida.Engine`, `Aida.Platform` | Revisión manual: no escala |
| `StyleCop.Analyzers` | Un tipo por archivo y nombre del archivo igual al tipo (SA1402, SA1649); el resto, apagado | Todos los proyectos de `src/` | Solo revisión manual |
| Stryker.NET (`dotnet-stryker`, herramienta local) | Mutación con umbral por proyecto | CI y tester | Tablas de mutaciones a mano: lo que se hacía en Python |
| DocFX (`docfx`, herramienta local) | El sitio de la API a partir de los comentarios XML | `docs/` | Sandcastle: solo Windows, menos mantenido |

- **Ninguna viaja en el producto.** Los analizadores se referencian con `PrivateAssets="all"`, y las
  herramientas viven en `.config/dotnet-tools.json`.
- **Las versiones las fija el lock** en la unidad N000 (la que crea la solución). Dependabot propone
  las actualizaciones.

## Consecuencias
- Activar más reglas de StyleCop es una decisión con su propio ADR. Activarlas todas de golpe
  bloquearía el trabajo sin acordar el estilo.
- Stryker es lento: corre en el CI y sobre los proyectos que toca cada unidad, no en cada compilación
  local.

## Cómo se hace cumplir
- Las referencias están en `Directory.Packages.props`, y el CI restaura en modo bloqueado.
- Un paquete de herramienta fuera de esta tabla necesita un ADR que la amplíe.
