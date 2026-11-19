# 0032 — Cada brazo escribe el SQL de sus tablas, en el archivo de la plataforma

- **Estado:** Aceptado
- **Fecha:** 2026-11-15
- **Relacionados:** precisa el [0028](0028-sqlite-como-almacen-del-servidor.md); [0002](0002-agnosticismo-de-dominio.md), [0011](0011-brazos-y-comunicacion.md), [0029](0029-que-se-sella.md)

## Contexto
- El [0028](0028-sqlite-como-almacen-del-servidor.md) dice dos cosas que no caben juntas en cuanto llega el primer brazo
  con datos:
  - **«solo `Aida.Platform` ve una conexión o escribe SQL»;**
  - **«la agenda, la historia clínica y la facturación usarán el mismo archivo, cada brazo con sus tablas y a través de
    sus almacenes».**
- Si el almacén de citas viviera en la plataforma, el núcleo hablaría de citas y pacientes, y el
  [0002](0002-agnosticismo-de-dominio.md) lo prohíbe. Además, quitar un brazo obligaría a tocar la plataforma
  ([0011](0011-brazos-y-comunicacion.md)).
- Un almacén genérico en la plataforma (registros con columnas sin nombre) haría perder lo que justifica SQLite: buscar
  citas por rango de fechas con un índice.
- La agenda (unidad N008) es el primer brazo que guarda datos.

## Decisión
- **Un brazo escribe el SQL de sus propias tablas**, a mano y con parámetros, como la plataforma. Nunca toca las tablas
  de la plataforma ni las de otro brazo.
- **La conexión se la abre la plataforma:** `PlatformDatabase.Open()` pasa a ser pública. Así el archivo, el modo WAL,
  las claves foráneas y la espera de 5 s son los mismos para todos.
- **Cada brazo versiona su esquema por separado:**
  - la plataforma sube a la versión 2 de su esquema, que añade la tabla
    `arm_schema(arm TEXT PRIMARY KEY, version INTEGER NOT NULL) STRICT`;
  - un brazo entrega su esquema como una lista de pasos SQL numerados, que aplica
    `PlatformDatabase.ApplyArmSchema(armId, steps)`;
  - los pasos que faltan se aplican en una transacción;
  - un archivo con un esquema de brazo más nuevo que el código no se abre, igual que el de la plataforma.
- **Lo que cambia junto se escribe en la misma transacción, también entre brazo y plataforma.** El cambio de un brazo y
  su hecho de auditoría van juntos: `AuditLog` expone en público su registro con conexión y transacción.
- **Sus tablas son `STRICT`**, y lo que identifique a un paciente se guarda sellado ([0029](0029-que-se-sella.md)).

## Consecuencias
- La plataforma no aprende el vocabulario de ningún brazo, y un brazo no aprende las pragmas ni la ruta del archivo.
- `Microsoft.Data.Sqlite` llega a los brazos de forma transitiva, a través de `Aida.Platform`. Su versión sigue fijada en
  `Directory.Packages.props`, y la cadena de suministro no cambia.
- Un brazo puede romper sus propias tablas, pero no las ajenas. La revisión de cada contrato comprueba su SQL literal.
- La copia de seguridad sigue siendo copiar un archivo.

## Cómo se hace cumplir
- Test de arquitectura (sustituye al del 0028):
  - `Microsoft.Data.Sqlite` solo aparece en `src/Aida.Platform/` y en `src/Aida.Arms.*/`;
  - ningún `.csproj` lo referencia directamente salvo el de `Aida.Platform`.
- Revisión: el SQL de un brazo solo nombra sus propias tablas. El validator lo comprueba con `grep` sobre los nombres de
  las tablas de la plataforma (`users`, `ledger`, `arm_schema`).
- Tests del esquema por brazo:
  - se aplican los pasos que faltan;
  - una versión más nueva no se abre;
  - dos brazos no se pisan la versión.
