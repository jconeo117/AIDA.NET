# 0033 — Los brazos leen su pack con el lector de nodos de la plataforma

- **Estado:** Aceptado
- **Fecha:** 2026-11-15
- **Relacionados:** amplía el [0031](0031-yaml-en-la-plataforma.md) y el [0024](0024-yaml-con-yamldotnet.md); [0002](0002-agnosticismo-de-dominio.md), [0012](0012-packs-yaml.md)

## Contexto
- La agenda (unidad N008) lee tres archivos de su pack:
  - `packs/agenda/arm.yaml`: el sector por defecto y sus acciones;
  - `packs/agenda/ui.yaml`: los textos;
  - la plantilla `packs/agenda/<sector>/agenda.yaml`, que se copia a la configuración del servidor.
- El [0031](0031-yaml-en-la-plataforma.md) solo permite YamlDotNet en `Aida.Platform` y solo para `packs/platform/`.
- Las alternativas eran dos:
  - que cada brazo referenciara YamlDotNet, con un `.csproj` más que vigilar y el mismo lector repetido;
  - que la plataforma leyera los archivos de los brazos, con su vocabulario dentro.

## Decisión
- **`Aida.Platform.Packs.PlatformYaml` pasa a ser público**, con sus tres operaciones:
  - `ReadRoot(path)`: el mapa raíz, o un fallo con la ruta;
  - `Child(map, key)`;
  - `Text(node)`.
- **Un brazo lo usa para leer su propia carpeta de packs** (`packs/<brazo>/`) y la configuración que se copió de ella.
  Nada más.
- **Las mismas condiciones del 0024 y del 0031:**
  - solo el modelo de nodos de YamlDotNet (`YamlMappingNode`, `YamlSequenceNode`, `YamlScalarNode`);
  - nunca su deserialización a objetos;
  - un archivo que no cuadra hace fallar el arranque con su ruta.
- **Ningún brazo referencia YamlDotNet en su `.csproj`.** Los tipos de nodo le llegan a través de `Aida.Platform`.

## Consecuencias
- Hay un solo lector de nodos fuera del motor, y el pack del mundo lo sigue leyendo el motor entero.
- `PlatformYaml` es API pública de la plataforma: cambiarlo afecta a los brazos y lleva su contrato.

## Cómo se hace cumplir
- El validator rechaza:
  - un `PackageReference` a YamlDotNet fuera de `Aida.Engine` y `Aida.Platform`;
  - cualquier uso de `Deserializer` o `DeserializerBuilder`.
- Test de arquitectura: ningún `.csproj` de `src/Aida.Arms.*` nombra YamlDotNet.
- Tests de cada lector de un brazo, con archivos válidos y rotos.
