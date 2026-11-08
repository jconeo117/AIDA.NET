# Aida.Web

- **Estado:** creado vacío en N000; se llena en M3.
- **ADR:** [0011](../adr/0011-brazos-y-comunicacion.md), [0015](../adr/0015-forma-del-codigo.md), [0020](../adr/0020-servidor-en-red-local.md)

## Responsabilidad
La aplicación de la clínica, en Blazor Server, servida desde el equipo servidor a los puestos de la red local por HTTPS ([ADR 0020](../adr/0020-servidor-en-red-local.md)). Tiene dos partes:
- **el armazón común:** inicio de sesión por usuario con rol, bloqueo por puesto, licencia y ajustes;
- **la raíz de composición:** registra los servicios y monta los brazos que incluye la licencia.

El JavaScript, solo en sus módulos de interoperabilidad (micrófono).

## Depende de
Todos los proyectos de `src/` salvo `Aida.Guards`.

## Expone
_Se completa en la unidad que lo construye: las interfaces públicas y los puntos de extensión._

## Lo que no hace
- No contiene lógica de dominio: la de cada brazo vive en su proyecto.
- No guarda nada en los puestos: los datos solo viven en el servidor.
- No escucha fuera de la red local.
