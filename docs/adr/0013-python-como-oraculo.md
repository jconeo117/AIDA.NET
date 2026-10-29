# 0013 — La versión 0.1 de Python es el oráculo: logs dorados byte a byte

- **Estado:** Aceptado
- **Fecha:** 2026-10-28
- **Relacionados:** [0003](0003-determinismo.md), [0005](0005-eventos-y-journal.md), [0012](0012-packs-yaml.md)

## Contexto
- AIDA se reescribe de Python a .NET.
- El valor del proyecto no está en el código, sino en **lo que decide**: casi dos mil tests, reglas
  calibradas con consultas reales y mediciones (tiempos, fármacos reconocidos, códigos correctos).
- Una reescritura que cambie lo que AIDA decide sin que nadie lo note es el riesgo principal.

## Decisión
- **La versión 0.1 de Python se congela** (etiqueta `v0.1.0`) y pasa a ser la especificación
  ejecutable. Solo recibe arreglos.
- **Logs dorados**: para cada escenario se generan con la 0.1 el log de eventos y las salidas. La
  versión .NET, con las mismas entradas, los mismos packs y la misma semilla, tiene que producirlos
  **idénticos byte a byte**.
- **Medición, donde no puede haber igualdad exacta.** En la captura (transcripción, OCR) los
  motores cambian, y la versión .NET se compara con los números de la 0.1:
  - el tiempo de proceso;
  - los fármacos reconocidos;
  - los códigos de la lista final.

  Un cambio que empeore no entra.
- **Una diferencia en un log dorado es un defecto**, salvo que un ADR decida cambiar ese
  comportamiento. Entonces se regenera el dorado y el ADR lo dice.

## Consecuencias
- Hay que construir el generador de dorados en la 0.1 y mantenerlo mientras dure la migración.
- La serialización tiene que coincidir en los dos lenguajes ([0003](0003-determinismo.md)).
- Al terminar la migración, los dorados se quedan como tests de regresión, y la 0.1 se archiva.

## Cómo se hace cumplir
- `dotnet test --filter Category=Golden` en el CI, bloqueante.
- Los dorados están versionados en `tests/golden/`, con el commit de la 0.1 que los generó.
