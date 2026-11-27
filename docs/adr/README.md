# Decisiones de arquitectura (ADR)

Cada decisión que da forma a AIDA vive en un archivo propio, numerado. Un ADR responde a cuatro
preguntas: qué problema había, qué se decidió, qué cuesta, y **cómo se comprueba que se sigue
cumpliendo**.

## Reglas

1. **Un ADR aceptado no se edita.** Si la decisión cambia, se escribe uno nuevo que lo sustituye, y
   el viejo pasa a `Sustituido por [NNNN](NNNN-...md)`. Lo único que se toca en uno aceptado es esa
   línea de estado.
2. **Toda dependencia externa nueva tiene su ADR** ([0017](0017-cadena-de-suministro.md)).
3. **Un ADR sin «Cómo se hace cumplir» no se acepta.** Una decisión que nada vigila se olvida.
4. Los enlaces son relativos. Un ADR no enlaza a la carpeta de proceso (`.claude/`): el producto se
   puede publicar y el proceso no.

## Índice

| ADR | Decisión | Estado |
|---|---|---|
| [0001](0001-el-motor-decide.md) | El motor decide; el modelo de lenguaje lee y redacta | Aceptado; precisado por [0037](0037-asistente-sin-llm.md) |
| [0002](0002-agnosticismo-de-dominio.md) | El núcleo no sabe de dominio: el mundo va en packs | Aceptado |
| [0003](0003-determinismo.md) | Determinismo: aleatoriedad por clave, orden explícito, desempate por entero | Sustituido por [0023](0023-determinismo-y-blake2b-propio.md) |
| [0004](0004-reloj-hibrido.md) | Reloj híbrido y motor síncrono | Aceptado |
| [0005](0005-eventos-y-journal.md) | Ningún cambio de estado sin evento; journal de entradas | Sustituido por [0025](0025-eventos-y-lo-derivado.md) |
| [0006](0006-owner-y-autoria.md) | `Owner` obligatorio; todo registro tiene autor | Aceptado |
| [0007](0007-autoridad-y-puerta-de-herramientas.md) | Ninguna acción irreversible sin autoridad; una sola puerta de acciones | Aceptado; precisado por [0035](0035-lo-que-declara-un-brazo.md) y [0037](0037-asistente-sin-llm.md) y [0039](0039-autorizar-lo-irreversible.md) |
| [0008](0008-local-primero.md) | Local primero; la nube con autorización aparte; lo clínico, siempre local | Sustituido por [0021](0021-local-primero-y-salidas-autorizadas.md) |
| [0009](0009-datos-sellados.md) | Datos sellados en reposo con AES-GCM y la clave del equipo protegida con DPAPI | Aceptado; precisado por [0029](0029-que-se-sella.md), [0034](0034-clave-del-equipo.md) y [0036](0036-copia-de-seguridad.md) |
| [0010](0010-licencia-firmada.md) | Licencia firmada con Ed25519 y comprobada sin conexión | Aceptado |
| [0011](0011-brazos-y-comunicacion.md) | Brazos descritos por datos; se comunican por el motor, nunca entre sí | Aceptado; precisado por [0035](0035-lo-que-declara-un-brazo.md) y [0038](0038-la-persona-en-la-plataforma.md) |
| [0012](0012-packs-yaml.md) | Packs en YAML, con el formato de la versión 0.1 | Aceptado |
| [0013](0013-python-como-oraculo.md) | La versión 0.1 de Python es el oráculo: logs dorados byte a byte | Aceptado |
| [0014](0014-windows-y-blazor-server.md) | Solo Windows; .NET 10 y Blazor Server sobre web local | Sustituido por [0020](0020-servidor-en-red-local.md) |
| [0015](0015-forma-del-codigo.md) | Forma del código: 300 líneas, un tipo público, SOLID, dependencias en una dirección | Aceptado |
| [0016](0016-documentacion-de-la-api.md) | Documentación de la API viva y obligatoria | Aceptado |
| [0017](0017-cadena-de-suministro.md) | Cadena de suministro: cada dependencia con su ADR, versiones con lock, auditoría | Aceptado; precisado por [0028](0028-sqlite-como-almacen-del-servidor.md) |
| [0018](0018-herramientas-de-construccion.md) | Herramientas de construcción y prueba: xUnit, ArchUnitNET, analizadores, Stryker.NET, DocFX | Sustituido por [0019](0019-guardianes-propios.md) |
| [0019](0019-guardianes-propios.md) | Herramientas de construcción y prueba, con guardianes propios: xUnit, Roslyn, analizadores, Stryker.NET, DocFX | Aceptado |
| [0020](0020-servidor-en-red-local.md) | Solo Windows; .NET 10 y Blazor Server, servido desde un equipo a los puestos de la red local | Aceptado |
| [0021](0021-local-primero-y-salidas-autorizadas.md) | Local primero; AIDA es el sistema de registro; lo que sale, sale autorizado | Aceptado |
| [0022](0022-facturacion-con-emisor-externo.md) | Facturación: AIDA arma la factura y un proveedor tecnológico la emite | Aceptado |
| [0023](0023-determinismo-y-blake2b-propio.md) | Determinismo: aleatoriedad por clave, orden explícito, serialización canónica igual a la de Python y BLAKE2b propio | Sustituido por [0027](0027-determinismo-y-registros-dorados.md) |
| [0024](0024-yaml-con-yamldotnet.md) | YAML con YamlDotNet, leído como lo lee la 0.1 (reglas de PyYAML y conversiones de pydantic) | Aceptado; ampliado por [0031](0031-yaml-en-la-plataforma.md) |
| [0025](0025-eventos-y-lo-derivado.md) | Eventos: se registra lo que no se puede deducir; la evolución derivada se reproduce. Journal de entradas | Aceptado |
| [0026](0026-pow-propio.md) | Funciones trascendentes propias: el `pow` de ARM optimized-routines, bit a bit igual que la 0.1 | Aceptado |
| [0027](0027-determinismo-y-registros-dorados.md) | Determinismo, con semilla de 64 bits; la reproducibilidad entre procesos la prueban los registros del bucle de la 0.1 | Aceptado |
| [0028](0028-sqlite-como-almacen-del-servidor.md) | SQLite como almacén del servidor, con Microsoft.Data.Sqlite y sin ORM | Aceptado; precisado por [0032](0032-sql-de-cada-brazo.md) |
| [0029](0029-que-se-sella.md) | Qué se sella: los datos de los pacientes y los secretos, no las cuentas del personal | Aceptado; precisado por [0038](0038-la-persona-en-la-plataforma.md) y [0040](0040-el-documento-firmado.md) |
| [0030](0030-identidad-del-personal.md) | Identidad del personal: usuario y contraseña, PBKDF2, espera tras cinco fallos y primer administrador solo desde el servidor | Aceptado; precisado por [0036](0036-copia-de-seguridad.md) |
| [0031](0031-yaml-en-la-plataforma.md) | YamlDotNet también en la plataforma, para los archivos de `packs/platform` | Aceptado; ampliado por [0033](0033-packs-de-los-brazos.md) |
| [0032](0032-sql-de-cada-brazo.md) | Cada brazo escribe el SQL de sus tablas, en el archivo de la plataforma, con su versión de esquema | Aceptado; precisado por [0038](0038-la-persona-en-la-plataforma.md) |
| [0033](0033-packs-de-los-brazos.md) | Los brazos leen su pack con el lector de nodos de la plataforma (`PlatformYaml` público) | Aceptado |
| [0034](0034-clave-del-equipo.md) | La clave del equipo: DPAPI en Windows, archivo sin proteger solo en desarrollo, claves derivadas con HKDF | Aceptado; precisado por [0036](0036-copia-de-seguridad.md) |
| [0035](0035-lo-que-declara-un-brazo.md) | Lo que declara un brazo: permisos, clases de hecho y acciones con su reversibilidad; el techo lo fija la composición | Aceptado; precisado por [0037](0037-asistente-sin-llm.md) y [0039](0039-autorizar-lo-irreversible.md) |
| [0036](0036-copia-de-seguridad.md) | La copia de seguridad: la base, la configuración y la clave del equipo, cifradas con una frase; restaurar solo en un equipo vacío, al arrancar | Aceptado |
| [0037](0037-asistente-sin-llm.md) | El asistente del operador sin LLM: quien entiende propone en el formulario y nunca ejecuta; intenciones y palabras del tiempo en packs | Aceptado |
| [0038](0038-la-persona-en-la-plataforma.md) | La persona vive en la plataforma, con su documento único y el teléfono compartible; un brazo la usa por su API; un paso de esquema puede ser código | Aceptado; precisado por [0040](0040-el-documento-firmado.md) |
| [0039](0039-autorizar-lo-irreversible.md) | Una acción irreversible pasa la puerta solo con la autorización de una persona que la confirma en ese momento (su contraseña) | Aceptado |
| [0040](0040-el-documento-firmado.md) | El documento firmado vive en la plataforma: borrador del autor, firma con huella, inalterable en la base y con registro de cada acceso | Aceptado |

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
