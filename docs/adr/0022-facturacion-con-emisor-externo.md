# 0022 — Facturación: AIDA arma la factura y un proveedor tecnológico la emite

- **Estado:** Aceptado
- **Fecha:** 2026-11-07
- **Relacionados:** [0002](0002-agnosticismo-de-dominio.md), [0007](0007-autoridad-y-puerta-de-herramientas.md), [0011](0011-brazos-y-comunicacion.md), [0017](0017-cadena-de-suministro.md), [0021](0021-local-primero-y-salidas-autorizadas.md)

## Contexto
- En Colombia, la factura electrónica se valida ante la DIAN. Emitirla exige un formato, una firma
  digital y una conexión con la DIAN que mantiene un proveedor tecnológico autorizado.
- Construir y mantener eso dentro de AIDA sería un producto aparte, con su propia certificación.
- La facturación del cliente nace de lo que AIDA ya sabe: la cita, la atención y los productos
  entregados.

## Decisión
- **AIDA arma la factura**: el cliente, los conceptos, las tarifas, los impuestos y lo que se descuenta
  del inventario. Lo hace el brazo de facturación, a partir de los hechos de la atención y del
  inventario, que le llegan por el motor ([0011](0011-brazos-y-comunicacion.md)).
- **Un proveedor tecnológico la emite**, detrás de un puerto: la valida, la firma y la envía a la DIAN.
  AIDA guarda el estado devuelto (aceptada, rechazada o pendiente) y su identificador, el CUFE.
- **Emitir es irreversible.** Pasa por la puerta de acciones ([0007](0007-autoridad-y-puerta-de-herramientas.md))
  con una persona identificada, y el proveedor es un destino declarado ([0021](0021-local-primero-y-salidas-autorizadas.md)).
  Una factura emitida no se edita: se corrige con una nota crédito o débito, que es otra emisión.
- **El proveedor se puede cambiar.** Su formato y su API se quedan en su implementación del puerto, y
  nada fuera de ella los conoce.
- **El núcleo no sabe qué es una factura** ([0002](0002-agnosticismo-de-dominio.md)):
  - el brazo de facturación es genérico, sirve a cualquier negocio y no nombra la salud;
  - lo propio de salud que acompañe a la factura (los RIPS, si el asesor lo confirma) lo aporta el brazo
    de salud a través del motor.
- **Lo que se decide en la unidad del brazo, no aquí:** dónde viven el puerto y la implementación del
  proveedor, respetando que el núcleo no conoce la factura y que un adaptador no depende de un brazo
  ([0011](0011-brazos-y-comunicacion.md)). También el proveedor elegido y su cliente HTTP, que entran
  con su ADR ([0017](0017-cadena-de-suministro.md)).

## Consecuencias
- AIDA depende de un proveedor para emitir. Si cae, la factura queda pendiente y se reintenta, y la
  atención no se bloquea.
- Las credenciales del proveedor son secretos y se guardan sellados.
- Los requisitos legales concretos se confirman con el asesor antes de la unidad: anexos técnicos,
  contingencia, notas y soportes de salud.

## Cómo se hace cumplir
- Test de la puerta: sin autorización de una persona identificada, ninguna factura se emite.
- Test de vocabulario (A2): el núcleo no nombra la factura ni la salud.
- Tests del brazo con un emisor falso: una emisión rechazada deja la factura en su estado y con su
  motivo, sin perder datos, y una emitida no se puede modificar.
