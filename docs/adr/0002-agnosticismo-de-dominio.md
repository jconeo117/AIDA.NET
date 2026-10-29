# 0002 — El núcleo no sabe de dominio: el mundo va en packs

- **Estado:** Aceptado
- **Fecha:** 2026-10-28
- **Relacionados:** [0011](0011-brazos-y-comunicacion.md), [0012](0012-packs-yaml.md)

## Contexto
AIDA es una plataforma para cualquier campo (consultorio, peluquería, papelería…). Si el núcleo
nombra conceptos de un campo, cada campo nuevo obliga a tocar el núcleo, y el núcleo acaba siendo
del primer cliente.

## Decisión
- `Aida.Engine` y `Aida.Platform` no contienen vocabulario de dominio: ni en identificadores, ni en
  cadenas, ni en comentarios.
- Lo que nombra el mundo (necesidades, acciones, textos, umbrales, catálogos) vive en un **pack** de
  datos ([0012](0012-packs-yaml.md)).
- El vocabulario propio de un campo solo existe dentro de su brazo (`Aida.Arms.<Campo>`) y de su pack.

## Consecuencias
- Un campo nuevo es un pack, y como mucho un brazo. No se toca el núcleo.
- Los ejemplos de la documentación XML del núcleo tampoco nombran un dominio. Cuesta escribirlos
  abstractos.

## Cómo se hace cumplir
- Test con el árbol de sintaxis (Roslyn) sobre `Aida.Engine` y `Aida.Platform`, contra una lista
  de vocabulario prohibido que crece con cada brazo.
- Sustituye al guardián de AST de la versión de Python (`test_agnosticism.py`).
