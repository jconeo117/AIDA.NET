# 0027 — Determinismo: aleatoriedad por clave, orden explícito, serialización canónica y BLAKE2b propio, comprobados con los registros de la 0.1

- **Estado:** Aceptado
- **Fecha:** 2026-11-14
- **Relacionados:** sustituye a [0023](0023-determinismo-y-blake2b-propio.md); [0004](0004-reloj-hibrido.md), [0013](0013-python-como-oraculo.md), [0017](0017-cadena-de-suministro.md), [0025](0025-eventos-y-lo-derivado.md), [0026](0026-pow-propio.md)

## Contexto
El [0023](0023-determinismo-y-blake2b-propio.md) fijó cómo se garantiza que el mismo estado y las mismas
entradas den la misma decisión y el mismo registro, byte a byte, en cualquier proceso. Dejó dos cosas
para cuando llegaran la deliberación y el bucle:
- **El generador de los sorteos.** En la 0.1, la huella de cada clave siembra un `random.Random`. La
  deliberación (unidad N005) ya reproduce su Mersenne Twister, su siembra y `choices`, y fijó la
  semilla de una corrida en 64 bits. La 0.1 admitía enteros de cualquier tamaño, pero ninguna corrida
  los usó.
- **La prueba entre procesos.** El 0023 pedía «un test que ejecuta el motor en dos procesos distintos y
  compara los logs». Al llegar el bucle (unidad N006) hay una prueba más fuerte, que ya está en el
  repositorio: los registros que generó el bucle de la 0.1.

Medido el 2026-11-14:
- El registro dorado de N002 (`tests/golden/journals/plantilla-1312-300.jsonl`, 363 eventos) sale
  idéntico al regenerarlo con la 0.1.
- Un bucle .NET, sobre lo portado hasta N005, reproduce byte a byte ese registro y otros siete que la
  0.1 genera por guion sobre los packs reales y los sintéticos. Son 4 372 eventos en total, con todas
  las clases de hecho que emite el bucle: propuestas, autorizaciones, rechazos, caducidades, abortos,
  escaladas y mensajes del buzón, con y sin clasificar.

## Decisión
Se mantienen todas las del 0023:
- **La aleatoriedad se deriva por clave.** Cada sorteo sale de `BLAKE2b(semilla, clave…)`, nunca de
  un generador consumido en secuencia y compartido. La huella siembra un Mersenne Twister igual que
  `random.Random(int)` de CPython 3.12, y los pesos se sortean como `choices`.
- **La semilla de una corrida es un entero de 64 bits con signo.** Se escribe al abrir el registro.
- **El orden es explícito.** Un `HashSet` o un `Dictionary` no se recorre sin ordenarlo antes con una
  clave estable. Los identificadores se ordenan por punto de código, como `sorted()` en Python.
- **Se desempata por un entero único** (id, `Seq`), nunca por un `double`.
- **Nada que cambie entre procesos** entra en algo que se persista o se compare:
  - `string.GetHashCode()`;
  - `HashCode`;
  - `System.Random`;
  - la hora del sistema leída directamente.
- **El tiempo entra por `TimeProvider`**, inyectado.
- **Cada mecanismo tiene una sola puerta.**
- **La serialización que se persiste, se firma o se compara es canónica e igual a la de la 0.1:**
  - claves por punto de código;
  - sin espacios;
  - las cadenas, con los escapes de `json.dumps(ensure_ascii=False)` y U+2028 y U+2029 escapados;
  - los enteros, en decimal;
  - los decimales, con el formato de `repr`.
- **BLAKE2b es propio**, según el RFC 7693. No entra ningún paquete para BLAKE2b, para Mersenne Twister
  ni para la serialización.

**Cambia cómo se comprueba la reproducibilidad entre procesos:** la prueban los **registros dorados del
bucle**. El bucle .NET reproduce byte a byte lo que el bucle de la 0.1 escribió con la misma semilla y
las mismas entradas. Eso ya ocurre en otro proceso, con otro runtime y con otro lenguaje. El CI lo
repite además en Windows.

## Consecuencias
- Es una prueba más fuerte que comparar dos procesos .NET entre sí. Dos procesos .NET podrían coincidir
  en un error común, y aquí .NET tiene que coincidir con un oráculo independiente. No hace falta un
  ejecutable de prueba aparte, ni lanzar procesos desde los tests.
- Cada registro dorado depende de todo lo portado (estado, deliberación, sorteo, autoridad y buzón): un
  fallo no dice por sí solo dónde está la diferencia. Por eso los dorados de cada pieza (N002 a N005)
  se mantienen, y localizan el fallo.
- Las semillas mayores que 64 bits no se pueden reproducir. Ninguna corrida de la 0.1 las usó.
- Se mantiene la prohibición de añadir un paquete de BLAKE2b, de aleatoriedad o de serialización al
  núcleo sin un ADR que sustituya a este.

## Cómo se hace cumplir
- **`BannedApiAnalyzers` en el núcleo** prohíbe:
  - `System.Random` y `string.GetHashCode`;
  - `HashCode`;
  - `DateTime.Now` y `DateTime.UtcNow`;
  - `DateTimeOffset.Now` y `DateTimeOffset.UtcNow`.
- **Los dorados de la 0.1** ([0013](0013-python-como-oraculo.md)):
  - los vectores de BLAKE2b y de decimales;
  - los casos de eventos;
  - Mersenne Twister, las claves y `choices`;
  - la deliberación;
  - **los registros del bucle** (`tests/golden/journals/`), con su guion. El test los reproduce byte a
    byte con el bucle .NET en cada ejecución de la batería, también en el CI de Windows.
- **El validator** rechaza un paquete de BLAKE2b, de aleatoriedad o de serialización en
  `Directory.Packages.props`.
