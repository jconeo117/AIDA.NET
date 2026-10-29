# 0010 — Licencia firmada con Ed25519 y comprobada sin conexión

- **Estado:** Aceptado
- **Fecha:** 2026-10-28
- **Relacionados:** [0008](0008-local-primero.md), [0011](0011-brazos-y-comunicacion.md), [0017](0017-cadena-de-suministro.md)

## Contexto
AIDA activa brazos según lo contratado. Tiene que funcionar sin conexión ([0008](0008-local-primero.md)),
así que la licencia no puede depender de un servidor.

## Decisión
- **Una licencia es un documento** con el cliente, los brazos, la fecha de emisión y la de
  caducidad, **firmado con Ed25519**. AIDA lleva dentro solo la clave pública.
- **Estados**:
  - vigente;
  - por caducar (15 días antes, con aviso);
  - caducada (**solo lectura**: se consulta lo hecho, no se crea nada nuevo);
  - ausente;
  - inválida.
- **La clave privada nunca está en el repositorio.** La herramienta que firma se niega a escribirla
  dentro de él, y no viaja en el instalador.
- **No hay modo desarrollo que salte la firma.** Los tests firman con una pareja propia.

## Consecuencias
- Revocar una licencia antes de su caducidad no es posible sin conexión. Se asume, y se acota con
  caducidades cortas.
- La versión de Python usó Ed25519 con PyNaCl/cryptography. En .NET, la biblioteca que firme y
  verifique se elige con su ADR en la unidad de licencias ([0017](0017-cadena-de-suministro.md)).

## Cómo se hace cumplir
- Tests de cada estado, de una firma alterada y de un payload alterado.
- Test de arquitectura: la clave pública es una constante de `Aida.Platform`, y no hay ninguna rama
  que acepte una licencia sin firma.
