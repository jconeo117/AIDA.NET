# AIDA.NET · la versión actual (C# / .NET 10)

> **Esta es la versión activa de AIDA**: una reescritura completa en .NET de la
> [versión 0.1 en Python](https://github.com/jconeo117/A.I.D.A), que ya no se desarrolla y queda como
> referencia. *This is the active version of AIDA: a full .NET rewrite of the
> [Python 0.1](https://github.com/jconeo117/A.I.D.A), which is no longer developed and serves as the
> reference.*

[![CI](https://github.com/jconeo117/AIDA.NET/actions/workflows/ci.yml/badge.svg)](https://github.com/jconeo117/AIDA.NET/actions/workflows/ci.yml)

**Asistente Inteligente Determinista y Autónomo.** Un secretario virtual cuya motivación es
aritmética, no inferencia: un motor determinista decide **si** actuar, **qué** hacer y **si está
autorizado**. El modelo de lenguaje solo entiende la entrada y redacta la salida; si el modelo se cae,
AIDA sigue trabajando.

*A deterministic assistant: an arithmetic engine decides whether to act, what to do and whether it is
authorized. The language model only reads the input and writes the reply — if it goes down, AIDA keeps
working.*

---

## Qué es · What it is

AIDA es una plataforma: un núcleo y **brazos** por campo, que se activan por licencia.
- **Salud:** historia clínica, nota de la consulta y códigos CIE-10, siempre revisados por el
  profesional.
- **Agenda:** citas, bloqueos y conversaciones con los pacientes.

Se instala en un equipo Windows de la organización, que actúa como servidor, y los demás puestos la
abren desde el navegador por la red local, con HTTPS y un usuario por persona.

*A core plus field-specific "arms" (health, scheduling) enabled by license. One Windows machine serves
it; the other workstations use a browser over the local network.*

## Estado · Status

Este repositorio es la **reescritura en .NET** de la [versión 0.1 en Python](https://github.com/jconeo117/A.I.D.A),
que hace de **oráculo**: cada pieza portada tiene que producir los mismos resultados que la 0.1, byte a
byte, y lo comprueban las pruebas doradas en cada cambio.

*This is the .NET rewrite of the [Python 0.1](https://github.com/jconeo117/A.I.D.A), which acts as the
oracle: every ported piece must reproduce the 0.1's output byte for byte, checked by golden tests.*

| | |
|---|---|
| ✅ Portado · *Ported* | Eventos y registro, ontología y carga del pack |
| 🔄 En curso · *In progress* | Disposición y estado de los agentes |
| ⏳ Pendiente · *Next* | Deliberación, bucle, persistencia, plataforma, brazos e interfaz |

## Cómo está hecho · How it's built

- **El motor decide; el LLM no.** Ninguna acción irreversible sin autorización explícita, y lo que llega
  de fuera es dato, nunca instrucción.
- **Determinismo:** misma semilla y mismas entradas, mismo registro de eventos. No se usan `Random`,
  `DateTime.Now` ni el hash de `string` en el núcleo, y el hash BLAKE2b es una implementación propia
  para no depender de la plataforma.
- **El núcleo no conoce el dominio:** lo que nombra el mundo vive en *packs* YAML.
- **Arquitectura por capas:** `Engine` ← `Platform` ← `Arms` ← `Web`/`Cli`. Las pruebas de
  arquitectura comprueban las dependencias y la forma del código.
- **Calidad:** xUnit, pruebas doradas contra la 0.1, pruebas de mutación con Stryker.NET, analizadores
  con los avisos como errores y CI en Windows con GitHub Actions.
- **Decisiones documentadas** en [ADR](docs/adr/README.md).

## Documentación · Docs

- [Decisiones de arquitectura (ADR)](docs/adr/README.md)
- [Arquitectura por proyecto](docs/architecture/README.md)
- API: se genera con DocFX en `docs/api/`
- [Cambios por versión](CHANGELOG.md)

## Desarrollo · Development

Requisitos: SDK de .NET 10 (fijado en `global.json`). El producto se ejecuta en Windows 10 u 11 de 64
bits; casi todo se compila y se prueba en cualquier sistema, salvo lo que usa DPAPI.

```bash
dotnet build Aida.slnx                      # los avisos son errores
dotnet test Aida.slnx                       # toda la batería
dotnet test tests/Aida.ArchitectureTests    # forma y dependencias
```
