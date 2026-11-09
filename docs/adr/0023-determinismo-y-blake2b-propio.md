# 0023 — Determinismo: aleatoriedad por clave, orden explícito, serialización canónica y BLAKE2b propio

- **Estado:** Aceptado
- **Fecha:** 2026-11-08
- **Relacionados:** sustituye a [0003](0003-determinismo.md); [0005](0005-eventos-y-journal.md), [0013](0013-python-como-oraculo.md), [0017](0017-cadena-de-suministro.md)

## Contexto
La garantía central de AIDA es que el mismo estado y las mismas entradas producen la misma decisión
y el mismo log, byte a byte, en cualquier proceso. Romperla no hace fallar ningún test de forma
visible: dos ejecuciones divergen en silencio y se nota semanas después.

El [0003](0003-determinismo.md) fijó las reglas y dejó dos puntos abiertos que la unidad N002, la
primera que serializa y calcula huellas, tiene que cerrar:
- **De dónde sale BLAKE2b.** No está en la biblioteca base de .NET, y el 0003 lo dejaba en manos de
  un paquete con su ADR.
- **Qué es exactamente la «serialización canónica».** El 0003 hablaba de «números con formato fijo»,
  pero la 0.1 de Python escribe los decimales con `repr`, y los logs dorados tienen que coincidir
  con ella byte a byte ([0013](0013-python-como-oraculo.md)).

Medido el 2026-11-08 contra la 0.1:
- BLAKE2b sin clave da los mismos bytes que `hashlib.blake2b` en los vectores del RFC 7693.
- Escribir un `double` de .NET con el formato de `repr` de Python coincide en 200 020 de 200 020
  valores. Se probó partiendo de la cadena más corta que recupera el mismo valor (`"R"`) y
  colocando la coma y el exponente con las reglas de Python.

## Decisión
- **La aleatoriedad se deriva por clave.** Cada sorteo sale de `BLAKE2b(semilla, clave…)`, nunca de
  un generador consumido en secuencia y compartido. En la 0.1, esa huella siembra un
  `random.Random` (Mersenne Twister), y la versión .NET lo reproduce. Se porta junto con su único
  consumidor, la deliberación.
- **El orden es explícito.** Un `HashSet` o un `Dictionary` no se recorre sin ordenarlo antes con
  una clave estable.
- **Se desempata por un entero único** (id, `Seq`), nunca por un `double`.
- **Nada que cambie entre procesos** entra en algo que se persista o se compare:
  - `string.GetHashCode()`, que en .NET es aleatorio por proceso;
  - `HashCode`;
  - la hora del sistema leída directamente.
- **El tiempo entra por `TimeProvider`**, inyectado.
- **Cada mecanismo tiene una sola puerta**: la misma comprobación no vive en dos sitios con
  criterios que puedan divergir.
- **La serialización que se persiste, se firma o se compara es canónica**, y es la misma que la de
  la 0.1:
  - las claves se ordenan por **punto de código** Unicode, no por unidad UTF-16 (`StringComparer.Ordinal` no
    sirve: coloca `😀` antes que `U+E000`, y Python al revés);
  - sin espacios (`,` y `:`);
  - las cadenas, con los escapes de `json.dumps(ensure_ascii=False)`, y U+2028 y U+2029 escapados;
  - los enteros, en decimal;
  - los decimales, con el formato de `repr` de Python (`0.1`, `1.0`, `-0.0`, `1e-05`, `1e+16`), y
    `NaN`, `Infinity` y `-Infinity` como en Python.
- **BLAKE2b es propio.** Se implementa en `Aida.Engine` según el RFC 7693, sin clave y con huellas
  de 1 a 64 bytes, como tipo interno. No entra ningún paquete para BLAKE2b.
  - Es un algoritmo pequeño, especificado y cerrado, que no va a cambiar.
  - Un paquete añadiría una cadena de suministro a un núcleo que hoy no tiene ninguna dependencia
    de ejecución.
  - Lo que importa es coincidir con Python byte a byte, y eso lo comprueban los dorados, con
    paquete o sin él.
  - Aquí BLAKE2b no cifra ni firma: da huellas de integridad y siembra sorteos. Lo que protege
    datos o firma licencias usa lo que trae la plataforma (DPAPI y las primitivas de
    `System.Security.Cryptography`).

## Consecuencias
- El repositorio mantiene una implementación propia de un hash (unas 150 líneas). Se cubre con los
  vectores del RFC y con los de Python, y Stryker mide esos tests como los de cualquier otro código del
  núcleo.
- Recorrer colecciones cuesta un orden explícito en cada sitio.
- Nadie usa `System.Text.Json` para escribir lo canónico: sus escapes y su formato de números no
  son los de Python. Leer JSON con él sí está permitido.
- Queda prohibido añadir un paquete de BLAKE2b o de serialización al núcleo sin un ADR que
  sustituya a este.

## Cómo se hace cumplir
- `BannedApiAnalyzers` en el núcleo prohíbe `System.Random`, `string.GetHashCode`, `HashCode`,
  `DateTime.Now`, `DateTime.UtcNow`, `DateTimeOffset.Now` y `DateTimeOffset.UtcNow`.
- Logs dorados ([0013](0013-python-como-oraculo.md)): los vectores de BLAKE2b, los de decimales,
  los casos de eventos y el log de eventos de una ejecución real, todos generados con la 0.1.
- El validator rechaza un paquete de BLAKE2b o de serialización en `Directory.Packages.props`.
- Un test ejecuta el motor en dos procesos distintos y compara los logs (llega con el bucle).
