# Aida.Web

- **Estado:** N007 trajo el armazón mínimo: el servidor solo por HTTPS, la entrada con usuario y contraseña, el
  primer administrador, el bloqueo por puesto y la página de usuarios. N008 trae la pantalla del día de la agenda y
  compone el primer brazo. N009 y N010 completan la agenda (bloqueos, edición, semana, búsqueda y ficha). N011 trae
  Ajustes con la copia de seguridad, y restaurar desde la pantalla del primer administrador. N012 trae la caja del
  asistente en la agenda. N016 trae la historia clínica (`/historia/{persona}` y `/historia/atencion/{id}`).
- **ADR:** [0011](../adr/0011-brazos-y-comunicacion.md), [0015](../adr/0015-forma-del-codigo.md), [0020](../adr/0020-servidor-en-red-local.md), [0030](../adr/0030-identidad-del-personal.md), [0034](../adr/0034-clave-del-equipo.md), [0035](../adr/0035-lo-que-declara-un-brazo.md), [0036](../adr/0036-copia-de-seguridad.md), [0037](../adr/0037-asistente-sin-llm.md), [0039](../adr/0039-autorizar-lo-irreversible.md), [0040](../adr/0040-el-documento-firmado.md)

## Responsabilidad
La aplicación de la clínica, en Blazor, servida desde el equipo servidor a los puestos de la red local por HTTPS ([ADR 0020](../adr/0020-servidor-en-red-local.md)). Tiene dos partes:
- **el armazón común:**
  - entrada por usuario y contraseña, con cookie de sesión;
  - primer administrador, solo desde el propio servidor. Ahí mismo, mientras no hay usuarios, se restaura una copia:
    se prepara, AIDA se cierra y la aplica al volver a arrancar;
  - bloqueo por puesto tras el tiempo sin uso de `ui.yaml`;
  - página de usuarios para quien tiene el permiso;
  - **pantalla de la agenda** (`/agenda`): la rejilla del día y un panel lateral que elige la URL (la cita, la nueva
    cita con sus huecos libres o reprogramar). Cada cambio es un formulario que llega al servicio del brazo
    (`AgendaDesk`) y pasa por la puerta del motor. Vive aquí, y no en el brazo, porque necesita la sesión;
  - **la caja del asistente**, en la agenda y solo para quien puede cambiarla: lo tecleado va por POST, nunca en la
    URL. La frase la entiende `Aida.Cognition` y la convierte en propuesta el brazo. La página abre el panel de siempre,
    ya relleno, y el operador pulsa el botón de siempre ([0037](../adr/0037-asistente-sin-llm.md));
  - **la historia clínica** (`/historia/{persona}`, `/historia/atencion/{id}`): las atenciones de una persona, el
    borrador y la firma repitiendo la contraseña. Se abre desde la ficha y desde la cita atendida, solo con `health.read`
    ([0040](../adr/0040-el-documento-firmado.md));
  - **Ajustes** (`/ajustes`), por ahora con una sola tarjeta: hacer la copia, que se descarga en el navegador y pide
    `snapshot.create`;
  - después, la licencia y las demás tarjetas de Ajustes;
- **la raíz de composición** (`AidaWebApp`): registra los servicios, la autenticación y la tubería, y la usan
  `Program.cs` y los tests. Antes de abrir nada, aplica la copia preparada si la hay. Elige el protector de la clave del equipo según el sistema y el entorno, fija el techo de
  la puerta en `reversible` y pasa a la plataforma los permisos y las clases de hecho de los brazos
  ([0034](../adr/0034-clave-del-equipo.md), [0035](../adr/0035-lo-que-declara-un-brazo.md)). Hoy compone la agenda;
  con la licencia, montará los brazos que incluya.

Todas las páginas de N007 y N008 son estáticas, renderizadas en el servidor, con formularios. La interactividad de Blazor
Server entra con la primera página que la necesite, junto con su forma de contar la actividad del puesto.

El JavaScript, solo en sus módulos de interoperabilidad (micrófono).

## Depende de
Todos los proyectos de `src/` salvo `Aida.Guards`.

## Expone
- `AidaWebApp.Build(args, configure)`: construye el servidor sin arrancarlo. Los tests le dan su certificado, sus
  carpetas y su reloj.
- Las rutas: `/entrar`, `/primer-usuario`, `/bloqueado`, `/usuarios`, `/agenda`, `/historia/{persona}`, `/historia/atencion/{id}`, `/ajustes`, `/`, `POST /salir`,
  `POST /ajustes/copia` y `POST /primer-usuario/restaurar`.

## Lo que no hace
- No contiene lógica de dominio: la de cada brazo vive en su proyecto.
- No decide permisos ni guarda nada: pregunta a los almacenes de `Aida.Platform`.
- No guarda nada en los puestos: los datos solo viven en el servidor, y la cookie solo dice qué sesión es.
- No escucha en HTTP ni fuera de la red local.
