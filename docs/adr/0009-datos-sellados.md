# 0009 — Datos sellados en reposo con AES-GCM y la clave del equipo protegida con DPAPI

- **Estado:** Aceptado
- **Fecha:** 2026-10-28
- **Relacionados:** [0008](0008-local-primero.md), [0014](0014-windows-y-blazor-server.md), [0017](0017-cadena-de-suministro.md)

## Contexto
- En la versión de Python, lo guardado iba cifrado con Fernet, y la clave del equipo era un archivo
  con permisos `0600`, que en Windows no protegen nada (la carpeta del perfil sí).
- Fernet no está en la biblioteca base de .NET.
- No hay usuarios reales todavía, así que mantener la compatibilidad no tiene a quién servir.

## Decisión
- **Todo lo guardado que identifique a una persona va sellado** con `AesGcm` de la biblioteca base:
  nonce aleatorio por mensaje y etiqueta de autenticación.
- **La clave del equipo se protege con DPAPI**, ligada al usuario de Windows. Nunca se guarda en
  claro.
- **El texto en claro solo existe dentro de un ámbito explícito** (un `IDisposable`), en una carpeta
  temporal propia que se borra al salir del ámbito y al arrancar.
- **Los plazos de retención los declara el pack.** El borrado firma su línea en la auditoría.
- **Se rompe la compatibilidad con los datos de la 0.1**: las instalaciones de prueba se reinstalan.

## Consecuencias
- Copiar los datos a otro usuario o a otro equipo no basta para leerlos. La copia de seguridad
  exporta la clave cifrada con una frase.
- Sin migrador de la 0.1.

## Cómo se hace cumplir
- Tests de sellado: lo escrito en disco no contiene el texto en claro, y otra clave no lo abre.
- Test de arquitectura: solo `Aida.Platform` usa `AesGcm` y `ProtectedData`.
