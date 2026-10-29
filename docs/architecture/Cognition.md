# Aida.Cognition

- **Estado:** creado vacío en N000; se llena en M4 (redacción de la nota) y M6 (voz).
- **ADR:** [0001](../adr/0001-el-motor-decide.md), [0008](../adr/0008-local-primero.md)

## Responsabilidad
La voz y la intención:
- los proveedores de lenguaje (local y en la nube) detrás del puerto de redacción de
  `Aida.Platform`, con respaldo en cadena;
- el clasificador de intención contra el catálogo cerrado del pack;
- la guía de redacción a partir de los hechos.

## Depende de
`Aida.Platform` (y, a través de ella, de `Aida.Engine`).

## Expone
_Se completa en la unidad que lo construye: las interfaces públicas y los puntos de extensión._

## Lo que no hace
- No decide ni muta estado: devuelve propuestas y texto.
- `Aida.Engine` no lo referencia ([ADR 0001](../adr/0001-el-motor-decide.md)).
