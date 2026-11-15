# Aida.Platform

- **Estado:** N007 trae las rutas, el almacén SQLite, la cadena, la auditoría, la identidad, los roles, los textos del
  armazón y las sesiones. El resto entra con su primer consumidor.
- **ADR:** [0006](../adr/0006-owner-y-autoria.md), [0009](../adr/0009-datos-sellados.md), [0010](../adr/0010-licencia-firmada.md), [0011](../adr/0011-brazos-y-comunicacion.md), [0020](../adr/0020-servidor-en-red-local.md), [0021](../adr/0021-local-primero-y-salidas-autorizadas.md), [0028](../adr/0028-sqlite-como-almacen-del-servidor.md), [0029](../adr/0029-que-se-sella.md), [0030](../adr/0030-identidad-del-personal.md), [0031](../adr/0031-yaml-en-la-plataforma.md)

## Responsabilidad
Lo común a todos los brazos:
- **rutas del servidor:** dónde están los packs y dónde escribe;
- **almacén:** un archivo SQLite con su esquema versionado; ningún otro proyecto ve SQL
  ([0028](../adr/0028-sqlite-como-almacen-del-servidor.md));
- **auditoría encadenada:** cada hecho con su autor, su puesto y la huella de la línea anterior, con el formato de la
  0.1 ([0006](../adr/0006-owner-y-autoria.md));
- **identidad:** usuarios con contraseña y rol, espera tras cinco fallos, primer administrador solo desde el servidor
  ([0030](../adr/0030-identidad-del-personal.md));
- **roles:** los declara `packs/platform/roles.yaml`, con sus permisos;
- **textos del armazón:** la sección `shell` de `packs/platform/ui.yaml`
  ([0031](../adr/0031-yaml-en-la-plataforma.md));
- **sesiones:** una por puesto, con bloqueo por inactividad.

Con su primer consumidor entrarán:
- el sellado en reposo con la clave del equipo (DPAPI), con el primer brazo que guarde datos de pacientes
  ([0029](../adr/0029-que-se-sella.md));
- la licencia y el descubrimiento de brazos (`arm.yaml`);
- la instancia única y la copia de seguridad;
- los destinos de salida declarados ([ADR 0021](../adr/0021-local-primero-y-salidas-autorizadas.md));
- los puertos que implementan los adaptadores: transcripción, lectura de documentos, redacción y mensajería.

## Depende de
`Aida.Engine`. Paquetes: `Microsoft.Data.Sqlite` ([0028](../adr/0028-sqlite-como-almacen-del-servidor.md)) y
`YamlDotNet` ([0031](../adr/0031-yaml-en-la-plataforma.md)).

## Expone
| Espacio de nombres | Tipos públicos | Para qué |
|---|---|---|
| `Aida.Platform.Paths` | `ServerPaths` | La carpeta de los packs y la de datos, inyectables |
| `Aida.Platform.Storage` | `PlatformDatabase` | Abre el archivo y deja el esquema al día. La conexión es interna |
| `Aida.Platform.Audit` | `AuditLog`, `AuditKind`, `Actor` | Registrar un hecho, leer las líneas y verificar la cadena |
| `Aida.Platform.Identity` | `UserStore`, `UserAccount`, `PasswordHasher`, `IdentityError`, `IdentityException` | Entrar, crear, desactivar y restablecer |
| `Aida.Platform.Roles` | `RoleCatalog`, `RoleSpec`, `PlatformPermission` | Qué roles hay y qué permite cada uno |
| `Aida.Platform.Wording` | `ShellWording` | Los textos del armazón y el tiempo sin uso |
| `Aida.Platform.Sessions` | `SessionTracker`, `SessionView`, `SessionStatus` | Abrir, comprobar, bloquear, desbloquear y cerrar la sesión de un puesto |

## Lo que no hace
- No conoce ningún brazo ni ningún dominio: ni la factura ni la historia clínica. Los nombres de los roles viven en el
  pack.
- No implementa adaptadores nativos.
- No escribe páginas ni sabe de HTTP: eso es `Aida.Web`.
