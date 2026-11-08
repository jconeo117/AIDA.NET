# Aida.Arms.Health

- **Estado:** creado vacío en N000; se llena en la suite clínica (M4: atención e historia clínica) y en el dictado (M5).
- **ADR:** [0002](../adr/0002-agnosticismo-de-dominio.md), [0007](../adr/0007-autoridad-y-puerta-de-herramientas.md), [0011](../adr/0011-brazos-y-comunicacion.md), [0013](../adr/0013-python-como-oraculo.md), [0021](../adr/0021-local-primero-y-salidas-autorizadas.md), [0022](../adr/0022-facturacion-con-emisor-externo.md)

## Responsabilidad
La atención clínica:
- **la historia clínica, de la que AIDA es el sistema de registro**, con el pack de oftalmología (CIE-10 del capítulo H, refracción, agudeza visual…);
- **su envío a destinos clínicos declarados** (proveedor de historia clínica, interoperabilidad), por la puerta de acciones;
- **lo propio de salud que acompaña a la factura** (los RIPS, si el asesor lo confirma), que entrega al brazo de facturación a través del motor;
- **en M5, el dictado:** autorización de quien es grabado, frases, apartados, extracción, negación y retractación, cotejo CIE-10, trazabilidad («No consta»), revisión, retención y recuperación de grabaciones.

Sus pantallas (componentes Blazor) se montan en `Aida.Web`.

## Depende de
`Aida.Platform` y `Aida.Engine`. Usa los puertos de captura por interfaz, nunca `Aida.Capture` directamente.

## Expone
_Se completa en la unidad que lo construye: las interfaces públicas y los puntos de extensión._

## Lo que no hace
- No referencia otro brazo (agenda, facturación, inventario) ni ningún adaptador: se comunica por el motor.
- No emite facturas.
