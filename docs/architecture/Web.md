# Aida.Web

- **Estado:** N007 trajo el armazón mínimo: el servidor solo por HTTPS, la entrada con usuario y contraseña, el
  primer administrador, el bloqueo por puesto y la página de usuarios. N008 trae la pantalla del día de la agenda y
  compone el primer brazo.
- **ADR:** [0011](../adr/0011-brazos-y-comunicacion.md), [0015](../adr/0015-forma-del-codigo.md), [0020](../adr/0020-servidor-en-red-local.md), [0030](../adr/0030-identidad-del-personal.md), [0034](../adr/0034-clave-del-equipo.md), [0035](../adr/0035-lo-que-declara-un-brazo.md)

## Responsabilidad
La aplicación de la clínica, en Blazor, servida desde el equipo servidor a los puestos de la red local por HTTPS ([ADR 0020](../adr/0020-servidor-en-red-local.md)). Tiene dos partes:
- **el armazón común:**
  - entrada por usuario y contraseña, con cookie de sesión;
  - primer administrador, solo desde el propio servidor;
  - bloqueo por puesto tras el tiempo sin uso de `ui.yaml`;
  - página de usuarios para quien tiene el permiso;
  - **pantalla de la agenda** (`/agenda`): la rejilla del día y un panel lateral que elige la URL (la cita, la nueva
    cita con sus huecos libres o reprogramar). Cada cambio es un formulario que llega al servicio del brazo
    (`AgendaDesk`) y pasa por la puerta del motor. Vive aquí, y no en el brazo, porque necesita la sesión;
  - después, licencia y ajustes;
- **la raíz de composición** (`AidaWebApp`): registra los servicios, la autenticación y la tubería, y la usan
  `Program.cs` y los tests. Elige el protector de la clave del equipo según el sistema y el entorno, fija el techo de
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
- Las rutas: `/entrar`, `/primer-usuario`, `/bloqueado`, `/usuarios`, `/agenda`, `/` y `POST /salir`.

## Lo que no hace
- No contiene lógica de dominio: la de cada brazo vive en su proyecto.
- No decide permisos ni guarda nada: pregunta a los almacenes de `Aida.Platform`.
- No guarda nada en los puestos: los datos solo viven en el servidor, y la cookie solo dice qué sesión es.
- No escucha en HTTP ni fuera de la red local.
