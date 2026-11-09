# 0003 — Determinismo: aleatoriedad por clave, orden explícito, desempate por entero

- **Estado:** Sustituido por [0023](0023-determinismo-y-blake2b-propio.md)
- **Fecha:** 2026-10-28
- **Relacionados:** [0004](0004-reloj-hibrido.md), [0005](0005-eventos-y-journal.md), [0013](0013-python-como-oraculo.md)

## Contexto
La garantía central de AIDA es que el mismo estado y las mismas entradas producen la misma decisión
y el mismo log, byte a byte, en cualquier proceso. Romperla no hace fallar ningún test de forma
visible: dos ejecuciones divergen en silencio y se nota semanas después.

## Decisión
- **Aleatoriedad derivada por clave**: cada valor sale de `BLAKE2b(semilla, clave…)`. Nunca de un
  generador consumido en secuencia y compartido.
- **Orden explícito**: un `HashSet` o un `Dictionary` no se recorre sin ordenar antes con una clave
  estable.
- **Desempate por entero único** (id, `Seq`), nunca por `double`.
- **Nada que cambie entre procesos** entra en algo que se persista o se compare:
  - `string.GetHashCode()` (en .NET es aleatorio por proceso);
  - `HashCode`;
  - la hora del sistema leída directamente.
- **El tiempo entra por `TimeProvider`**, inyectado.
- **Una sola puerta por mecanismo**: la misma comprobación no vive en dos sitios con criterios que
  puedan divergir.
- **La serialización que se persiste o se firma es canónica**: claves ordenadas, sin espacios,
  números con formato fijo.

## Consecuencias
- BLAKE2b no está en la biblioteca base de .NET. Necesita un paquete con su propio ADR
  ([0017](0017-cadena-de-suministro.md)), porque hay que coincidir byte a byte con Python
  ([0013](0013-python-como-oraculo.md)).
- Recorrer colecciones cuesta un orden explícito en cada sitio.

## Cómo se hace cumplir
- `BannedApiAnalyzers` en el núcleo: `System.Random`, `string.GetHashCode`, `HashCode`,
  `DateTime.Now`, `DateTime.UtcNow` y `DateTimeOffset.Now`.
- Logs dorados ([0013](0013-python-como-oraculo.md)).
- Un test que ejecuta el motor en dos procesos distintos y compara los logs.
