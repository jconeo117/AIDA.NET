# Aida.Capture

- **Estado:** creado vacío en N000; se llena en M5.
- **ADR:** [0021](../adr/0021-local-primero-y-salidas-autorizadas.md), [0013](../adr/0013-python-como-oraculo.md), [0017](../adr/0017-cadena-de-suministro.md)

## Responsabilidad
Implementa los puertos de captura de `Aida.Platform`: micrófono y corte por silencio, transcripción local y en la nube, OCR, PDF, conversión de audio. Es el único proyecto con dependencias nativas pesadas.

## Depende de
`Aida.Platform` (y por ella `Aida.Engine`).

## Expone
_Se completa en la unidad que lo construye: las interfaces públicas y los puntos de extensión._

## Lo que no hace
No decide nada: entrega texto. No conoce ningún brazo.
