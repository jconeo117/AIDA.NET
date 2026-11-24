# 0036 — La copia de seguridad: la base, la configuración y la clave del equipo, cifradas con una frase, y restaurar solo en un equipo vacío al arrancar

- **Estado:** Aceptado
- **Fecha:** 2026-11-23
- **Relacionados:** precisa el [0009](0009-datos-sellados.md), el [0030](0030-identidad-del-personal.md) y el [0034](0034-clave-del-equipo.md); [0006](0006-owner-y-autoria.md), [0017](0017-cadena-de-suministro.md), [0020](0020-servidor-en-red-local.md), [0028](0028-sqlite-como-almacen-del-servidor.md)

## Contexto
- Desde la agenda (N008), `aida.db` guarda datos de personas con los campos sensibles sellados con la clave del equipo
  ([0029](0029-que-se-sella.md)). La clave vive en `keys/machine.key`, envuelta con DPAPI y atada a la cuenta del
  servicio ([0034](0034-clave-del-equipo.md)).
- **Copiar los archivos no basta.** En otro equipo, o tras reinstalar Windows, la clave envuelta no se abre, y lo
  sellado se pierde. El 0034 y el 0030 dejaron la salida para esta copia.
- **La 0.1** (`platform/snapshot.py`):
  - copiaba solo la carpeta de configuración, cifrada con una frase (scrypt y Fernet, tope de 5 MB);
  - al restaurar, escribía en su sitio, apartaba lo anterior y **se cerraba**;
  - el primero que entraba después firmaba la restauración.
- **Lo que se mantiene de la 0.1 no se puede hacer igual.** Mientras AIDA corre, la base está abierta (en Windows no se
  renombra un archivo abierto), y la clave y sus derivadas ya están en memoria. Además, la biblioteca base de .NET no
  trae scrypt.

## Decisión
- **Qué entra.** Un solo archivo `.aidacopia`, que es un tar (Ustar) sin comprimir con una lista cerrada de entradas:
  - `manifest.json`, siempre primero;
  - `aida.db`, una copia coherente hecha con `VACUUM INTO` de SQLite. Lee en una sola transacción, recoge lo que aún
    está en el WAL y sale compacta y en modo de diario normal; al abrirla, `PlatformDatabase` vuelve a poner el WAL.
    No se usa `BackupDatabase` porque el guardián A2 veta su nombre en `Aida.Platform`, y esquivar el guardián con
    reflexión no se acepta (N011-fix);
  - `machine.key`, la clave del equipo **en bruto**, 32 bytes;
  - `config/<nombre>`, los archivos de la configuración de la instalación.

  Cualquier otra entrada, o una entrada repetida, rechaza la copia.
- **El cifrado**, todo de la biblioteca base:
  - la clave sale de la frase con **PBKDF2-SHA256** (sal de 16 bytes y 600 000 iteraciones, como las contraseñas del
    [0030](0030-identidad-del-personal.md)). La frase tiene 12 caracteres o más, como en la 0.1;
  - el contenido va en **trozos de 1 MiB, cada uno con AES-GCM**. El nonce lleva el número del trozo, y los datos
    asociados llevan la cabecera, ese número y una marca de último trozo. Así no se puede quitar, reordenar ni cambiar
    ningún trozo sin que se note;
  - la cabecera es `AIDACOPIA2\n`, la sal y las iteraciones. Al leer, las iteraciones fuera de
    [600 000, 10 000 000] se rechazan;
  - el contenido tiene un tope de 1 GiB.
- **Sin compresión.** Lo sellado no se comprime, y así no hay bomba de descompresión. Medido el 2026-11-23 sobre una
  base de 20,6 MiB: 15 ms de copia, 112 ms de PBKDF2 y 24 ms de cifrado. Con `VACUUM INTO` (2026-11-24, SQLite
  3.53.3), una base de unos 20 MiB se copia en 19 ms.
