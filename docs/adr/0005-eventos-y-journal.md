# 0005 — Ningún cambio de estado sin evento; journal de entradas

- **Estado:** Aceptado
- **Fecha:** 2026-10-28
- **Relacionados:** [0003](0003-determinismo.md), [0006](0006-owner-y-autoria.md), [0013](0013-python-como-oraculo.md)

## Contexto
- Si un cambio de estado no deja rastro, ni las capas de arriba ni una auditoría pueden saber que
  ocurrió.
- Si las entradas del exterior no se guardan exactas, el determinismo no se puede verificar.
- Añadir el registro más tarde obliga a reescribir todo lo que debió pasar por él.

## Decisión
- **Todo cambio de estado emite un evento** en un registro de solo añadir, con un `Seq` entero y
  creciente.
- **Toda entrada del exterior se guarda en el journal con su contenido exacto**, antes de que el
  motor la procese.
- Las capas de arriba (progreso, explicación, métricas) **leen eventos**. No llevan contadores
  paralelos.

## Consecuencias
- Para las capas de arriba, un cambio sin evento no ocurrió.
- El registro crece. Su retención la declara el pack, y su borrado también deja evento.

## Cómo se hace cumplir
- Tests del motor: cada operación que muta el estado comprueba el evento emitido.
- Logs dorados ([0013](0013-python-como-oraculo.md)).
