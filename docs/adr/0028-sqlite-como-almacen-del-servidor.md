# 0028 — SQLite como almacén del servidor, con Microsoft.Data.Sqlite y sin ORM

- **Estado:** Aceptado; precisado por [0032](0032-sql-de-cada-brazo.md)
- **Fecha:** 2026-11-15
- **Relacionados:** [0017](0017-cadena-de-suministro.md), [0020](0020-servidor-en-red-local.md), [0006](0006-owner-y-autoria.md), [0009](0009-datos-sellados.md)

## Contexto
- El servidor de la clínica guarda datos desde varias sesiones a la vez ([0020](0020-servidor-en-red-local.md)).
  El primer dato es el de los usuarios y su auditoría (unidad N007); después vendrán la agenda, la historia clínica y
  la facturación.
- En la 0.1 cada cosa era un archivo JSON o JSONL, escrito por un solo proceso con un candado en memoria. Con
  varias sesiones, un archivo así obliga a reinventar transacciones: crear un usuario y escribir su línea de
  auditoría tienen que ocurrir juntos o no ocurrir.
- La biblioteca base no trae ninguna base de datos embebida.

## Decisión
- **Un único archivo SQLite en la carpeta de datos del servidor** (`aida.db`), abierto con
  **`Microsoft.Data.Sqlite` 10.0.15** y sin ORM: SQL escrito a mano, con parámetros.
  - **Quién lo usa:** solo `Aida.Platform`, en `Aida.Platform.Storage` (`PlatformDatabase`, `Schema`) y en los
    almacenes que reciben su conexión (`UserStore`, `AuditLog` y la cadena). Ningún otro proyecto ve una conexión ni
    escribe SQL: usan los almacenes.
  - **Por qué no basta la base:** .NET no trae una base de datos embebida.
  - **Alternativas descartadas:**
    - archivos JSON como en la 0.1: sin transacciones entre sesiones;
    - Entity Framework Core: una capa grande para unas pocas tablas, con migraciones que generan código que nadie
      revisa;
    - un servidor de base de datos aparte: más que instalar y mantener en una clínica con pocos puestos.
  - **Mantenedor y licencia:** Microsoft (.NET Foundation), MIT. Trae `SQLitePCLRaw` 2.1.13 (Eric Sink, Apache-2.0)
    y la biblioteca nativa `e_sqlite3` (SQLite, dominio público), que incluye el binario de `win-x64`.
- **Tablas `STRICT`** y versión del esquema en `PRAGMA user_version`. Un archivo de una versión más nueva no se
  abre.
- **Modo WAL, claves foráneas activas y espera de 5 s** si otra sesión tiene el archivo bloqueado.
- **Cada operación abre su conexión y su transacción**, y la cierra. Lo que cambia junto se escribe en la misma
  transacción.
- **Precisa el [0017](0017-cadena-de-suministro.md):** la biblioteca nativa de SQLite vive en `Aida.Platform`, no en
  `Aida.Capture`. Es almacenamiento, no captura; es pequeña y no tiene dependencias propias.

## Consecuencias
- La agenda, la historia clínica y la facturación usarán el mismo archivo y las mismas transacciones, cada brazo
  con sus tablas y a través de sus almacenes.
- El esquema crece por versiones numeradas; cada unidad que añade tablas sube `user_version` y su contrato escribe
  el SQL literal.
- Lo que identifique a un paciente se sella antes de guardarse ([0029](0029-que-se-sella.md)): SQLite no cifra.
- La copia de seguridad es copiar un archivo, cuando llegue su unidad.

## Cómo se hace cumplir
- Test de arquitectura: ningún archivo de `src/` fuera de `src/Aida.Platform/` usa `Microsoft.Data.Sqlite`, y
  ningún `.csproj` salvo el de `Aida.Platform` lo referencia.
- `dotnet list package --vulnerable --include-transitive` bloqueante en el CI ([0017](0017-cadena-de-suministro.md)).
- Tests de los almacenes contra un archivo temporal real, no contra una base en memoria.
