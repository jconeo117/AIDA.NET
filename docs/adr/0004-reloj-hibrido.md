# 0004 — Reloj híbrido y motor síncrono

- **Estado:** Aceptado
- **Fecha:** 2026-10-28
- **Relacionados:** [0003](0003-determinismo.md), [0005](0005-eventos-y-journal.md)

## Contexto
- Un asistente que solo reacciona a eventos no ve que un plazo se acerca si no llega nada.
- Uno que solo avanza a paso fijo malgasta trabajo y responde tarde.
- Y si el motor es asíncrono por dentro, el orden de ejecución depende del planificador: el
  determinismo se rompe.

## Decisión
- **Reloj híbrido**: un latido lento de fondo, más un despertar en cuanto llega una entrada. No es
  paso fijo puro ni cola de eventos pura.
- **El motor es síncrono.** `async` vive solo en los adaptadores (red, audio, disco, servidor web).
  Los adaptadores entregan al buzón del motor resultados ya materializados.

## Consecuencias
- La autonomía (actuar sin que nadie pregunte) sale del latido.
- El servidor web y los adaptadores son asíncronos y no pueden llamar al motor «a medias»: le
  entregan entradas.

## Cómo se hace cumplir
Test de arquitectura: ningún método público de `Aida.Engine` devuelve `Task`, `ValueTask` o
`IAsyncEnumerable`, y `Aida.Engine` no usa `async`.
