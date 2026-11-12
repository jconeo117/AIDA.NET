# 0024 — YAML con YamlDotNet, leído como lo lee la 0.1

- **Estado:** Aceptado
- **Fecha:** 2026-11-09
- **Relacionados:** [0012](0012-packs-yaml.md), [0013](0013-python-como-oraculo.md), [0017](0017-cadena-de-suministro.md), [0023](0023-determinismo-y-blake2b-propio.md)

## Contexto
Los packs están en YAML, con el formato de la 0.1 ([0012](0012-packs-yaml.md)). .NET no trae un lector
de YAML. La 0.1 los lee en dos pasos:
- **PyYAML 6.0.3** convierte el texto en valores. Usa las reglas de YAML 1.1: `yes` es un booleano,
  `1e5` sin punto es un texto y `0x10` es un entero;
- **pydantic 2.13** los convierte a los tipos del pack. Es tolerante: acepta un texto `"0.5"` en un
  campo decimal y `true` en un campo decimal, que vale 1.0. Rechaza las claves que no conoce.

Para que un pack cargue igual en las dos versiones ([0013](0013-python-como-oraculo.md)), lo que importa
no es el lector, sino reproducir esos dos pasos.

## Decisión
- **Se usa YamlDotNet** (paquete `YamlDotNet` 18.1.0) **solo en `Aida.Engine`**, y solo su modelo de
  nodos (`YamlStream`, `YamlMappingNode`, `YamlSequenceNode`, `YamlScalarNode`). No se usa su
  deserialización a objetos.
  - **Quién lo usa:** el cargador de packs, `PackLoader`, y los tipos que leen cada parte del pack, en
    `Aida.Engine.Packs`.
  - **Por qué no basta la base:** .NET no lee YAML.
  - **Alternativas descartadas:**
    - un lector propio del subconjunto de YAML que usan los packs: más código propio que mantener,
      en una gramática con muchos casos raros;
    - el cargador en `Aida.Platform`: partiría la validación entre dos proyectos, con los rangos en el
      motor y las claves desconocidas en la plataforma.
  - **Mantenedor y licencia:** Antoine Aubry, MIT. Sin dependencias en `net10.0`.
- **Los escalares se resuelven con las reglas de PyYAML**: sus expresiones regulares de booleano,
  entero, decimal y nulo, y sus conversiones de entero y decimal. Después, cada campo los acepta con la
  tabla de conversiones de pydantic que fija el contrato de la unidad. Lo comprueban los dorados.
- **Más estricto que la 0.1 en cinco cosas**, que hoy no aparecen en ningún pack y que en la 0.1 se
  aceptaban en silencio:
  - una clave repetida en un mapa: en la 0.1 gana la última; aquí, falla la carga;
  - las anclas y los alias (`&`, `*`);
  - las claves de fusión (`<<`);
  - las etiquetas explícitas (`!!str`…);
  - los enteros que no caben en 64 bits, que Python admite sin límite.

  Las cinco hacen fallar la carga con su ruta. Un pack que dependa de ellas tiene que reescribirse sin
  ellas.

## Consecuencias
- El motor tiene su primera dependencia de ejecución. Es pequeña, no arrastra otras y queda dentro de
  `Aida.Engine`.
- El cargador convierte los nodos a mano, campo a campo. Es más código que una deserialización
  automática, pero es lo que permite reproducir las conversiones de pydantic y señalar cada error con
  su ruta (`needs.0.decay_per_tick`).
- Un pack que cargue en la 0.1 y no aquí solo puede deberse a una de las cinco restricciones de arriba.
  Cualquier otra diferencia es un defecto.

## Cómo se hace cumplir
- Logs dorados ([0013](0013-python-como-oraculo.md)):
  - el volcado de cada pack real cargado en .NET coincide byte a byte con el de la 0.1;
  - los packs rotos del generador fallan en las mismas rutas, y con los mismos mensajes cuando el
    mensaje es de AIDA y no de pydantic.
- Tests unitarios de las cinco restricciones.
- El validator rechaza un uso de YamlDotNet fuera de `Aida.Engine.Packs`, y cualquier uso de su
  deserialización a objetos.
