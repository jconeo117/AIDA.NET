# Decisiones de arquitectura (ADR)

Cada decisión que da forma a AIDA vive en un archivo propio, numerado. Un ADR responde a cuatro
preguntas: qué problema había, qué se decidió, qué cuesta, y **cómo se comprueba que se sigue
cumpliendo**.

## Reglas

1. **Un ADR aceptado no se edita.** Si la decisión cambia, se escribe uno nuevo que lo sustituye, y
   el viejo pasa a «Sustituido por [NNNN](NNNN-...md)». Lo único que se toca en uno aceptado es esa
   línea de estado.
2. **Toda dependencia externa nueva tiene su ADR** ([0017](0017-cadena-de-suministro.md)).
3. **Un ADR sin «Cómo se hace cumplir» no se acepta.** Una decisión que nada vigila se olvida.
4. Los enlaces son relativos. Un ADR no enlaza a la carpeta de proceso (`.claude/`): el producto se
   puede publicar y el proceso no.

## Índice

| ADR | Decisión | Estado |
|---|---|---|
| [0001](0001-el-motor-decide.md) | El motor decide; el modelo de lenguaje lee y redacta | Aceptado |
| [0002](0002-agnosticismo-de-dominio.md) | El núcleo no sabe de dominio: el mundo va en packs | Aceptado |
| [0003](0003-determinismo.md) | Determinismo: aleatoriedad por clave, orden explícito, desempate por entero | Aceptado |
| [0004](0004-reloj-hibrido.md) | Reloj híbrido y motor síncrono | Aceptado |
| [0005](0005-eventos-y-journal.md) | Ningún cambio de estado sin evento; journal de entradas | Aceptado |
| [0006](0006-owner-y-autoria.md) | `Owner` obligatorio; todo registro tiene autor | Aceptado |
| [0007](0007-autoridad-y-puerta-de-herramientas.md) | Ninguna acción irreversible sin autoridad; una sola puerta de acciones | Aceptado |
| [0008](0008-local-primero.md) | Local primero; la nube con autorización aparte; lo clínico, siempre local | Aceptado |
| [0009](0009-datos-sellados.md) | Datos sellados en reposo con AES-GCM y la clave del equipo protegida con DPAPI | Aceptado |
| [0010](0010-licencia-firmada.md) | Licencia firmada con Ed25519 y comprobada sin conexión | Aceptado |
| [0011](0011-brazos-y-comunicacion.md) | Brazos descritos por datos; se comunican por el motor, nunca entre sí | Aceptado |
| [0012](0012-packs-yaml.md) | Packs en YAML, con el formato de la versión 0.1 | Aceptado |
| [0013](0013-python-como-oraculo.md) | La versión 0.1 de Python es el oráculo: logs dorados byte a byte | Aceptado |
| [0014](0014-windows-y-blazor-server.md) | Solo Windows; .NET 10 y Blazor Server sobre web local | Aceptado |
| [0015](0015-forma-del-codigo.md) | Forma del código: 300 líneas, un tipo público, SOLID, dependencias en una dirección | Aceptado |
| [0016](0016-documentacion-de-la-api.md) | Documentación de la API viva y obligatoria | Aceptado |
| [0017](0017-cadena-de-suministro.md) | Cadena de suministro: cada dependencia con su ADR, versiones con lock, auditoría | Aceptado |
| [0018](0018-herramientas-de-construccion.md) | Herramientas de construcción y prueba: xUnit, ArchUnitNET, analizadores, Stryker.NET, DocFX | Aceptado |

## Plantilla

```markdown
# NNNN — Título en una línea

- **Estado:** Propuesto | Aceptado | Sustituido por [NNNN](NNNN-...md)
- **Fecha:** AAAA-MM-DD
- **Relacionados:** [NNNN](NNNN-...md)

## Contexto
Qué problema hay, y qué se rompe si no se decide.

## Decisión
Qué se hace, en frases que se puedan comprobar.

## Consecuencias
Lo que se gana, lo que cuesta y lo que queda prohibido.

## Cómo se hace cumplir
El test, el analizador o la revisión que falla si se incumple.
```
