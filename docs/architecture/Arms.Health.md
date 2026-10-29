# Aida.Arms.Health

- **Estado:** creado vacío en N000; se llena en M4 (tubería) y M6 (aplicación y vivo).
- **ADR:** [0002](../adr/0002-agnosticismo-de-dominio.md), [0007](../adr/0007-autoridad-y-puerta-de-herramientas.md), [0008](../adr/0008-local-primero.md), [0011](../adr/0011-brazos-y-comunicacion.md), [0013](../adr/0013-python-como-oraculo.md)

## Responsabilidad
La consulta: autorización de quien es grabado, frases, apartados, extracción, negación y retractación, cotejo CIE-10, nota por campos, trazabilidad («No consta»), revisión, métricas de revisión, exportación, retención y recuperación de grabaciones. Sus pantallas (componentes Blazor) se montan en `Aida.Web`.

## Depende de
`Aida.Platform`, `Aida.Engine`. Usa los puertos de captura por interfaz, nunca `Aida.Capture` directamente.

## Expone
_Se completa en la unidad que lo construye: las interfaces públicas y los puntos de extensión._

## Lo que no hace
No referencia `Aida.Arms.Agenda` ni ningún adaptador.
