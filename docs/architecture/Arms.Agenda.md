# Aida.Arms.Agenda

- **Estado:** creado vacío en N000; se llena en M7.
- **ADR:** [0007](../adr/0007-autoridad-y-puerta-de-herramientas.md), [0011](../adr/0011-brazos-y-comunicacion.md)

## Responsabilidad
Citas, recursos, bloqueos, ficha del cliente, conversaciones en el mostrador. Sus acciones pasan por la puerta del motor.

## Depende de
`Aida.Platform`, `Aida.Engine`. Usa la mensajería por su puerto.

## Expone
_Se completa en la unidad que lo construye: las interfaces públicas y los puntos de extensión._

## Lo que no hace
No referencia `Aida.Arms.Health` ni ningún adaptador.
