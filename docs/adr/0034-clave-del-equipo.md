# 0034 — La clave del equipo: DPAPI en Windows, archivo sin proteger solo en desarrollo, y claves derivadas con HKDF

- **Estado:** Aceptado
- **Fecha:** 2026-11-15
- **Relacionados:** precisa el [0009](0009-datos-sellados.md); [0017](0017-cadena-de-suministro.md), [0020](0020-servidor-en-red-local.md), [0029](0029-que-se-sella.md)

## Contexto
- El [0009](0009-datos-sellados.md) protege la clave del equipo con DPAPI. El [0029](0029-que-se-sella.md) la hace
  entrar con el primer brazo que guarde datos de pacientes, que es la agenda (unidad N008).
- **DPAPI solo existe en Windows**, y en .NET no viene en la biblioteca base: está en el paquete
  `System.Security.Cryptography.ProtectedData`.
- **Se desarrolla en Linux**, y la aplicación tiene que poder probarse allí a mano.
- **La 0.1 usaba dos claves:** la que sellaba y otra, derivada de ella, para la huella del teléfono
  (`store.py:150`, con `blake2b(key, person=…)`). Quien tiene una no puede usar la otra.

## Decisión
- **La clave del equipo son 32 bytes de `RandomNumberGenerator`**, guardados en `<datos>/keys/machine.key`.
  - Se crean la primera vez que se piden y nunca se regeneran: si el archivo existe y no se puede abrir, el arranque
    falla.
  - El archivo nunca guarda la clave en claro, salvo con el protector de desarrollo.
- **La protege un `IKeyProtector`** (`Aida.Platform.Keys`), con dos implementaciones:
  - **`DpapiKeyProtector`**: DPAPI con ámbito de usuario (`DataProtectionScope.CurrentUser`), que en el servidor es la
    cuenta del servicio. Solo en Windows.
  - **`DevelopmentKeyProtector`**: no protege nada. Solo lo elige la composición fuera de Windows **y** en el entorno
    *Development*, y deja un aviso en el registro de arranque. El archivo se crea con permisos 0600.
  - **Fuera de Windows y fuera de *Development*, no hay protector:** el primer uso de la clave falla con un mensaje que
    lo dice. Un servidor de producción en Linux no guarda datos de pacientes con la clave en claro.
- **Las claves que salen de ella se derivan con HKDF-SHA256** (`System.Security.Cryptography.HKDF`, de la biblioteca
  base), con un propósito en ASCII: `MachineKey.Derive(purpose)`.
  - El sellado usa el propósito `aida-seal`.
  - Cada huella usa el suyo: la del teléfono de la agenda es `aida-agenda-phone`.
- **Paquete nuevo:** `System.Security.Cryptography.ProtectedData` **10.0.15**, solo en `Aida.Platform`.
  - **Por qué no basta la base:** .NET no trae DPAPI.
  - **Mantenedor y licencia:** Microsoft (.NET Foundation), MIT.
  - **Dependencias:** ninguna en `net10.0`.
  - **Alternativa descartada:** la protección de datos de ASP.NET Core, con su anillo de claves. Es un marco entero
    (rotación, almacén de claves) para guardar una sola clave, y obliga a la plataforma a depender de ASP.NET.
- **Sigue sin migración desde la 0.1** ([0009](0009-datos-sellados.md)).

## Consecuencias
- En Windows, copiar `aida.db` y `machine.key` a otro equipo o a otro usuario no basta para leer los datos.
- En desarrollo en Linux, la clave está en claro en la carpeta de datos de prueba. Es aceptable porque ahí no hay datos
  reales.
- Perder `machine.key` es perder los datos sellados. La copia de la clave cifrada con una frase entra con la copia de
  seguridad.
- La huella del teléfono no se puede recalcular sin la clave del equipo, y la clave de la huella no abre los datos
  sellados.

## Cómo se hace cumplir
- Test de arquitectura: `ProtectedData` y `AesGcm` solo aparecen en `src/Aida.Platform/` ([0009](0009-datos-sellados.md)).
- Tests:
  - la clave se crea una vez y se relee igual;
  - un archivo corrupto falla;
  - el protector de desarrollo solo lo elige la composición en *Development*;
  - en producción fuera de Windows, el primer uso de la clave falla;
  - dos propósitos distintos dan claves distintas.
- `dotnet list package --vulnerable --include-transitive` bloqueante en el CI ([0017](0017-cadena-de-suministro.md)).
