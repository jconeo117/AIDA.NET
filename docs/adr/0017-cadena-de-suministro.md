# 0017 — Cadena de suministro: cada dependencia con su ADR, versiones con lock, auditoría

- **Estado:** Aceptado
- **Fecha:** 2026-10-28
- **Relacionados:** [0003](0003-determinismo.md), [0009](0009-datos-sellados.md), [0010](0010-licencia-firmada.md), [0012](0012-packs-yaml.md)

## Contexto
- Las vulnerabilidades en dependencias de terceros son un riesgo en cualquier ecosistema.
- En la versión de Python, una dependencia opcional arrastraba la mayoría de los 146 paquetes
  instalados.
- Cambiar de lenguaje no elimina el riesgo. Lo reducen tener pocas dependencias, saber por qué está
  cada una, fijarlas y vigilarlas.

## Decisión
- **La biblioteca base primero.** Una dependencia externa solo entra si la base no lo resuelve.
- **Cada dependencia externa directa tiene un ADR** con cuatro cosas:
  - quién la usa (proyecto y clase);
  - por qué no basta la base;
  - qué alternativa se descartó;
  - su mantenedor y su licencia.
- **Versiones centralizadas** en `Directory.Packages.props` (`ManagePackageVersionsCentrally`), con
  `packages.lock.json` y restauración en modo bloqueado en el CI.
- **Auditoría**: `NuGetAudit` en la restauración, con los avisos como errores, y
  `dotnet list package --vulnerable --include-transitive` bloqueante en el CI. Dependabot propone las
  actualizaciones.
- **Lo nativo y pesado** (audio, STT, OCR, PDF) vive solo en `Aida.Capture`, aislado del núcleo.

## Consecuencias
- Añadir un paquete cuesta un documento. Es intencionado.
- Las dependencias que ya se prevén entran cada una con su ADR, en la unidad que la use primero:
  - BLAKE2b ([0003](0003-determinismo.md));
  - YAML ([0012](0012-packs-yaml.md));
  - Ed25519 ([0010](0010-licencia-firmada.md));
  - ArchUnitNET;
  - StyleCop;
  - Stryker.NET;
  - DocFX;
  - los motores de captura.

## Cómo se hace cumplir
- El CI falla con una vulnerabilidad conocida o con un lock desactualizado.
- El validator rechaza un paquete nuevo en `Directory.Packages.props` que no tenga su ADR.
- Test de arquitectura: solo `Aida.Capture` referencia los paquetes de captura.
