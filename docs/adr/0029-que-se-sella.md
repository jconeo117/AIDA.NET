# 0029 — Qué se sella: los datos de los pacientes y los secretos, no las cuentas del personal

- **Estado:** Aceptado
- **Fecha:** 2026-11-15
- **Relacionados:** precisa el [0009](0009-datos-sellados.md); [0006](0006-owner-y-autoria.md), [0020](0020-servidor-en-red-local.md), [0021](0021-local-primero-y-salidas-autorizadas.md), [0028](0028-sqlite-como-almacen-del-servidor.md)

## Contexto
- El [0009](0009-datos-sellados.md) dice que todo lo guardado que identifique a una persona va sellado con la clave
  del equipo. Leído al pie de la letra, eso incluye al personal: el nombre de quien entra y el de quien firma cada
  hecho de auditoría.
- En la 0.1, la auditoría escribía en claro el nombre del profesional (`platform/audit.py:1-11`), y solo sellaba lo
  que se dijo en la consulta. El rastro de quién hizo qué tiene que poder leerse aunque se pierda la clave.
- Sellar al personal obligaría a traer la clave del equipo (DPAPI) antes de que exista un solo dato de pacientes, y
  a buscar a cada usuario por una huella en vez de por su nombre.

## Decisión
- **Se sella todo lo que identifique a un paciente**: nombre, documento, teléfono, historia clínica, grabaciones y
  cualquier texto que hable de él. También los secretos: claves de servicios externos y textos de la auditoría
  que repitan lo clínico.
- **No se sellan las cuentas del personal**: usuario, nombre, rol y huella de la contraseña. Van en claro en la base
  de datos del servidor ([0028](0028-sqlite-como-almacen-del-servidor.md)), protegida por los permisos de la cuenta
  del servicio.
- **La auditoría escribe en claro** quién, cuándo, desde qué puesto y qué clase de hecho. Lo clínico que acompañe a
  un hecho va aparte, sellado, y la línea solo lleva su huella, como en la 0.1.
- **La clave del equipo entra con el primer brazo que guarde datos de pacientes**, no antes.

## Consecuencias
- La identidad y la auditoría funcionan en cualquier sistema, también en el de desarrollo (Linux), sin DPAPI.
- Quien tenga el archivo de la base de datos sabe quién trabaja en la clínica y con qué rol, pero no lee nada de un
  paciente ni puede entrar sin las contraseñas, de las que solo hay huellas PBKDF2
  ([0030](0030-identidad-del-personal.md)).
- La revisión de cada brazo tiene que decir qué campos sella.

## Cómo se hace cumplir
- Desde el primer brazo con datos de pacientes, tests de sellado: lo escrito en la base no contiene el texto en claro
  de ningún campo de paciente, y otra clave no lo abre ([0009](0009-datos-sellados.md)).
- El contrato de cada brazo lista sus campos sellados, y el validator lo comprueba.
