# 0038 — La persona vive en la plataforma, y un brazo la usa por su API

- **Estado:** Aceptado
- **Fecha:** 2026-11-26
- **Relacionados:** precisa el [0011](0011-brazos-y-comunicacion.md), el [0029](0029-que-se-sella.md) y el [0032](0032-sql-de-cada-brazo.md); [0002](0002-agnosticismo-de-dominio.md), [0009](0009-datos-sellados.md), [0034](0034-clave-del-equipo.md)

## Contexto
- **La agenda guarda a las personas en su propia tabla**, `agenda_clients`, con el nombre y el teléfono sellados. El
  teléfono es la llave: es único, al agendar con un teléfono conocido se usa esa persona, y no se puede cambiar el
  teléfono de alguien al de otra (como en la 0.1, `tool.py:122-130` y `:210-214`).
- **La historia clínica** (la siguiente unidad) necesita identificar a la misma persona con su documento, su fecha de
  nacimiento y su sexo. **La factura** necesitará lo mismo. Un brazo no ve a otro
  ([0011](0011-brazos-y-comunicacion.md)), así que cada brazo tendría su propia copia de los mismos datos personales,
  y las copias acabarían discrepando.
- **En una clínica de oftalmología, varias personas comparten un teléfono:** un niño o un adulto mayor con el del
  acudiente. Con el teléfono como llave, agendar al niño renombraría a la madre.
- El [0032](0032-sql-de-cada-brazo.md) dice que un brazo escribe el SQL de sus tablas, que nunca toca las de la
  plataforma, y que su esquema son pasos SQL. Pero llevar las personas de la agenda a la plataforma exige recalcular la
  huella del teléfono con otra clave, y eso no se hace con SQL.

## Decisión
- **La persona vive en `Aida.Platform` (`Aida.Platform.People`)**, en la tabla `people` del esquema de la plataforma
  (versión 3). Tiene el nombre, el teléfono y, opcionales, el tipo y el número de documento, la fecha de nacimiento y
  el sexo.
  - Todo va sellado salvo el tipo de documento ([0029](0029-que-se-sella.md)).
  - El teléfono y el documento se buscan por una huella HMAC con claves derivadas propias
    ([0034](0034-clave-del-equipo.md)).
- **El teléfono deja de ser único; el documento (tipo y número) lo es.** Lo que identifica a una persona es su
  documento cuando lo tiene.
- **Un brazo usa a la persona por la API de la plataforma** (`PersonStore`), dentro de su propia transacción, como ya
  hace con la auditoría. Nunca con SQL sobre `people`. Sus tablas guardan el id de la persona sin clave foránea.
- **Los tipos de documento y los valores de sexo van en un pack** (`packs/platform/people.yaml`). Cambian con el país y
  con la norma, no con el código.
- **Un paso del esquema de un brazo puede ser código** (`ArmSchemaStep.Code`), además de SQL, cuando mueve datos que
  el SQL no puede mover. Los pasos se aplican en orden y en una sola transacción, como hasta ahora. Precisa el
  [0032](0032-sql-de-cada-brazo.md).
- **La agenda migra al arrancar** (su esquema 3): copia sus contactos a la plataforma conservando sus ids, rehace su
  tabla de citas sin la clave foránea y borra `agenda_clients`, todo en una transacción.
- **Al agendar, la persona se dice explícitamente**: `client=<id>` para una persona conocida, o `client=new` para
  una nueva. **Si el teléfono ya es de alguien, `client=new` no crea a nadie:** se rechaza con `phone_known` y la
  pantalla pregunta si es una de esas personas o una nueva de verdad (`client=new_confirmed`). Así se evitan los
  duplicados por un nombre mal escrito (N015-fix, a petición del usuario).
  - Sin `client`, la regla de la 0.1 se conserva tal cual, para que los dorados sigan valiendo.
  - Con un teléfono que compartan varias personas, se rechaza (`client_ambiguous`).

## Consecuencias
- **La agenda, la historia y la factura ven a la misma persona.** Corregir un nombre en la ficha lo corrige en todas.
- **Un niño puede tener el teléfono de su madre** sin que nadie la renombre.
- **Dos reglas de la 0.1 cambian a propósito:** cambiar el teléfono de alguien al de otra persona ya no se rechaza con
  `phone_taken`, y las dos peticiones del guion dorado que lo comprobaban salen del guion. El resto del guion, y su
  oráculo, no cambian.
- **La plataforma conoce a la persona, pero no a qué la dedica un brazo.** «Cliente» y «paciente» siguen fuera del
  núcleo ([0002](0002-agnosticismo-de-dominio.md)): la plataforma dice «persona».
- **El patrón de migración con código** queda listo para el próximo cambio de esquema con datos reales.

## Cómo se hace cumplir
- Tests de la plataforma:
  - el sellado: la tabla `people` no contiene en claro ningún nombre, teléfono, documento, fecha ni sexo;
  - el documento repetido se rechaza;
  - el mismo teléfono se admite en varias personas.
- Test de migración: una base con la agenda en su esquema 2 arranca con el 3, con los mismos ids, nombres, teléfonos
  y citas, y sin `agenda_clients`. Un fallo a mitad no deja nada a medias.
- El validator comprueba que ningún brazo nombra la tabla `people` en su SQL.
