# 0037 — El asistente del operador sin LLM: quien entiende propone en el formulario y nunca ejecuta

- **Estado:** Aceptado
- **Fecha:** 2026-11-24
- **Relacionados:** precisa el [0001](0001-el-motor-decide.md), el [0007](0007-autoridad-y-puerta-de-herramientas.md) y el [0035](0035-lo-que-declara-un-brazo.md); [0002](0002-agnosticismo-de-dominio.md), [0021](0021-local-primero-y-salidas-autorizadas.md), [0031](0031-yaml-en-la-plataforma.md), [0033](0033-packs-de-los-brazos.md)

## Contexto
- **La queja del cliente** es que el programa antiguo es incómodo de usar. La ruta 130 (decisión 5) pide un asistente dentro de la
  aplicación: el operador escribe «cita para Ana mañana a las 10» y no recorre cuatro controles.
- **La 0.1 sabe clasificar** una frase contra un catálogo cerrado, por bolsa de palabras y coseno
  (`intent/classify.py`, `textmatch.py`), pero solo para empujar una necesidad del bucle. Ningún brazo declaraba
  intenciones, y nada pasaba de la frase a una acción con sus argumentos.
- **Un modelo de lenguaje entendería mejor**, pero el [0001](0001-el-motor-decide.md) exige que AIDA decida entera sin
  él. Además, mandar al proveedor la frase con el nombre de una persona es una salida, y eso lo regula el
  [0021](0021-local-primero-y-salidas-autorizadas.md).
- **Medido el 2026-11-24** sobre 15 frases de agenda y 3 ajenas, con el clasificador de la 0.1:
  - tal cual, acierta 10 de 15, y una frase ajena puntúa hasta 0.408;
  - con palabras vacías declaradas y el texto plegado, acierta 14 de 15, y las ajenas puntúan 0;
  - con solo dos intenciones en el catálogo, «cancela la cita de Ana» se toma por «agendar» (0.500).

## Decisión
- **Quien entiende propone; nunca ejecuta.**
  - Entender lo hace `Aida.Cognition` y devuelve **lo entendido**: la intención, el teléfono y el papel de cada
    palabra; con N013, también los momentos.
  - Con eso, el brazo arma **una propuesta**: qué panel de su pantalla abrir y con qué relleno.
  - El operador revisa y pulsa **el botón de siempre**, que pasa por la puerta ([0007](0007-autoridad-y-puerta-de-herramientas.md))
    y queda en la auditoría.
  - El asistente no tiene ningún camino propio hacia una acción.
- **Lo entendido vive en `Aida.Platform` (`Aida.Platform.Assistant`)**, porque lo producen `Aida.Cognition` y lo leen
  los brazos, y ni el uno ni los otros se ven entre sí. No lleva vocabulario de dominio.
- **Las palabras van en packs:**
  - cada brazo declara sus intenciones en `packs/<brazo>/intents.yaml`: cada una con sus ejemplos, las palabras vacías
    y el umbral. La composición rechaza una intención que el brazo no declare en código ([0035](0035-lo-que-declara-un-brazo.md));
  - las palabras del tiempo («hoy», «mañana», «lunes», «tarde») van en `packs/platform/time.yaml` (N013);
  - los packs los lee la plataforma, con nodos ([0031](0031-yaml-en-la-plataforma.md)).
- **El clasificador es el de la 0.1.** Con un pack de motor se comporta igual, y lo fijan los dorados. Con el catálogo
  de un brazo, pliega el texto y quita las palabras vacías que el brazo declara, que es lo que `vectorize` ya prevé.
- **Si encaja más de una persona o más de una cita, se listan** y elige el operador. El asistente no elige nunca.
- **Lo tecleado va por POST y nunca en la URL**, como la búsqueda: lleva nombres de personas.
- **Entender no deja auditoría ni evento.** No cambia nada; lo que se ejecuta después queda como siempre.
- **El reparto en tres unidades:**
  - N012: entender y rellenar la agenda, sin fechas. Cancelar y reprogramar abren la ficha;
  - N013: fechas y horas;
  - N014: cancelar y reprogramar sobre la cita concreta.

## Consecuencias
- **AIDA entiende sin modelo de lenguaje y sin sacar nada del equipo.** Entiende peor que un modelo, y lo que no
  entiende lo dice.
- **Cuando llegue un modelo**, producirá lo entendido en el mismo tipo y la propuesta seguirá igual. Lo que decide no
  cambia; solo cambia quién entiende.
- **Un error de comprensión se ve antes de que pase nada:** el operador tiene delante el formulario relleno.
- **Cambiar de idioma o añadir una forma de decir algo** es tocar un pack, no compilar.
- **La intención todavía no empuja el bucle del agente.** Eso llega con el servicio del bucle.

## Cómo se hace cumplir
- Test de arquitectura: A2 sigue vetando el vocabulario de dominio en `Aida.Platform` (también en
  `Aida.Platform.Assistant`). `Aida.Cognition` no ve los brazos, por la política de dependencias.
- Dorados de la 0.1: los tokens, el recuento y el coseno; el clasificador sobre la plantilla y sobre el catálogo de la
  agenda.
- Tests:
  - tras usar el asistente, los eventos de la puerta y la auditoría no cambian;
  - con dos personas que encajan, la propuesta las lista;
  - lo tecleado no aparece en ninguna URL;
  - una intención del pack que el brazo no declara impide arrancar.
- Mutaciones críticas en el contrato de N012: el desempate del clasificador, que el asistente elija él y que ejecute.