- **Quién la hace:**
  - quien tiene el permiso de plataforma `snapshot.create`, que en el pack por defecto solo tiene administración;
  - se descarga en el navegador y nunca se guarda en el equipo;
  - el hecho queda en la auditoría como `snapshot_created` ([0006](0006-owner-y-autoria.md)).
- **Dónde se restaura:** solo en un equipo **sin usuarios**, desde la pantalla del primer administrador y por loopback.
  Es la misma condición que la del primer administrador ([0030](0030-identidad-del-personal.md)).
- **Cómo entra en vigor:**
  1. **Se valida entera** en una carpeta de paso, antes de que exista nada preparado: el descifrado de todos los
     trozos, las entradas, el manifiesto y la base (`quick_check`). Una copia de un AIDA más nuevo (el esquema de la
     plataforma o el de un brazo) se rechaza.
  2. **Se deja preparada** en `restaurar/`, con la clave **ya envuelta** con el protector de este equipo. La clave en
     bruto nunca toca el disco fuera del archivo cifrado.
  3. **AIDA se cierra.** Al arrancar, antes de abrir la base o la clave, se aplica:
     - lo que había se aparta a `antes-<fecha>/`, sin borrar nada;
     - lo preparado se mueve a su sitio;
     - queda un aviso.
     
     Si la base actual ya tiene usuarios, lo preparado se aparta sin aplicarse.
  4. **El primero que entra firma `snapshot_restored`** con los datos del aviso, como en la 0.1.
- **Sin copias automáticas** por ahora. Una copia sin nadie delante obliga a guardar la frase o a cifrar de otro modo,
  y eso tiene su propia decisión.
- **Sin paquetes nuevos** ([0017](0017-cadena-de-suministro.md)): `System.Formats.Tar`, `System.Text.Json`, `AesGcm` y
  `Rfc2898DeriveBytes` son de la biblioteca base.

## Consecuencias
- **Con el archivo y la frase se lee todo lo de AIDA.** Lo que protege son la frase y dónde se guarda el archivo, y por
  eso hay un permiso propio y una línea de auditoría.
- **Perder el equipo deja de ser perder los datos,** si hay una copia reciente. Lo que se hizo después de la copia se
  pierde.
- **Un archivo de la 0.1 no se puede restaurar:** tiene otra magia. Es coherente con el [0009](0009-datos-sellados.md),
  que no migra desde la 0.1.
- **Restaurar obliga a volver a arrancar AIDA.** Como servicio de Windows, el reinicio automático lo configura el
  instalador (M7). Mientras tanto, se arranca a mano.
- **No se restaura sobre un equipo con datos.** Volver atrás en un equipo en marcha necesitaría su propia decisión.
- **Un brazo declara la versión de su esquema** a la composición ([0035](0035-lo-que-declara-un-brazo.md)), para que la
  restauración no acepte una copia más nueva.

## Cómo se hace cumplir
- Tests de arquitectura:
  - `AesGcm` y `ProtectedData` solo en `src/Aida.Platform/` ([0009](0009-datos-sellados.md),
    [0034](0034-clave-del-equipo.md));
  - fuera de `src/Aida.Guards/` no se buscan ni se invocan miembros por reflexión, para que nadie esquive un guardián
    llamando a un método por su nombre (N011-fix).
- Tests:
  - la copia de la base recoge lo que aún está en el WAL;
  - la ida y vuelta entre dos equipos, con otro protector, abre lo sellado;
  - un trozo cambiado, quitado, reordenado o de más se rechaza, igual que una frase distinta;
  - cualquier entrada fuera de la lista se rechaza, y tras un fallo no queda nada preparado;
  - una copia más nueva se rechaza;
  - con usuarios, ni se prepara ni se aplica;
  - la clave preparada va envuelta y no contiene los 32 bytes en claro;
  - el aviso lo firma una sola vez el primero que entra.
- Mutaciones críticas en el contrato de N011: el trozo último, la lista de entradas y la condición de equipo vacío.
