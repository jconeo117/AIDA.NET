# 0016 — Documentación de la API viva y obligatoria

- **Estado:** Aceptado
- **Fecha:** 2026-10-28
- **Relacionados:** [0015](0015-forma-del-codigo.md)

## Contexto
- Quien mantiene AIDA necesita saber qué hace cada clase y cada miembro, qué recibe y por qué es
  así, sin leer la implementación.
- La documentación que vive aparte del código se queda atrás. En la versión de Python, el documento
  guía llegó a describir un número de tests que ya no era cierto.

## Decisión
- **Todo lo público lleva comentario XML**:
  - `summary`: qué es o qué hace;
  - `param` y `returns`;
  - `exception`, con cuándo se lanza;
  - **`remarks`, con el porqué**: qué se rompería si se hiciera de otro modo. Es la costumbre de la
    versión de Python, que se conserva.
- **En español.** Los identificadores, en inglés.
- **Ningún comentario XML del núcleo nombra un dominio**, ni en un ejemplo ([0002](0002-agnosticismo-de-dominio.md)).
- **DocFX genera el sitio** (`docs/api/`) a partir de los comentarios, en cada CI. Nadie edita
  `docs/api/` a mano.
- **Cada proyecto tiene su página en `docs/architecture/`**, escrita a mano: responsabilidad,
  dependencias permitidas, puntos de extensión y los ADR que lo gobiernan. Cambia en el mismo
  commit que la forma del proyecto.

## Consecuencias
- La documentación técnica no se queda atrás, porque sin ella no compila.
- Un `summary` que repite el nombre del método cumple la regla y no informa. Lo vigila el validator.

## Cómo se hace cumplir
- `GenerateDocumentationFile` y `CS1591` (falta un comentario XML) como error.
- `docfx --warningsAsErrors` en el CI: falla una referencia rota o un `cref` que no existe.
- El validator compara el diff con `docs/architecture/` cuando cambia la forma de un proyecto.
