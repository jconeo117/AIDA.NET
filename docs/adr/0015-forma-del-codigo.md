# 0015 — Forma del código: 300 líneas, un tipo público, SOLID, dependencias en una dirección

- **Estado:** Aceptado
- **Fecha:** 2026-10-28
- **Relacionados:** [0011](0011-brazos-y-comunicacion.md), [0016](0016-documentacion-de-la-api.md)

## Contexto
- En la versión de Python, 11 archivos pasaron de 500 líneas: la aplicación de salud llegó a 2929,
  con rutas, páginas, JavaScript y orquestación juntos.
- No fue culpa del lenguaje: ningún contrato ponía límites, y lo que no se comprueba no se cumple.
- El código lo escribe sobre todo una IA. La forma tiene que imponerse con herramientas, no con
  buena voluntad.

## Decisión
- **≤ 300 líneas por archivo** (`.cs` y `.razor`), sin contar las líneas de documentación XML.
- **Un tipo público por archivo**, y el archivo se llama como el tipo.
- **SOLID**:
  - una responsabilidad por clase;
  - dependencias por constructor, hacia interfaces, resueltas en una única raíz de composición
    (`Aida.Web` y `Aida.Cli`);
  - sin localizador de servicios;
  - sin estado estático mutable.
- **Dependencias en una dirección**: `Aida.Engine` ← `Aida.Platform` ← `Aida.Arms.*` ← `Aida.Web` /
  `Aida.Cli`. `Aida.Capture` y `Aida.Channel` son adaptadores que dependen hacia dentro, nunca al
  revés.
- **`nullable` activado y los avisos como errores** en todos los proyectos, desde
  `Directory.Build.props`.
- **Tests**: xUnit; un proyecto de tests por proyecto, más `Aida.ArchitectureTests`; Stryker.NET para
  la mutación.

## Consecuencias
- Más archivos y más tipos pequeños.
- Partir una clase que crece es obligatorio, no opcional.
- Las reglas se discuten una vez, aquí, y no en cada revisión.

## Cómo se hace cumplir
- `Aida.ArchitectureTests`:
  - límite de líneas;
  - dirección de las dependencias (ArchUnitNET);
  - ningún `IServiceProvider` fuera de la raíz de composición;
  - ningún campo `static` mutable.
- StyleCop y los analizadores de .NET con los avisos como errores.
- El validator revisa la responsabilidad única, que ninguna herramienta mide.
