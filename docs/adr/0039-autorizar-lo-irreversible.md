# 0039 — Una acción irreversible pasa la puerta solo con la autorización de una persona que la confirma en ese momento

- **Estado:** Aceptado
- **Fecha:** 2026-11-27
- **Relacionados:** precisa el [0007](0007-autoridad-y-puerta-de-herramientas.md) y el [0035](0035-lo-que-declara-un-brazo.md); [0001](0001-el-motor-decide.md), [0006](0006-owner-y-autoria.md), [0030](0030-identidad-del-personal.md)

## Contexto
- El [0035](0035-lo-que-declara-un-brazo.md) dejó la puerta rechazando todo lo que pasa el techo
  (`needs_authorization`), porque «proponer, autorizar con una persona identificada y la caducidad entran con la
  primera acción irreversible». Hasta hoy, nada irreversible podía ejecutarse.
- **La primera acción irreversible es firmar una historia clínica** (N016): lo firmado no se cambia ni se borra; se
  corrige con una nota aclaratoria.
- La 0.1 ya tenía el hecho `action_authorized` (`events.py`), separado del inicio de la acción, porque el permiso y la
  ejecución ocurren en instantes distintos.
- Subir el techo del brazo haría pasar cualquier acción irreversible de ese brazo sin confirmación: la puerta dejaría
  de proteger.

## Decisión
- **Una petición puede llevar una autorización** (`ActionRequest.AuthorizedBy`): el identificador de la persona que la
  confirmó en ese momento.
- **La puerta deja pasar una acción por encima del techo solo si trae autorización.**
  - Antes de ejecutarla registra `action_authorized` con `by` = quien autoriza.
  - Sin autorización, todo sigue como hoy: `needs_authorization` y la herramienta no se llama.
  - Para una acción dentro del techo, la autorización no cambia nada y no se registra.
- **Quién pone la autorización:** el servicio del brazo (el «mostrador»), y solo **después de verificar en ese mismo
  momento la credencial de la persona**: la contraseña, con `UserStore.Verify`, con su espera tras cinco fallos
  ([0030](0030-identidad-del-personal.md)). La sesión abierta no basta: alguien podría usar un puesto con la sesión de
  otro.
- **Un agente nunca autoriza.** El bucle y el asistente no arman peticiones con `AuthorizedBy`. La conversación
  asíncrona de proponer, autorizar y caducar sigue siendo de M6 (WhatsApp), con sus hechos `action_proposed`,
  `action_denied` y `action_expired`.

## Consecuencias
- **La regla «ninguna acción irreversible sin autoridad» se cumple** con una persona identificada en el instante de
  la acción, y queda en los hechos de la puerta.
- Firmar, emitir una factura o enviar una historia a un tercero usan el mismo camino.
- Una petición con `AuthorizedBy` falsificado pasaría la puerta. La puerta confía en quien la arma: por eso solo un
  mostrador la pone, tras verificar, y el validator lo revisa en cada contrato.

## Cómo se hace cumplir
- Tests de la puerta:
  - sin autorización, `needs_authorization` y la herramienta no se llama;
  - con autorización, `action_requested`, `action_authorized` (con `by`) y el cierre, en ese orden;
  - dentro del techo, ningún `action_authorized`.
- Tests del mostrador de salud: con una contraseña equivocada no se arma la petición y no hay ningún hecho de la
  puerta; tras cinco fallos, la espera.
- Revisión: `AuthorizedBy` solo se asigna en un mostrador, justo después de `UserStore.Verify`.
