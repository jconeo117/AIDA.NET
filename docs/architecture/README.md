# Arquitectura

Una página por proyecto de la solución. Cada página dice:
- qué responsabilidad tiene el proyecto;
- de qué puede depender;
- qué expone a los demás;
- qué ADR lo gobiernan.

**Cambia en el mismo commit que la forma del proyecto** (regla C9 del proceso). La API detallada,
clase a clase, la genera DocFX a partir de los comentarios XML.

## Dependencias permitidas

```
                 Aida.Engine            (núcleo puro: sin dependencias de la solución)
                      ▲
                 Aida.Platform          (identidad, auditoría, sellado, licencia, puertos comunes)
              ▲       ▲        ▲
   Aida.Capture  Aida.Cognition  Aida.Channel  Aida.Arms.Health  Aida.Arms.Agenda   (adaptadores y brazos)
              ▲       ▲        ▲          ▲
                 Aida.Web   Aida.Cli    (raíces de composición: las únicas que ven todo)
```

- Una flecha va del que depende hacia aquel del que depende.
- **Ningún brazo depende de otro brazo, y ningún adaptador depende de un brazo**
  ([ADR 0011](../adr/0011-brazos-y-comunicacion.md)).
- Las comprueba `Aida.ArchitectureTests`, con los guardianes de `Aida.Guards`
  ([ADR 0015](../adr/0015-forma-del-codigo.md), [ADR 0019](../adr/0019-guardianes-propios.md)).
- `Aida.Guards` queda fuera del diagrama: es solo de desarrollo, no depende de ningún proyecto de la
  solución y ninguno de `src/` depende de él.

## Proyectos

| Proyecto | Responsabilidad | Página |
|---|---|---|
| `Aida.Engine` | Decidir: ontología, estado, eventos, aleatoriedad, deliberación, autoridad, packs | [Engine](Engine.md) |
| `Aida.Platform` | Lo común a todos los brazos: identidad, auditoría, sellado, licencia, rutas, puertos | [Platform](Platform.md) |
| `Aida.Capture` | Adaptadores nativos: audio, transcripción, OCR, PDF | [Capture](Capture.md) |
| `Aida.Cognition` | Proveedores de lenguaje, intención y redacción guiada | [Cognition](Cognition.md) |
| `Aida.Channel` | El canal de mensajería (WhatsApp) y su recogida | [Channel](Channel.md) |
| `Aida.Arms.Health` | El brazo de salud: la tubería de la consulta | [Arms.Health](Arms.Health.md) |
| `Aida.Arms.Agenda` | El brazo de agenda | [Arms.Agenda](Arms.Agenda.md) |
| `Aida.Web` | La aplicación local: Blazor Server y la composición | [Web](Web.md) |
| `Aida.Cli` | La línea de comandos y la composición para el operador | [Cli](Cli.md) |
| `Aida.Guards` | Solo de desarrollo: los guardianes que comprueban las reglas del repositorio | [Guards](Guards.md) |

Pendiente de decidir en la fase M7: `Aida.Relay`, el relé del canal que corre en la nube, desplegado
aparte del producto.
