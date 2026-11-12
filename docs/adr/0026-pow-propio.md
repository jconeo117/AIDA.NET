# 0026 — Funciones trascendentes propias: el `pow` de la 0.1, bit a bit

- **Estado:** Aceptado
- **Fecha:** 2026-11-11
- **Relacionados:** [0023](0023-determinismo-y-blake2b-propio.md), [0013](0013-python-como-oraculo.md), [0017](0017-cadena-de-suministro.md), [0020](0020-servidor-en-red-local.md)

## Contexto
La disposición del agente eleva números reales a potencias reales:
- `multiplicador ** intensidad` y `multiplicador ** desgaste`, en `engine/disposition.py:84, 116,
  146, 173, 325, 328`;
- la curva de urgencia, `(1 - valor/techo) ** exponente`, en `disposition.py:209`.

La deliberación (N005) hará lo mismo con `math.exp` (`agents/decide.py:334`). Esos resultados
deciden qué hace AIDA y acaban en el registro.

Python resuelve `x ** y` en `float_pow` (CPython 3.12, `Objects/floatobject.c:745-862`): primero los
casos especiales y luego el `pow` de la biblioteca de C. En el equipo donde se generan los dorados es
glibc 2.44, que en x86-64 con FMA elige su variante compilada con FMA.

En .NET, `Math.Pow` llama al `pow` de la biblioteca de C de cada sistema:
- en Linux, al mismo de glibc;
- en Windows, donde corre AIDA ([0020](0020-servidor-en-red-local.md)) y el CI, al de la UCRT.

No se garantiza que los dos coincidan en el último bit, y el de la UCRT puede cambiar con una
actualización de Windows. Si cambia, la misma ejecución reproducida en otro equipo, o tras una
actualización, decide distinto, y nada falla de forma visible.

Medido el 2026-11-11 con 3 959 357 pares (x, y). Cubren los rangos del motor, todo el rango de los
`double` positivos, x cerca de 1 con exponentes grandes, x subnormal, y resultados cerca del
desbordamiento y de los subnormales:
- **El `pow` de glibc es el de ARM optimized-routines** (`math/pow.c`), del que sale desde la 2.28.
  Compilado con FMA y la contracción de gcc, coincide con `x ** y` de Python en los 3 959 357 pares.
- **La FMA importa.** Sin ninguna contracción salen 629 resultados distintos en los primeros
  1 059 357 pares. Salen 0 con FMA en todo producto cuyos usos son sumas en su mismo bloque básico,
  que es la regla de gcc.
- **Un port a C# con `Math.FusedMultiplyAdd` en esos sitios coincide en los 3 959 357.**
  `FusedMultiplyAdd` redondea una sola vez, por definición IEEE 754, en cualquier procesador y
  sistema. Y el JIT de .NET no fusiona por su cuenta.

## Decisión
- **`pow` es propio.** Se porta a `Aida.Engine` el `pow` de ARM optimized-routines (commit
  `20859d3fd9e797dde592c489c8ef7d383f778620`, `math/pow.c`, `math/pow_log_data.c`,
  `math/exp_data.c`) en su configuración x86-64:
  - tablas de 128 entradas;
  - polinomio de orden 8 para el logaritmo y de orden 5 para la exponencial;
  - las FMA de la regla de gcc.
- **Una sola puerta.** Toda potencia del motor pasa por `PyFloat.Pow`, que reproduce los casos
  especiales de `float_pow` (cero, infinitos, NaN, base negativa, base 1, desbordamiento) antes de
  llamar al núcleo portado.
- **Las tablas se escriben como patrones de bits** (`ulong`), sacados del código compilado, porque
  C# no tiene literales decimales hexadecimales. No se convierten a mano.
- **`exp` sigue el mismo camino cuando llegue su consumidor** (N005): se porta de la misma
  biblioteca, con su propia medición, y entra en este ADR sin otro nuevo.
- **La licencia se respeta.** ARM optimized-routines se distribuye como `MIT OR Apache-2.0 WITH
  LLVM-exception`. Se toma bajo MIT. El aviso de copyright va en la cabecera de cada archivo
  portado y en `THIRD-PARTY-NOTICES.md`.
- **Las funciones trascendentes de la plataforma quedan prohibidas en el núcleo.**

## Consecuencias
- El núcleo mantiene unas 200 líneas de aritmética portada y unas 650 constantes. No cambian: el
  algoritmo está fijado por el commit, y la medición dice que es el de la 0.1.
- El resultado es el mismo en Windows, en Linux y frente a la 0.1, y no depende de actualizaciones
  del sistema.
- Si un día se regenera el oráculo en un equipo cuyo `pow` no es el de la variante con FMA, los
  dorados cambiarían. El generador lo detecta con un par centinela y se niega a escribir.
- Queda prohibido usar `Math.Pow`, `Math.Exp`, `Math.Log` y sus equivalentes de `double` en
  `Aida.Engine` y `Aida.Platform`.

## Cómo se hace cumplir
- `BannedSymbols.txt` prohíbe `Math.Pow`, `Math.Exp`, `Math.Log` (sus dos sobrecargas),
  `Math.Log2`, `Math.Log10`, `double.Pow`, `double.Exp` y `double.Log`.
- Un dorado de pares generado con la 0.1 se reproduce bit a bit en el CI de Windows. Cubre todas las
  entradas de las dos tablas, los rangos del motor, los extremos y los casos especiales de
  `float_pow`.
- El generador de dorados comprueba antes de escribir que el `pow` del oráculo es el de la variante
  con FMA (un par cuyo resultado difiere en la variante sin FMA).
- Revisión del validator: una función trascendente nueva en el núcleo trae su port y su medición.
