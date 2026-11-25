# Aida.Cognition

- **Estado:** creado vacío en N000. **N012** trae entender sin modelo de lenguaje: el clasificador de intención de la
  0.1 y el intérprete de una frase del operador. **N013** le añade las fechas y las horas: el lector de momentos
  («mañana a las 10», «el jueves en la tarde», «20/11»), con las palabras de `packs/platform/time.yaml`. Los
  proveedores de lenguaje llegan con su unidad y su ADR de salida.
- **ADR:** [0001](../adr/0001-el-motor-decide.md), [0021](../adr/0021-local-primero-y-salidas-autorizadas.md), [0037](../adr/0037-asistente-sin-llm.md)

## Responsabilidad
La voz y la intención:
- **el clasificador de intención** contra el catálogo cerrado de un pack (`intent/classify.py` de la 0.1). Construido
  desde un pack de motor se comporta como la 0.1; con el catálogo de un brazo, pliega el texto y quita sus palabras
  vacías;
- **el intérprete**: de una frase saca lo entendido (`Aida.Platform.Assistant.Understanding`), es decir, la intención,
  los momentos (día, hora y parte del día), el teléfono y el papel de cada palabra, y nunca decide ni cambia nada
  ([0037](../adr/0037-asistente-sin-llm.md));
- después: los proveedores de lenguaje (local y en la nube) detrás del puerto de redacción de `Aida.Platform`, con
  respaldo en cadena, y la guía de redacción a partir de los hechos.

## Depende de
`Aida.Platform` (y, a través de ella, de `Aida.Engine`): usa `TextTokens`, `WordBag` y `TextFold` del motor, y los
tipos de lo entendido y el catálogo de la plataforma.

## Expone
| Espacio de nombres | Tipos públicos | Para qué |
|---|---|---|
| `Aida.Cognition.Intent` | `BagOfWordsClassifier` | La intención de una frase, o ninguna si nada alcanza el umbral. El desempate es estricto, como en la 0.1 |
| `Aida.Cognition.Interpretation` | `AssistantInterpreter` | De una frase a lo entendido, contra el día de hoy que le da quien llama. Lo compone la web con el catálogo de cada brazo. El lector de fechas y horas (`WhenReader`, `DayExpressions`, `HourExpressions`) es interno |

## Lo que no hace
- No decide ni muta estado: devuelve lo entendido, y el brazo propone.
- No ve ningún brazo: el catálogo y el vocabulario le llegan por la composición.
- No sale a la red. Hasta que un ADR lo regule, ningún texto del operador sale del equipo.
- `Aida.Engine` no lo referencia ([ADR 0001](../adr/0001-el-motor-decide.md)).
