# 0031 — YamlDotNet también en la plataforma, para los archivos de packs/platform

- **Estado:** Aceptado
- **Fecha:** 2026-11-15
- **Relacionados:** amplía el [0024](0024-yaml-con-yamldotnet.md); [0002](0002-agnosticismo-de-dominio.md), [0012](0012-packs-yaml.md), [0017](0017-cadena-de-suministro.md), [0020](0020-servidor-en-red-local.md)

## Contexto
- El [0024](0024-yaml-con-yamldotnet.md) permite YamlDotNet solo en `Aida.Engine`, para el cargador de packs, y
  descartó el cargador en la plataforma para no partir la validación de un pack entre dos proyectos.
- La plataforma lee otros archivos: los textos del armazón (`packs/platform/ui.yaml`, que en la 0.1 leía
  `platform/web/wording.py`) y los roles del personal (`packs/platform/roles.yaml`). No son el pack del mundo: el
  motor no los valida ni los usa.
- Los nombres de los roles son vocabulario del cliente, y el guardián no los deja en el código de la plataforma
  ([0002](0002-agnosticismo-de-dominio.md)): tienen que vivir en un pack.

## Decisión
- **`Aida.Platform` puede usar YamlDotNet 18.1.0**, con las mismas condiciones que el motor: solo su modelo de nodos
  (`YamlStream`, `YamlMappingNode`, `YamlSequenceNode`, `YamlScalarNode`) y nunca su deserialización a objetos.
  - **Quién lo usa:** `Aida.Platform.Packs.PlatformYaml`, que lee un archivo de `packs/platform/` a su mapa raíz, y
    los dos lectores que lo llaman: `ShellWording` y `RoleCatalog`.
  - **Solo para `packs/platform/`.** El pack del mundo lo sigue leyendo el motor, entero.
- **Los archivos de la plataforma solo tienen mapas, listas, textos y números.** Un archivo que no se pueda leer, o
  al que le falte algo que una página pinta o un almacén comprueba, hace fallar el arranque con su ruta.

## Consecuencias
- Hay dos lectores de YAML, uno por proyecto, y cada uno valida lo suyo. El pack del mundo no se parte.
- La plataforma no hereda las reglas de PyYAML del motor: sus archivos no tienen escalares ambiguos que convertir.

## Cómo se hace cumplir
- El validator rechaza un uso de YamlDotNet fuera de `Aida.Engine.Packs` y `Aida.Platform.Packs`, y cualquier uso de
  su deserialización a objetos.
- Tests de `ShellWording` y `RoleCatalog` con archivos válidos y rotos.
