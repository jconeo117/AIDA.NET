# Aida.Arms.Agenda

- **Estado:**
  - **N008** trajo la agenda del día:
    - la configuración de servicios, recursos y horarios;
    - el almacén de citas y contactos, con el nombre y el teléfono sellados;
    - cinco acciones: agendar, reprogramar, cancelar, atendida y no asistió;
    - los huecos libres;
    - el mostrador, que ve el día y pasa cada cambio por la puerta del motor.
  - **N009** trajo:
    - los bloqueos (bloquear y desbloquear, con su propio permiso `agenda.block`);
    - la regla `blocked` del hueco;
    - `editar`, que cambia el nombre, el teléfono, el servicio o el recurso de una cita;
    - la versión 2 del esquema, con `agenda_blocks`.
  - **N010** trae la consulta del mostrador:
    - la semana;
    - la búsqueda de contactos por nombre o teléfono;
    - la ficha, con `editar_contacto`.
  - **N012** trae el asistente: de lo entendido de una frase saca una propuesta (qué panel abrir y con qué relleno).
    No ejecuta nada. N013 le añade las fechas y las horas. **N014** resuelve la cita concreta para cancelar o
    reprogramar (por persona, día de la semana, fecha u hora), y el mostrador abre una cita por su id, sea del día que
    sea (`AgendaDesk.Appointment`), para moverla a otro día.
  - **N015** lleva a sus contactos a la persona de la plataforma (esquema 3, con migración al arrancar): la ficha
    muestra y edita el documento, la fecha de nacimiento y el sexo, y el teléfono se puede compartir.
- **ADR:** [0007](../adr/0007-autoridad-y-puerta-de-herramientas.md), [0011](../adr/0011-brazos-y-comunicacion.md), [0013](../adr/0013-python-como-oraculo.md), [0029](../adr/0029-que-se-sella.md), [0032](../adr/0032-sql-de-cada-brazo.md), [0033](../adr/0033-packs-de-los-brazos.md), [0034](../adr/0034-clave-del-equipo.md), [0035](../adr/0035-lo-que-declara-un-brazo.md), [0037](../adr/0037-asistente-sin-llm.md), [0038](../adr/0038-la-persona-en-la-plataforma.md)

## Responsabilidad
- **Las citas de la clínica:** quién viene, a qué servicio, con qué recurso y a qué hora, y en qué estado está cada
  cita. También los tramos bloqueados.
- **Las reglas de la 0.1, en el mismo orden** (`arms/agenda/tool.py`). La primera regla que falla es la que se le
  explica a quien pidió. El hueco se comprueba en este orden: fuera de horario, bloqueado y solape.
- **Ningún cambio fuera de la puerta.** Todo cambio entra por la puerta de acciones del motor y, si sale bien, se
  registra en la auditoría en la misma transacción.
- **Lo que identifica a un paciente se guarda sellado:** el nombre, el teléfono y el motivo de un bloqueo. El teléfono
  se busca por su huella HMAC ([0029](../adr/0029-que-se-sella.md)).
- **Los permisos:**
  - ver el día, buscar y ver la ficha: cualquier sesión;
  - cambiar citas y contactos: `agenda.manage`;
  - bloquear y desbloquear: `agenda.block`.

## Depende de
`Aida.Platform` (y, a través de ella, `Aida.Engine`).
- Usa la conexión de la plataforma para sus tablas `agenda_appointments` y `agenda_blocks` (desde N015, las personas son
  de la plataforma: `PersonStore`, en la misma transacción)
  ([0032](../adr/0032-sql-de-cada-brazo.md)).
- Lee su pack con `PlatformYaml` ([0033](../adr/0033-packs-de-los-brazos.md)).
- Pliega el texto de la búsqueda con `Aida.Engine.Text.TextFold`.

No referencia ningún paquete directamente.

## Expone
| Espacio de nombres | Tipos públicos | Para qué |
|---|---|---|
| `Aida.Arms.Agenda` | `AgendaDeclarations` | El identificador del brazo, sus permisos (`agenda.manage` y `agenda.block`) su clase de hecho (`agenda_changed`) y la versión de su esquema (`SchemaVersion`, que lee la restauración), que la composición pasa a la plataforma |
| `Aida.Arms.Agenda.Setup` | `AgendaManifest`, `AgendaSetup`, `AgendaSetupLoader`, `ServiceSpec`, `ResourceSpec`, `TimeRange` | `arm.yaml` (sector por defecto y acciones) y la configuración de la clínica, copiada de la plantilla del sector al primer arranque |
| `Aida.Arms.Agenda.Storage` | `Appointment`, `AppointmentState`, `Client`, `Block` | Lo que se guarda, ya abierto. `Block` lleva las dos reglas que comparten el hueco, la rejilla y la semana |
| `Aida.Arms.Agenda.Desk` | `AgendaDesk`, `DayView`, `AppointmentView`, `BlockView`, `FreeSlot`, `WeekDay`, `ClientCard` | Ver el día y la semana, buscar, ver la ficha, abrir una cita por su id y pedir un cambio. Es lo único que usa la web |
| `Aida.Arms.Agenda.Wording` | `AgendaWording` | Los textos de `packs/agenda/ui.yaml` |
| `Aida.Arms.Agenda.Assistant` | `AgendaAssistant`, `AgendaProposal`, `ProposalKind` | De lo entendido a una propuesta para la pantalla. Solo lee (el día, la búsqueda y la ficha); si encaja más de una persona, las lista ([0037](../adr/0037-asistente-sin-llm.md)). Sus intenciones están en `AgendaDeclarations.Intents` y sus ejemplos en `packs/agenda/intents.yaml` |

Son internos (visibles para `Aida.Arms.Agenda.Tests`):
- `AgendaStore` y `AgendaSchema`: el SQL, en dos versiones;
- `AgendaWork`: una conexión y una transacción por petición;
- `AgendaTool`: implementa `IToolPort` y despacha las nueve acciones;
- `ActionArguments`, `EditAction` y `BlockActions`: las reglas de cada acción;
- `SlotRules` y `FreeSlots`: las reglas del hueco;
- `WeekSummary` y `ClientSearch`: la semana y la búsqueda;
- `AppointmentViews`: una cita como la pinta la pantalla;
- `StartArgument`: la forma aceptada de una fecha;
- `PhoneNumbers`.

## Lo que no hace
- No referencia `Aida.Arms.Health` ni ningún otro brazo.
- No escribe páginas ni sabe de HTTP: la pantalla vive en `Aida.Web`.
- No decide su techo de autoridad: se lo da la composición ([0035](../adr/0035-lo-que-declara-un-brazo.md)).
- No toca las tablas de la plataforma ni las de otro brazo.
- No escribe `owner` en la auditoría: entra con su primer lector, en M4.
