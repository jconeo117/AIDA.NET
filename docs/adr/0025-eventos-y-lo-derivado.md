# 0025 — Eventos: se registra lo que no se puede deducir; journal de entradas

- **Estado:** Aceptado
- **Fecha:** 2026-11-11
- **Relacionados:** sustituye a [0005](0005-eventos-y-journal.md); [0006](0006-owner-y-autoria.md), [0013](0013-python-como-oraculo.md), [0023](0023-determinismo-y-blake2b-propio.md)

## Contexto
El [0005](0005-eventos-y-journal.md) dice que **todo** cambio de estado emite un evento. Al portar el
estado del agente (unidad N004) se ve que la 0.1 no lo cumple, y que no le hace falta:
- Cada tick, el bucle (`engine/loop.py:125-131`) baja las necesidades, actualiza las rachas en el
  suelo, disipa el hastío, recalcula el desgaste y recompone los derivados.
- Al terminar una acción aplica sus efectos y anota el hastío (`loop.py:224-225`).
- De todo eso, solo el cruce de un umbral de escalada emite evento (`NEED_ESCALATED`,
  `state.py:494-529`). Del resto, ningún paso deja rastro.

Lo que sí registra son los hechos que nadie podría reconstruir: lo que entró de fuera, lo que se
decidió y lo que se autorizó o denegó. El resto es una función de esos hechos, del tick, del pack y
de la semilla.

Emitir un evento por cada paso derivado tiene dos costes:
- El registro crecería en unas *necesidades × ticks* líneas, sin información nueva: todas se pueden
  recalcular.
- Los logs dorados dejarían de compararse con la 0.1 tal cual ([0013](0013-python-como-oraculo.md)):
  el de N002 (`journals/plantilla-1312-300.jsonl`) tiene 363 eventos y ninguno de decaimiento.

## Decisión
- **Emite evento todo cambio de estado que no sea una función de los eventos anteriores, el tick,
  el pack y la semilla.** En concreto:
  - lo que entra de fuera (`INPUT_OBSERVED`, con su contenido exacto en el journal);
  - lo que el motor decide o propone (`ACTION_STARTED`, `ACTION_PROPOSED`…);
  - lo que una autoridad resuelve (`ACTION_AUTHORIZED`, `ACTION_DENIED`, `ACTION_EXPIRED`);
  - el resultado de lo que se hace fuera (`ACTION_COMPLETED`, `ACTION_FAILED`…);
  - la transición que alguien de arriba tiene que conocer, aunque se pueda deducir, cuando sin
    evento tendría que recalcularla con un contador paralelo (`NEED_ESCALATED`).
- **La evolución derivada no se registra: se reproduce.** Por ejemplo, el decaimiento, las rachas,
  el hastío, el desgaste, la recomposición de pesos y temperamento, el avance de lo que se está
  haciendo y los efectos de una acción ya registrada como completada.
- **Lo que muta el estado derivado es interno del motor.** Esos métodos son `internal` y solo los
  llama el bucle, el mismo que emite los eventos de los que dependen. Nadie de fuera puede cambiar el
  estado de un agente sin pasar por un hecho registrado.
- **Se mantiene del 0005:**
  - el registro es de solo añadir, con `Seq` entero y creciente;
  - toda entrada de fuera se guarda en el journal con su contenido exacto, antes de que el motor la
    procese;
  - las capas de arriba (progreso, explicación, métricas) leen eventos o el estado, nunca contadores
    paralelos;
  - el borrado por retención también deja evento.

## Consecuencias
- El registro tiene el mismo tamaño y la misma forma que en la 0.1, y los dorados se comparan byte a
  byte.
- Reconstruir el estado exige el pack y la semilla, además del registro. La persistencia (N007) lo
  comprueba: guardar, reanudar y seguir tiene que dar el mismo registro que no haberse parado.
- Una capa de arriba que quiera ver cómo evoluciona una necesidad la lee del estado (o de la
  explicación, N007), no del registro.
- Queda prohibido que un brazo, la plataforma o la interfaz cambien el estado de un agente por una
  vía que no sea un hecho registrado.

## Cómo se hace cumplir
- **Forma:** los mutadores de `AgentState` son `internal` y la API pública es de solo lectura. Un
  test del motor recorre por reflexión los miembros públicos de `AgentState` y falla si alguno
  muta.
- **Tests del motor:** cada operación que registra un hecho comprueba su evento. Cada operación
  derivada se compara, paso a paso, con la 0.1 en los dorados de trazas de estado (N004).
- **Logs dorados:** el registro de una ejecución real de la 0.1 se reproduce entero
  ([0013](0013-python-como-oraculo.md)).
- **Revisión del validator:** un evento nuevo necesita decir por qué no se puede deducir.
