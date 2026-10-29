# AIDA

**Asistente Inteligente Determinista y Autónomo.** Un secretario virtual cuya motivación es
aritmética, no inferencia: un motor determinista decide si actuar, qué hacer y si está autorizado.
El modelo de lenguaje solo entiende la entrada y redacta la salida.

AIDA es una plataforma con **brazos** por campo:
- **salud:** consulta grabada, nota clínica y códigos CIE-10, revisados por el profesional;
- **agenda:** citas, bloqueos y conversaciones por WhatsApp.

Corre en el equipo, en Windows, y funciona sin conexión.

## Documentación

- [Decisiones de arquitectura](docs/adr/README.md)
- [Arquitectura por proyecto](docs/architecture/README.md)
- API: se genera con DocFX en `docs/api/`
- [Cambios por versión](CHANGELOG.md)

## Requisitos para desarrollar

- SDK de .NET 10 (`global.json`)
- Windows 10 u 11 de 64 bits para ejecutar el producto. Se puede compilar y probar casi todo en otro
  sistema, salvo lo que usa DPAPI.
