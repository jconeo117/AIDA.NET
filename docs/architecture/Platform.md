# Aida.Platform

- **Estado:** creado vacío en N000; se llena en M2.
- **ADR:** [0006](../adr/0006-owner-y-autoria.md), [0009](../adr/0009-datos-sellados.md), [0010](../adr/0010-licencia-firmada.md), [0011](../adr/0011-brazos-y-comunicacion.md), [0020](../adr/0020-servidor-en-red-local.md), [0021](../adr/0021-local-primero-y-salidas-autorizadas.md)

## Responsabilidad
Lo común a todos los brazos:
- **identidad:** usuarios con rol (recepción, profesional, caja, administración), cada uno con su secreto, con intentos limitados y espera;
- auditoría encadenada y registro de solo añadir;
- sellado en reposo con DPAPI;
- licencia y descubrimiento de brazos (`arm.yaml`);
- rutas del servidor (datos y usuario), claves, instancia única y copia de seguridad;
- **los destinos de salida declarados**, con quién es, país y base legal ([ADR 0021](../adr/0021-local-primero-y-salidas-autorizadas.md)).

Define los **puertos** que implementan los adaptadores: transcripción, lectura de documentos, redacción y mensajería.

## Depende de
`Aida.Engine`.

## Expone
_Se completa en la unidad que lo construye: las interfaces públicas y los puntos de extensión._

## Lo que no hace
- No conoce ningún brazo ni ningún dominio: ni la factura ni la historia clínica.
- No implementa adaptadores nativos.
