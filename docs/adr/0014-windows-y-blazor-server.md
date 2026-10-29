# 0014 — Solo Windows; .NET 10 y Blazor Server sobre web local

- **Estado:** Aceptado
- **Fecha:** 2026-10-28
- **Relacionados:** [0004](0004-reloj-hibrido.md), [0009](0009-datos-sellados.md)

## Contexto
- El público objetivo (profesionales de la salud, pequeños comercios) usa Windows. El peso de Linux
  es nulo.
- En Python, el empaquetado con PyInstaller y las diferencias de plataforma costaron una remediación
  entera.
- La versión de Python servía la interfaz con un servidor HTTP escrito a mano, con HTML y JavaScript
  dentro del código.

## Decisión
- **Solo Windows** (10 y 11, x64). Un puerto a otro sistema sería una decisión futura con su ADR.
- **.NET 10** (LTS, con soporte hasta noviembre de 2028), publicado autocontenido.
- **La interfaz es Blazor Server sobre ASP.NET Core**, servida en local. Se abre en el navegador del
  equipo.
  - Sigue siendo web, así que otro dispositivo de la red (un teléfono en un mostrador) podrá entrar
    cuando haya una unidad que lo decida, con su seguridad.
  - El JavaScript se limita a lo que el navegador solo hace en JavaScript (el micrófono), aislado en
    un módulo propio.

## Consecuencias
- Se ganan DPAPI, el servicio de Windows, la firma de código y un instalador nativo.
- Blazor Server mantiene una conexión por pestaña. Para un equipo local es irrelevante.
- Ningún usuario de Linux.

## Cómo se hace cumplir
- El CI corre en Windows y es el único CI.
- Test de arquitectura: los archivos `.js` solo existen en la carpeta de módulos de interoperabilidad
  de `Aida.Web`.
