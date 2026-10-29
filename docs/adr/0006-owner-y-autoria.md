# 0006 — `Owner` obligatorio; todo registro tiene autor

- **Estado:** Aceptado
- **Fecha:** 2026-10-28
- **Relacionados:** [0005](0005-eventos-y-journal.md), [0007](0007-autoridad-y-puerta-de-herramientas.md)

## Contexto
- **Dueño:** una operación de memoria o de registro sin dueño explícito impide pasar de un equipo
  local a varios inquilinos sin reescribir.
- **Autor:** un registro sin autor no sirve como prueba ante nadie. En la versión de Python había
  registros firmados por «la sesión» y no por la persona, y hubo que corregirlo.

## Decisión
- **`Owner`** (de quién es el dato) es un parámetro obligatorio y tipado de toda operación de
  memoria, estado y registro. No hay valor por defecto.
- **`Actor`** (quién hizo el cambio: un profesional identificado, o AIDA como agente) es obligatorio
  en todo hecho de auditoría, autorización y cambio hecho por una persona.
- **AIDA nunca firma como una persona.**

## Consecuencias
- Pasar a varios inquilinos es una decisión de despliegue, no un refactor.
- Cada llamada lleva un parámetro más.

## Cómo se hace cumplir
- `Owner` y `Actor` son tipos propios, no cadenas, y no aceptan `null` (`nullable` activado).
- Tests que comprueban el autor de cada hecho de auditoría.
