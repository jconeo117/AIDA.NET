# 0011 — Brazos descritos por datos; se comunican por el motor, nunca entre sí

- **Estado:** Aceptado
- **Fecha:** 2026-10-28
- **Relacionados:** [0002](0002-agnosticismo-de-dominio.md), [0007](0007-autoridad-y-puerta-de-herramientas.md), [0010](0010-licencia-firmada.md), [0015](0015-forma-del-codigo.md)

## Contexto
AIDA crece por brazos: salud, agenda y los que vengan. Si un brazo usa directamente a otro, quitar
uno rompe al otro, la licencia por brazo deja de tener sentido y el grafo de dependencias se enreda.

## Decisión
- **Cada brazo es un proyecto propio** (`Aida.Arms.<Campo>`), descrito por un `arm.yaml` en su pack:
  identificador, nombre y punto de entrada.
- **La plataforma descubre los brazos y los monta** si la licencia los incluye. Un brazo se monta en
  la web como un componente del armazón común.
- **Un brazo no referencia a otro.** Si dos campos tienen que colaborar (por ejemplo, una cita que
  abre una consulta), lo hacen **a través del motor**: uno emite un evento o una acción, y el otro
  la recibe por la puerta común.
- Lo que sea común a varios brazos (identidad, auditoría, métricas, aprendizaje) **sube a la
  plataforma**, no se copia en cada brazo.

## Consecuencias
- Un brazo se puede quitar, licenciar o añadir sin tocar los demás.
- La colaboración entre brazos es más indirecta y exige definir los eventos que comparten.

## Cómo se hace cumplir
Tests de arquitectura (ArchUnitNET):
- ningún `Aida.Arms.X` referencia a `Aida.Arms.Y`;
- `Aida.Engine` y `Aida.Platform` no referencian ningún brazo.
