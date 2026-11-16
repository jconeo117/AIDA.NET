# Aida.Arms.Agenda

- **Estado:** N008 trae la agenda del día:
  - la configuración de servicios, recursos y horarios;
  - el almacén de citas y pacientes, con el nombre y el teléfono sellados;
  - las reglas de cinco acciones (agendar, reprogramar, cancelar, atendida y no asistió);
  - los huecos libres;
  - el servicio que ve el día y pasa cada cambio por la puerta del motor.

  Los bloqueos, la semana, la búsqueda, la ficha y la edición llegan en N009.
- **ADR:** [0007](../adr/0007-autoridad-y-puerta-de-herramientas.md), [0011](../adr/0011-brazos-y-comunicacion.md), [0029](../adr/0029-que-se-sella.md), [0032](../adr/0032-sql-de-cada-brazo.md), [0033](../adr/0033-packs-de-los-brazos.md), [0034](../adr/0034-clave-del-equipo.md), [0035](../adr/0035-lo-que-declara-un-brazo.md)

## Responsabilidad
- **Las citas de la clínica:** quién viene, a qué servicio, con qué recurso y a qué hora, y en qué estado está cada cita.
- **Las reglas de la 0.1, en el mismo orden** (`arms/agenda/tool.py`). La primera regla que falla es la que se le explica
  a quien pidió.
- **Ningún cambio fuera de la puerta.** Todo cambio entra por la puerta de acciones del motor, y si sale bien se
  registra en la auditoría en la misma transacción.
- **Lo que identifica a un paciente se guarda sellado**, el nombre y el teléfono, y el teléfono se busca por su huella
  HMAC ([0029](../adr/0029-que-se-sella.md)).

## Depende de
`Aida.Platform` (y, a través de ella, `Aida.Engine`). Usa la conexión de la plataforma para sus tablas
`agenda_clients` y `agenda_appointments` ([0032](../adr/0032-sql-de-cada-brazo.md)), y lee su pack con `PlatformYaml`
([0033](../adr/0033-packs-de-los-brazos.md)). No referencia ningún paquete directamente.

## Expone
| Espacio de nombres | Tipos públicos | Para qué |
|---|---|---|
| `Aida.Arms.Agenda` | `AgendaDeclarations` | El identificador del brazo, su permiso (`agenda.manage`) y su clase de hecho (`agenda_changed`), que la composición pasa a la plataforma |
| `Aida.Arms.Agenda.Setup` | `AgendaManifest`, `AgendaSetup`, `AgendaSetupLoader`, `ServiceSpec`, `ResourceSpec`, `TimeRange` | `arm.yaml` (sector por defecto y acciones) y la configuración de la clínica, copiada de la plantilla del sector al primer arranque |
| `Aida.Arms.Agenda.Storage` | `Appointment`, `AppointmentState`, `Client` | Lo que se guarda, ya abierto |
| `Aida.Arms.Agenda.Desk` | `AgendaDesk`, `DayView`, `AppointmentView`, `FreeSlot` | Ver el día y pedir un cambio. Es lo único que usa la web |
| `Aida.Arms.Agenda.Wording` | `AgendaWording` | Los textos de `packs/agenda/ui.yaml` |

Son internos (visibles para `Aida.Arms.Agenda.Tests`):
- `AgendaStore` y `AgendaSchema`: el SQL;
- `AgendaWork`: una conexión y una transacción por petición;
- `AgendaTool`: implementa `IToolPort`;
- `SlotRules` y `FreeSlots`: las reglas del hueco;
- `StartArgument`: la forma aceptada de una fecha;
- `PhoneNumbers`.

## Lo que no hace
- No referencia `Aida.Arms.Health` ni ningún otro brazo.
- No escribe páginas ni sabe de HTTP: la pantalla vive en `Aida.Web`.
- No decide su techo de autoridad: se lo da la composición ([0035](../adr/0035-lo-que-declara-un-brazo.md)).
- No toca las tablas de la plataforma ni las de otro brazo.
