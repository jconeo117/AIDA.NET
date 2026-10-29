# 0012 — Packs en YAML, con el formato de la versión 0.1

- **Estado:** Aceptado
- **Fecha:** 2026-10-28
- **Relacionados:** [0002](0002-agnosticismo-de-dominio.md), [0013](0013-python-como-oraculo.md), [0017](0017-cadena-de-suministro.md)

## Contexto
- Lo que nombra el mundo vive en packs ([0002](0002-agnosticismo-de-dominio.md)).
- La versión 0.1 tiene packs calibrados con consultas reales: apartados, léxico, umbrales y
  catálogos.
- Durante la migración, las dos versiones conviven, y el pack de un cliente nuevo (el optometrista)
  se escribe una sola vez.

## Decisión
- **El formato de los packs no cambia en la migración**: el mismo YAML, los mismos campos y las
  mismas validaciones que en la 0.1.
- Los packs se cargan a tipos inmutables, y **la validación falla al cargar, nunca a mitad de una
  operación**.
- Un cambio de formato después de la migración es una decisión con su propio ADR, y lleva versión
  de esquema.

## Consecuencias
- Un pack sirve en las dos versiones mientras dure la migración.
- Leer YAML necesita un paquete externo, con su ADR ([0017](0017-cadena-de-suministro.md)).

## Cómo se hace cumplir
- Tests de carga sobre todos los packs reales del repositorio.
- Test de equivalencia: cada pack cargado en .NET coincide con el volcado de la 0.1
  ([0013](0013-python-como-oraculo.md)).
