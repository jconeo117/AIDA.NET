# 0021 — Local primero; AIDA es el sistema de registro; lo que sale, sale autorizado

- **Estado:** Aceptado
- **Fecha:** 2026-11-07
- **Sustituye a:** [0008](0008-local-primero.md)
- **Relacionados:** [0002](0002-agnosticismo-de-dominio.md), [0007](0007-autoridad-y-puerta-de-herramientas.md), [0009](0009-datos-sellados.md), [0020](0020-servidor-en-red-local.md), [0022](0022-facturacion-con-emisor-externo.md)

## Contexto
- El ADR 0008 decía que lo clínico **nunca sale del equipo para guardarse**.
- La suite clínica tiene que mandar la historia clínica a sistemas de terceros:
  - por interoperabilidad: el resumen de atención que exige la normativa colombiana;
  - porque la clínica trabaje con un proveedor de historia clínica.
- Prohibirlo sin excepción hace imposible cumplir. Permitirlo sin regla abre la puerta a que los datos
  salgan sin que nadie lo decida.
- Los datos de personas, y en salud los clínicos, están protegidos por la Ley 1581 en Colombia. Mandarlos
  a un tercero es una transferencia que tiene que estar autorizada, y el país del tercero importa.

## Decisión
- **Todo funciona en la red de la clínica, sin conexión a internet**: decidir, guardar y revisar.
  Igual que el 0008.
- **AIDA es el sistema de registro.** La historia clínica oficial, la agenda, las facturas y el
  inventario se guardan en el servidor, sellados ([0009](0009-datos-sellados.md)). Un proveedor recibe
  una **copia**, y nunca es el único sitio donde está el dato.
- **Que algo salga hacia un tercero es una acción irreversible** y pasa por la puerta de acciones
  ([0007](0007-autoridad-y-puerta-de-herramientas.md)). Eso incluye:
  - enviar la historia clínica o un resumen de atención;
  - emitir una factura;
  - llamar a un modelo de lenguaje o de transcripción en la nube.
- **Cada destino se declara** con quién es, qué recibe, su país y su base legal (obligación normativa o
  consentimiento). La persona titular autoriza lo que no sea una obligación legal, y esa autorización es
  distinta de la de grabar.
- **La nube para procesar** (transcribir o redactar) sigue siendo opcional y se autoriza aparte. El
  despliegue puede declararse «solo en este equipo», y entonces solo salen las obligaciones legales
  declaradas.
- **Lo clínico solo sale hacia destinos declarados como clínicos**: el proveedor de historia clínica o
  la plataforma de interoperabilidad. Nunca, por ejemplo, al canal de mensajería ni a un modelo de
  lenguaje sin autorización de nube.

## Consecuencias
- El modelo local tiene que bastar cuando no hay nube, aunque sea peor y más lento.
- Cada proveedor nuevo exige declarar el destino (país y base legal) y entra en el pliego legal.
- Si un proveedor cae, AIDA sigue trabajando, y el envío queda pendiente y se reintenta.
- Los requisitos concretos de interoperabilidad, de custodia y de retención se confirman con el asesor
  antes de la unidad que los implemente. Este ADR no los da por ciertos.

## Cómo se hace cumplir
- La puerta de acciones no ejecuta un envío hacia un destino sin declarar, ni uno no clínico con datos
  clínicos. Hay tests para los dos casos.
- La puerta de transcripción y de redacción consulta la autorización de nube antes de cada llamada.
  Hay tests con la casilla apagada y con «solo en este equipo».
- La batería no sale a la red: el transporte HTTP real está cortado en los tests.
- Cada envío deja un hecho en la auditoría, con su `Actor`, su destino y su base legal.
