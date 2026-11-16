# 0035 — Lo que declara un brazo: permisos, clases de hecho y acciones con su reversibilidad. El techo lo fija la composición

- **Estado:** Aceptado
- **Fecha:** 2026-11-15
- **Relacionados:** precisa el [0007](0007-autoridad-y-puerta-de-herramientas.md) y el [0011](0011-brazos-y-comunicacion.md); [0002](0002-agnosticismo-de-dominio.md), [0006](0006-owner-y-autoria.md), [0020](0020-servidor-en-red-local.md)

## Contexto
- La plataforma de N007 tiene dos listas cerradas:
  - `RoleCatalog` rechaza todo permiso que no sea `users.manage`;
  - `AuditKind` es un `enum` con las siete clases de hecho del armazón.
- La agenda (unidad N008) necesita un permiso, `agenda.manage`, y una clase de hecho, `agenda_changed`.
  - En la 0.1, todas las clases de todos los brazos vivían en la plataforma (`KINDS`, `audit.py:28-52`).
  - Repetir eso haría que la plataforma conociera cada brazo ([0002](0002-agnosticismo-de-dominio.md),
    [0011](0011-brazos-y-comunicacion.md)).
- El [0007](0007-autoridad-y-puerta-de-herramientas.md) exige que cada acción declare su reversibilidad y que la puerta
  la compare con un techo.
  - En la 0.1, la puerta de la agenda no consultaba ninguna autoridad (`actions.py:52-97`, `app.py:106-112`).
  - El techo de N006 (`AuthoritySpec`) es del pack del mundo, y la agenda no tiene uno.

## Decisión
- **Un brazo declara en su código, como constantes públicas, sus permisos y sus clases de hecho.** La composición
  (`Aida.Web`) se los pasa a la plataforma, que valida contra la unión:
  - `RoleCatalog` recibe los permisos de los brazos además de `users.manage`. Un rol que declara un permiso que nadie
    conoce sigue haciendo fallar el arranque;
  - `AuditKind` deja de ser un `enum` y pasa a ser un tipo con su nombre en el disco (`^[a-z][a-z_]*$`). Las siete clases
    de la plataforma son sus valores estáticos, con el mismo nombre en el disco que hoy;
  - `AuditLog` recibe las clases de los brazos y rechaza las demás, como `KINDS` en la 0.1.
- **Un brazo declara sus acciones y su reversibilidad en su pack**, en el bloque `actions` de `packs/<brazo>/arm.yaml`,
  con los nombres de la ontología (`reversible`, `reversible_with_cost`, `irreversible`).
- **El techo no lo declara el brazo: lo fija la composición.** Para los brazos del servidor es `reversible`.
  - Si un brazo se pusiera su propio techo, la puerta no protegería de nada.
  - Subirlo es una decisión de arquitectura con su ADR, no un ajuste.
- **La puerta rechaza lo que esté por encima del techo, con el motivo `needs_authorization` y sin llamar a la
  herramienta.** Proponer, autorizar con una persona identificada y la caducidad entran con la primera acción
  irreversible (WhatsApp, M6). Hasta entonces, nada irreversible puede ejecutarse por la puerta.

## Consecuencias
- Quitar un brazo es quitar sus constantes de la composición. Sus permisos dejan de valer y `roles.yaml` lo dice al
  arrancar.
- El formato de la línea de auditoría no cambia, y la cadena de N007 sigue verificando.
- Una acción declarada irreversible por error no se ejecuta nunca, que es el fallo seguro. Una acción irreversible
  declarada reversible sí se ejecutaría: la revisión del contrato de cada brazo comprueba su bloque `actions`.

## Cómo se hace cumplir
- Tests de la puerta: una acción por encima del techo emite `action_requested` y `action_rejected` con
  `needs_authorization`, y la herramienta no recibe ninguna llamada ([0007](0007-autoridad-y-puerta-de-herramientas.md)).
- Tests de `RoleCatalog` y `AuditLog`: lo desconocido se rechaza, y lo declarado por un brazo se acepta.
- Test de arquitectura (A2): la plataforma no nombra a ningún brazo.
