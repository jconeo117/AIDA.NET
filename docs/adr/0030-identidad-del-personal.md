# 0030 — Identidad del personal: usuario y contraseña, PBKDF2, espera tras cinco fallos y primer administrador solo desde el servidor

- **Estado:** Aceptado
- **Fecha:** 2026-11-15
- **Relacionados:** [0006](0006-owner-y-autoria.md), [0020](0020-servidor-en-red-local.md), [0028](0028-sqlite-como-almacen-del-servidor.md), [0029](0029-que-se-sella.md)

## Contexto
- En la 0.1 se entraba eligiendo un perfil en una lista y escribiendo un PIN de 4 a 8 dígitos
  (`platform/identity.py`), en un único equipo. Lo protegían una huella PBKDF2 de 200 000 iteraciones y una espera de
  30 s tras cinco fallos.
- Ahora el servidor se alcanza desde toda la red local ([0020](0020-servidor-en-red-local.md)). Un PIN de 4 dígitos
  son 10 000 combinaciones; a cinco intentos cada 30 s, cae en unas 17 horas. La lista de perfiles, además, enseña a
  quien llegue quién trabaja en la clínica.
- Alguien tiene que crear el primer usuario, y en ese momento todavía no hay nadie que pueda autorizarlo.

## Decisión
- **Se entra con usuario y contraseña.**
  - El usuario se guarda en minúsculas, de 3 a 32 caracteres: letras ASCII, cifras, punto, guion y subrayado.
  - La contraseña tiene de 10 a 128 caracteres, sin reglas de composición.
- **La contraseña no se guarda.** Se guarda `pbkdf2-sha256$iteraciones$sal$huella`: PBKDF2-SHA256 de la biblioteca
  base, sal aleatoria de 16 bytes, huella de 32 bytes y **600 000 iteraciones**. Medido el 2026-11-15, son 111 ms por
  intento. Las iteraciones van dentro de la huella, para poder subirlas sin invalidar las anteriores. Se compara en
  tiempo constante.
- **Cinco fallos seguidos obligan a esperar 30 s**, como en la 0.1. Se cuenta por usuario escrito, exista o no, en
  memoria.
- **Un fallo no dice qué falló.** Un usuario que no existe, uno desactivado y una contraseña mala dan el mismo
  mensaje, y el usuario inexistente también calcula una huella, para que el tiempo no lo delate.
- **El primer administrador solo se crea desde el propio servidor**, con la petición llegando por loopback, y solo
  si no hay ningún usuario. Firma su propia creación en la auditoría.
- **No hay códigos de recuperación.** La contraseña olvidada la restablece un usuario con permiso para gestionar
  usuarios, y queda en la auditoría con su nombre.
- **Los roles y lo que permite cada uno los declara el pack** (`packs/platform/roles.yaml`), como pide el
  [0020](0020-servidor-en-red-local.md). El permiso se comprueba en el almacén y no solo en la página. Un usuario
  cuyo rol ya no esté declarado no tiene ningún permiso.
- **No se puede desactivar al último usuario activo que gestiona usuarios.** Un usuario desactivado no entra, y sus
  hechos siguen a su nombre.
- **Desbloquear el puesto pide la misma contraseña**, de la misma persona, con la misma espera.

## Consecuencias
- Cada persona recuerda una contraseña larga. El bloqueo por inactividad la pide de nuevo.
- Quien llegue desde la red no puede quedarse con un servidor recién instalado.
- Perder la contraseña del único administrador obliga a reinstalar, hasta que entre la copia de seguridad.
- Cinco fallos con el usuario de otra persona le hacen esperar 30 s: es el precio de frenar a quien prueba.

## Cómo se hace cumplir
- Tests de `UserStore`:
  - la espera llega exactamente en el quinto fallo y termina a los 30 s;
  - un usuario inexistente y uno desactivado dan el mismo error que una contraseña mala;
  - un rol sin permiso no crea, no desactiva y no restablece;
  - no se desactiva al último administrador.
- Test del armazón: sin usuarios, todo lleva a crear el primero, y solo una petición desde loopback lo crea.
- Test de `PasswordHasher`: la huella lleva sus iteraciones, la misma contraseña con otra sal da otra huella y una
  huella ilegible no coincide nunca.
