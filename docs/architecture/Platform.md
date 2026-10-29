# Aida.Platform

- **Estado:** creado vacío en N000; se llena en M2.
- **ADR:** [0006](../adr/0006-owner-y-autoria.md), [0008](../adr/0008-local-primero.md), [0009](../adr/0009-datos-sellados.md), [0010](../adr/0010-licencia-firmada.md), [0011](../adr/0011-brazos-y-comunicacion.md)

## Responsabilidad
Lo común a todos los brazos: perfiles con PIN, auditoría encadenada, registro de solo añadir, sellado en reposo con DPAPI, licencia, descubrimiento de brazos (`arm.yaml`), rutas del equipo (datos y usuario), claves, instancia única, copia de seguridad. Define los **puertos** que implementan los adaptadores: transcripción, lectura de documentos, redacción, mensajería.

## Depende de
`Aida.Engine`.

## Expone
_Se completa en la unidad que lo construye: las interfaces públicas y los puntos de extensión._

## Lo que no hace
No conoce ningún brazo. No implementa adaptadores nativos.
