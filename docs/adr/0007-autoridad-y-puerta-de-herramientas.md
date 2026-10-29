# 0007 — Ninguna acción irreversible sin autoridad; una sola puerta de acciones

- **Estado:** Aceptado
- **Fecha:** 2026-10-28
- **Relacionados:** [0001](0001-el-motor-decide.md), [0006](0006-owner-y-autoria.md), [0011](0011-brazos-y-comunicacion.md)

## Contexto
AIDA actúa en el mundo: confirma códigos, agenda citas, envía mensajes. Una acción que no se puede
deshacer, ejecutada sin que nadie la autorice, es el peor fallo posible de un asistente autónomo.

## Decisión
- **Toda acción pasa por una sola puerta del motor.** Ningún brazo ejecuta nada por su cuenta.
- **Cada acción declara su reversibilidad en el pack.** El motor la compara con el **techo de
  autonomía** del despliegue.
- **Por debajo del techo, el motor actúa. Por encima, propone**, y la propuesta espera una
  autorización explícita, con caducidad.
- **Autoriza una persona identificada** (perfil y PIN): su clic, firmado con su nombre, es la
  autoridad.
- **No hay modo experto** que desactive la puerta.
- **El orden de construcción es fijo:** no se conecta ninguna herramienta que actúe fuera de AIDA
  hasta que la autoridad esté terminada y validada.

## Consecuencias
- Lo irreversible siempre tiene un humano identificado detrás, y queda en la auditoría.
- Cada acción nueva cuesta declarar su reversibilidad y su techo.

## Cómo se hace cumplir
- Test de arquitectura: los brazos solo llegan a sus herramientas a través de la interfaz de la
  puerta del motor.
- Tests de la puerta: por encima del techo y sin autorización, ninguna acción se ejecuta.
