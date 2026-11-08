# 0020 — Solo Windows; .NET 10 y Blazor Server, servido desde un equipo a los puestos de la red local

- **Estado:** Aceptado
- **Fecha:** 2026-11-07
- **Sustituye a:** [0014](0014-windows-y-blazor-server.md)
- **Relacionados:** [0004](0004-reloj-hibrido.md), [0006](0006-owner-y-autoria.md), [0007](0007-autoridad-y-puerta-de-herramientas.md), [0009](0009-datos-sellados.md), [0021](0021-local-primero-y-salidas-autorizadas.md)

## Contexto
- El ADR 0014 suponía un solo equipo: AIDA servida en local y abierta en el navegador de esa máquina.
- El primer cliente de la suite es una clínica que trabaja desde varios puestos a la vez: recepción,
  consultorios y caja. Hoy usa un programa que lo hace todo.
- El público sigue usando Windows, y lo que el 0014 decidió por eso sigue valiendo: DPAPI, servicio de
  Windows, instalador nativo y un solo CI.

## Decisión
- **Solo Windows** (10 y 11, x64), con **.NET 10** (LTS) publicado autocontenido. Igual que el 0014.
- **Un equipo de la clínica es el servidor.**
  - Corre AIDA como servicio de Windows.
  - Guarda todos los datos y la clave del equipo ([0009](0009-datos-sellados.md)).
  - Sirve la interfaz, en **Blazor Server** sobre ASP.NET Core.
- **Los puestos solo tienen un navegador.** No guardan datos de AIDA ni la clave. Si se pierde un puesto,
  no se pierde nada; si se pierde el servidor, se recupera de la copia de seguridad.
- **La interfaz se sirve en la red local con HTTPS**, nunca en HTTP en claro. Cómo se emite y se instala
  el certificado en los puestos lo decide la unidad del armazón.
- **Cada sesión es de una persona identificada.**
  - Hay usuarios con rol: recepción, profesional, caja y administración. Las acciones que permite cada
    rol las declara el pack.
  - El bloqueo es por puesto y la sesión caduca.
  - Todo hecho lleva su `Actor` ([0006](0006-owner-y-autoria.md)), y la autoridad para lo irreversible
    sigue siendo una persona identificada ([0007](0007-autoridad-y-puerta-de-herramientas.md)).
- **No se expone a internet.** Solo escucha en la red local. Si alguna vez entra un acceso remoto, será
  con su propio ADR.
- **El JavaScript, solo donde el navegador no ofrece otra cosa** (el micrófono), aislado en sus módulos
  de interoperabilidad, como en el 0014.

## Consecuencias
- El ADR 0009 no cambia: el sellado y la clave viven en un solo sitio, el servidor.
- El servicio corre con una cuenta de Windows propia, y la clave queda ligada a ella. La copia de
  seguridad la exporta cifrada con una frase.
- Blazor Server mantiene una conexión por pestaña. En una clínica con pocos puestos es irrelevante, pero
  la caída de la red corta las sesiones abiertas.
- Hay que distribuir un certificado a los puestos. El instalador lo resuelve.
- La identidad pasa de un único usuario con PIN a varios usuarios con rol.

## Cómo se hace cumplir
- El CI corre en Windows y es el único CI.
- Test de arquitectura: los archivos `.js` solo existen en la carpeta de módulos de interoperabilidad de
  `Aida.Web`.
- Tests del armazón:
  - sin sesión, ninguna página de datos responde;
  - una acción fuera del rol del usuario no se ejecuta;
  - todo hecho registrado lleva el `Actor` de la sesión.
- Test de configuración: el servidor no acepta HTTP en claro.
