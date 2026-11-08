# 0008 — Local primero; la nube con autorización aparte; lo clínico, siempre local

- **Estado:** Sustituido por [0021](0021-local-primero-y-salidas-autorizadas.md)
- **Fecha:** 2026-10-28
- **Relacionados:** [0009](0009-datos-sellados.md), [0010](0010-licencia-firmada.md)

## Contexto
AIDA trata datos de personas, y en salud datos clínicos (Ley 1581 en Colombia). Mandar algo a un
proveedor en la nube es una transferencia de datos que la persona tiene que autorizar, y cuyo país
importa.

## Decisión
- **Todo funciona en el equipo, sin conexión**: decidir, guardar y revisar.
- **La nube es opcional, y se autoriza aparte**:
  - su casilla es distinta de la autorización de grabar;
  - el despliegue puede declararse «solo en este equipo»;
  - el país de cada proveedor de nube se comprueba contra lo que el pack declara permitido.
- **Sin nube autorizada, nada sale del equipo.**
- **Lo clínico nunca sale del equipo para guardarse.** Un futuro plan en la nube (agente 24/7) lleva
  solo lo no clínico.

## Consecuencias
- El modelo local tiene que bastar cuando no hay nube, aunque sea peor y más lento.
- Cada proveedor nuevo exige declarar su país y entra en el pliego legal.

## Cómo se hace cumplir
- La puerta de transcripción y de redacción consulta la autorización de nube antes de cada llamada.
  Hay tests con la casilla apagada y con «solo en este equipo».
- La batería no sale a la red: el transporte HTTP real está cortado en los tests.
