# 0019 — Herramientas de construcción y prueba, con guardianes propios

- **Estado:** Aceptado
- **Fecha:** 2026-11-02
- **Sustituye a:** [0018](0018-herramientas-de-construccion.md)
- **Relacionados:** [0002](0002-agnosticismo-de-dominio.md), [0003](0003-determinismo.md), [0015](0015-forma-del-codigo.md), [0016](0016-documentacion-de-la-api.md), [0017](0017-cadena-de-suministro.md)

## Contexto
El ADR 0018 eligió ArchUnitNET para comprobar las dependencias entre proyectos. Al diseñar esos tests
se vio que no sirve para eso:
- el compilador quita del ensamblado las referencias a proyectos que no se usan, así que una
  dependencia indebida no se ve hasta que alguien usa un tipo de ella;
- el resto de reglas que tenía asignadas se comprueban con la reflexión de la biblioteca base.

Además, el ADR 0002 pide vigilar el vocabulario de dominio con el árbol de sintaxis, y esa dependencia
no tenía ADR. Y al medir las herramientas se vieron tres cosas que el 0018 no recogía:
- SA1402 solo caza `class` si no se configura;
- DocFX no compila `Aida.Web`, porque su Roslyn no carga el generador de Razor de .NET 10;
- `dotnet-tools.json` está en la raíz, no en `.config/`.

## Decisión

| Herramienta | Para qué | Dónde | Alternativa descartada |
|---|---|---|---|
| xUnit | La batería de tests | `tests/*` | NUnit, MSTest: equivalentes; xUnit es la plantilla de la SDK |
| `Microsoft.CodeAnalysis.CSharp` | Distinguir identificador, cadena y comentario para el guardián de vocabulario | `Aida.Guards` | Expresiones regulares: no distinguen los tres, y tienen alcances distintos |
| `Microsoft.CodeAnalysis.BannedApiAnalyzers` | Las API prohibidas en el núcleo (`BannedSymbols.txt`), también la carga dinámica de ensamblados | `Aida.Engine`, `Aida.Platform` | Revisión manual: no escala |
| `StyleCop.Analyzers` | Un tipo por archivo y nombre del archivo igual al tipo (SA1402, SA1649); el resto, apagado | Todos los proyectos de `src/` | Solo revisión manual |
| Stryker.NET (`dotnet-stryker`, herramienta local) | Mutación con umbral por proyecto | CI y tester | Tablas de mutaciones a mano: lo que se hacía en Python |
| DocFX (`docfx`, herramienta local) | El sitio de la API a partir de los comentarios XML | `docs/` | Sandcastle: solo Windows, menos mantenido |

- **ArchUnitNET sale.** Deja de ser la herramienta que nombra «Cómo se hace cumplir» del
  [ADR 0015](0015-forma-del-codigo.md). El resto de ese ADR sigue vigente.
- **`Microsoft.CodeAnalysis.CSharp`**, con los cuatro puntos del [ADR 0017](0017-cadena-de-suministro.md):
  - quién la usa: `Aida.Guards.Vocabulary.SourceScanner`;
  - por qué no basta la base: la biblioteca base no trae un analizador de C#;
  - qué se descartó: las expresiones regulares, como dice la tabla;
  - mantenedor y licencia: .NET Foundation y Microsoft, MIT.
- **Los guardianes son código propio, en `src/Aida.Guards`.**
  - Es un proyecto de solo desarrollo: ningún proyecto de `src/` lo referencia, así que no viaja en el
    instalador.
  - Vive en `src/` para que quien lo escribe no sea quien lo prueba, y para que Stryker lo mute.
  - Los tests de `tests/Aida.ArchitectureTests` aplican los guardianes al repositorio.
- **Cada regla se mira donde se ve:**
  - las dependencias entre proyectos, en los `.csproj`;
  - los paquetes, en `packages.lock.json`, que trae también los transitivos;
  - el estado estático y el motor síncrono, por reflexión sobre los ensamblados.
- **El vocabulario de dominio ([ADR 0002](0002-agnosticismo-de-dominio.md)) lleva dos listas de datos:**
  - `forbidden-everywhere.txt`, con los términos inequívocos de los brazos reales, se busca también en
    los comentarios;
  - `forbidden-in-code.txt`, con palabras corrientes en español como «disco» o «memoria», se busca en
    identificadores, cadenas y concatenaciones.

  La documentación XML está en español: con una sola lista, escribir «se guarda en disco» rompería la
  compilación. La decisión del ADR 0002 no cambia: esto es cómo se comprueba.
- **StyleCop lee `stylecop.json`,** con `topLevelTypes` para que SA1402 cubra también `interface`,
  `struct`, `enum` y `delegate`. Cuenta todos los tipos de nivel superior, también los `internal`: es más
  estricto que «un tipo público por archivo».
- **Umbral de Stryker (`break`):** 80 en `Aida.Engine` y `Aida.Platform`, donde están la decisión, la
  autoridad y el sellado; 60 en el resto, con más E/S y código de unión. Cada proyecto se activa en la
  unidad que le trae su primer código.
- **DocFX excluye `Aida.Web`** hasta que tenga código propio que documentar.
- **Ninguna de estas herramientas viaja en el producto.** Los analizadores se referencian con
  `PrivateAssets="all"`, y las herramientas locales viven en `dotnet-tools.json`, en la raíz.

## Consecuencias
- Hay código propio que mantener a cambio de una dependencia menos.
- Las listas de vocabulario se amplían con cada brazo, sin tocar el guardián.
- Una palabra ambigua en un comentario pasa sin aviso; la revisa el validator.
- El análisis de vocabulario es solo sintáctico: una constante concatenada con un literal no se detecta.
- Activar más reglas de StyleCop es una decisión con su propio ADR.
- Stryker es lento: corre en el CI y sobre los proyectos que toca cada unidad, no en cada compilación
  local.
- La documentación de las clases `.cs` de `Aida.Web` queda sin comprobar hasta que tengan código propio.

## Cómo se hace cumplir
- Las referencias están en `Directory.Packages.props`, y el CI restaura en modo bloqueado.
- Un paquete de herramienta fuera de esta tabla necesita un ADR que la amplíe.
- `Aida.Guards.Dependencies.DependencyPolicy` no permite a ningún proyecto de `src/` referenciar
  `Aida.Guards`, y lo comprueba `tests/Aida.ArchitectureTests`.
- Stryker falla el CI por debajo del umbral de cada `stryker-config.json`, y DocFX corre en el CI con los
  avisos como errores.
