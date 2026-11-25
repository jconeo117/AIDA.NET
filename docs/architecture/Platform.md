# Aida.Platform

- **Estado:** N007 trajo las rutas, el almacén SQLite, la cadena, la auditoría, la identidad, los roles, los textos
  del armazón y las sesiones. N008 trae la clave del equipo, el sellado, el esquema por brazo y lo que declaran los
  brazos (permisos y clases de hecho). N011 trae la copia de seguridad. N012 trae lo que entiende el asistente
  (`Aida.Platform.Assistant`) y el catálogo de intenciones de un brazo. El resto entra con su primer consumidor.
- **ADR:** [0006](../adr/0006-owner-y-autoria.md), [0009](../adr/0009-datos-sellados.md), [0010](../adr/0010-licencia-firmada.md), [0011](../adr/0011-brazos-y-comunicacion.md), [0020](../adr/0020-servidor-en-red-local.md), [0021](../adr/0021-local-primero-y-salidas-autorizadas.md), [0028](../adr/0028-sqlite-como-almacen-del-servidor.md), [0029](../adr/0029-que-se-sella.md), [0030](../adr/0030-identidad-del-personal.md), [0031](../adr/0031-yaml-en-la-plataforma.md), [0032](../adr/0032-sql-de-cada-brazo.md), [0033](../adr/0033-packs-de-los-brazos.md), [0034](../adr/0034-clave-del-equipo.md), [0035](../adr/0035-lo-que-declara-un-brazo.md), [0036](../adr/0036-copia-de-seguridad.md), [0037](../adr/0037-asistente-sin-llm.md)

## Responsabilidad
Lo común a todos los brazos:
- **rutas del servidor:** dónde están los packs y dónde escribe;
- **almacén:** un archivo SQLite con su esquema versionado. La plataforma abre la conexión, y cada brazo escribe el SQL
  de sus propias tablas, con su versión en `arm_schema` ([0028](../adr/0028-sqlite-como-almacen-del-servidor.md),
  [0032](../adr/0032-sql-de-cada-brazo.md));
- **clave del equipo y sellado:** 32 bytes protegidos con DPAPI en Windows, de los que se derivan con HKDF la clave que
  sella (AES-GCM) y las de las huellas ([0009](../adr/0009-datos-sellados.md), [0034](../adr/0034-clave-del-equipo.md));
- **auditoría encadenada:** cada hecho con su autor, su puesto y la huella de la línea anterior, con el formato de la
  0.1 ([0006](../adr/0006-owner-y-autoria.md));
- **identidad:** usuarios con contraseña y rol, espera tras cinco fallos, primer administrador solo desde el servidor
  ([0030](../adr/0030-identidad-del-personal.md));
- **roles:** los declara `packs/platform/roles.yaml`, con sus permisos. Los permisos y las clases de hecho de los brazos
  los aporta la composición, y la plataforma valida contra la unión ([0035](../adr/0035-lo-que-declara-un-brazo.md));
- **lector de packs:** `PlatformYaml`, el modelo de nodos para `packs/platform/` y para la carpeta de cada brazo
  ([0033](../adr/0033-packs-de-los-brazos.md));
- **textos del armazón:** la sección `shell` de `packs/platform/ui.yaml`
  ([0031](../adr/0031-yaml-en-la-plataforma.md));
- **sesiones:** una por puesto, con bloqueo por inactividad;
- **copia de seguridad:** la base, la configuración y la clave del equipo en bruto, en un tar cifrado con una frase
  (PBKDF2 y AES-GCM por trozos). Al restaurar, se valida y se prepara con la clave ya envuelta; al arrancar, se aplica
  solo si la base no tiene usuarios, y el primero que entra firma el aviso ([0036](../adr/0036-copia-de-seguridad.md)).

Con su primer consumidor entrarán:
- los archivos sellados con su ámbito de texto en claro (`sealed.py`), con el primer brazo que guarde audio o
  documentos;
- la licencia y el descubrimiento de brazos (`arm.yaml`);
- la instancia única y las copias automáticas;
- los destinos de salida declarados ([ADR 0021](../adr/0021-local-primero-y-salidas-autorizadas.md));
- los puertos que implementan los adaptadores: transcripción, lectura de documentos, redacción y mensajería.

## Depende de
`Aida.Engine`. Paquetes: `Microsoft.Data.Sqlite` ([0028](../adr/0028-sqlite-como-almacen-del-servidor.md)),
`YamlDotNet` ([0031](../adr/0031-yaml-en-la-plataforma.md)) y `System.Security.Cryptography.ProtectedData`
([0034](../adr/0034-clave-del-equipo.md)).

## Expone
| Espacio de nombres | Tipos públicos | Para qué |
|---|---|---|
| `Aida.Platform.Paths` | `ServerPaths` | La carpeta de los packs y la de datos, inyectables |
| `Aida.Platform.Storage` | `PlatformDatabase` | Abre el archivo, deja el esquema al día, abre conexiones y aplica el esquema de un brazo |
| `Aida.Platform.Audit` | `AuditLog`, `AuditKind`, `Actor` | Registrar un hecho, solo o en la transacción de un cambio, leer las líneas y verificar la cadena. `AuditKind` es abierto: los brazos declaran los suyos |
| `Aida.Platform.Keys` | `IKeyProtector`, `DpapiKeyProtector`, `DevelopmentKeyProtector`, `KeyProtectors`, `MachineKey` | La clave del equipo, su protección y sus claves derivadas |
| `Aida.Platform.Sealing` | `Sealer` | Sellar y abrir texto con AES-GCM |
| `Aida.Platform.Packs` | `PlatformYaml` | Leer un archivo YAML de un pack a su mapa raíz |
| `Aida.Platform.Identity` | `UserStore`, `UserAccount`, `PasswordHasher`, `IdentityError`, `IdentityException` | Entrar, crear, desactivar y restablecer |
| `Aida.Platform.Roles` | `RoleCatalog`, `RoleSpec`, `PlatformPermission` | Qué roles hay y qué permite cada uno. La plataforma conoce `users.manage` y `snapshot.create` |
| `Aida.Platform.Wording` | `ShellWording` | Los textos del armazón y el tiempo sin uso |
| `Aida.Platform.Sessions` | `SessionTracker`, `SessionView`, `SessionStatus` | Abrir, comprobar, bloquear, desbloquear y cerrar la sesión de un puesto |
| `Aida.Platform.Snapshots` | `SnapshotWriter`, `SnapshotStager`, `SnapshotInstaller`, `PendingRestore`, `RestoreNotice`, `SnapshotError`, `SnapshotException` | Hacer la copia, prepararla, aplicarla al arrancar y firmar el aviso |
| `Aida.Platform.Assistant` | `Understanding`, `Word`, `WordRole`, `WhenMention`, `DayPeriod`, `IntentCatalog`, `IntentExamples`, `TimeVocabulary` | Lo que se entendió de una frase del operador, que produce `Aida.Cognition` y leen los brazos, y el catálogo de intenciones de un brazo, cargado de su pack ([0037](../adr/0037-asistente-sin-llm.md)). Sin vocabulario de dominio |

## Lo que no hace
- No conoce ningún brazo ni ningún dominio: ni la factura ni la historia clínica, ni las tablas de un brazo. Los
  nombres de los roles viven en el pack, y los permisos de los brazos llegan por la composición.
- No implementa adaptadores nativos.
- No escribe páginas ni sabe de HTTP: eso es `Aida.Web`.
