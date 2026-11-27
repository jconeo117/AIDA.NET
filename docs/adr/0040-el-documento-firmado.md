# 0040 — El documento firmado vive en la plataforma: borrador del autor, firma con huella, inalterable en la base y con registro de cada acceso

- **Estado:** Aceptado
- **Fecha:** 2026-11-27
- **Relacionados:** precisa el [0029](0029-que-se-sella.md) y el [0038](0038-la-persona-en-la-plataforma.md); [0006](0006-owner-y-autoria.md), [0009](0009-datos-sellados.md), [0011](0011-brazos-y-comunicacion.md), [0039](0039-autorizar-lo-irreversible.md)

## Contexto
- **La historia clínica es inalterable** una vez firmada: se corrige con una nota aclaratoria, con fecha y autor
  (Res. 1995 de 1999). Lo mismo vale para otros documentos de otros rubros: un acta, un informe técnico, la factura
  emitida (ruta 140 del repositorio de proceso, §6).
- **La firma** se hace con usuario y contraseña, repitiendo la contraseña al firmar, mientras el abogado no diga que
  hace falta un certificado digital (ruta 141, A3).
- **Quién accede a una historia** tiene que quedar registrado (Ley 1581, confidencialidad).
- Los eventos de la puerta llevan los argumentos de cada petición. Si el texto clínico viajara en ellos, quedaría en
  claro donde se guarden esos hechos ([0029](0029-que-se-sella.md)).

## Decisión
- **El documento firmado es de la plataforma** (`Aida.Platform.Records`, tabla `records`, esquema 4 de la
  plataforma). Un brazo declara su clase de documento, pone el contenido (campos con nombre y texto) y lo usa por la
  API de la plataforma en su transacción, como a la persona ([0038](0038-la-persona-en-la-plataforma.md)).
- **Un documento es un borrador o está firmado.**
  - El borrador lo edita y lo ve solo su autor. Los demás ven que existe, de quién y de cuándo, sin el contenido.
  - Firmar es irreversible: pasa la puerta con la autorización de su autor, que repite la contraseña
    ([0039](0039-autorizar-lo-irreversible.md)). Solo el autor firma lo suyo.
- **Al firmar** se guarda la huella SHA-256 del contenido canónico: los campos, la clase, la persona, el autor y el
  instante de la firma. Esa huella es la que se verifica.
- **Lo firmado es inalterable en la propia base:** unos *triggers* de SQLite abortan cualquier `UPDATE` o `DELETE`
  de un documento firmado. No depende de que el código se porte bien.
- **El contenido va sellado** ([0029](0029-que-se-sella.md)). En los argumentos de la puerta viaja solo su huella; el
  texto lo recibe la herramienta del brazo aparte.
- **Cada acceso queda en la auditoría:** abrir la lista de documentos de una persona (`records_listed`) y abrir un
  documento (`record_viewed`), con la persona y el documento, nunca con el contenido.
- **Un campo obligatorio vacío** se firma con el texto que el brazo declara en su pack («No consta.»). Antes de pedir
  la contraseña, la pantalla avisa de cuáles son.
- **Las notas aclaratorias y la retención** son de la unidad siguiente (N017). Ningún documento llega a un paciente
  real antes.

## Consecuencias
- La historia clínica, y más adelante otros documentos de otros rubros, comparten la firma, la inalterabilidad y el
  registro de accesos.
- La plataforma conoce «documento», «campo» y «firma», no «historia» ni «diagnóstico»
  ([0002](0002-agnosticismo-de-dominio.md)).
- Corregir algo firmado no es posible hasta N017. Hasta entonces no hay piloto (ruta 82).
- Leer una lista de documentos deja una línea en la auditoría: la auditoría crece con el uso, y es lo que se pide.

## Cómo se hace cumplir
- Tests de la plataforma:
  - un `UPDATE` o un `DELETE` directo sobre un documento firmado falla en la base;
  - la huella se recalcula desde el contenido abierto y coincide;
  - en los bytes de `records` no aparece el texto en claro.
- Tests del brazo:
  - un borrador no lo abre otro profesional;
  - firmar exige la contraseña del autor;
  - un campo obligatorio vacío queda «No consta.»;
  - en los hechos de la puerta no aparece el texto.
- Test de la auditoría: ver la lista y ver un documento dejan su línea.
