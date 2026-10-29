# Aida.Channel

- **Estado:** creado vacío en N000; se llena en M7.
- **ADR:** [0008](../adr/0008-local-primero.md), [0009](../adr/0009-datos-sellados.md)

## Responsabilidad
Implementa el puerto de mensajería: configuración del canal, recogida de mensajes del relé, envío con la ventana de 24 h, almacenamiento sellado de conversaciones.

## Depende de
`Aida.Platform`.

## Expone
_Se completa en la unidad que lo construye: las interfaces públicas y los puntos de extensión._

## Lo que no hace
No decide qué responder: eso es del motor y de los brazos.
