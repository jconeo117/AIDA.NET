# Aida.Web

- **Estado:** creado vacío en N000; se llena en M3.
- **ADR:** [0011](../adr/0011-brazos-y-comunicacion.md), [0014](../adr/0014-windows-y-blazor-server.md), [0015](../adr/0015-forma-del-codigo.md)

## Responsabilidad
La aplicación local: el armazón común (inicio de sesión con PIN, bloqueo, licencia, ajustes) en Blazor Server, y la **raíz de composición**: registra los servicios y monta los brazos que la licencia incluye. El JavaScript, solo en sus módulos de interoperabilidad (micrófono).

## Depende de
Todos los proyectos de `src/`.

## Expone
_Se completa en la unidad que lo construye: las interfaces públicas y los puntos de extensión._

## Lo que no hace
No contiene lógica de dominio: la de cada brazo vive en su proyecto.
