# 0001 — El motor decide; el modelo de lenguaje lee y redacta

- **Estado:** Aceptado
- **Fecha:** 2026-10-28
- **Relacionados:** [0002](0002-agnosticismo-de-dominio.md), [0007](0007-autoridad-y-puerta-de-herramientas.md), [0013](0013-python-como-oraculo.md)

## Contexto
Un asistente cuya decisión sale de un modelo de lenguaje:
- cambia de comportamiento al cambiar de modelo;
- no se puede auditar;
- deja de funcionar cuando el proveedor cae.

AIDA nació para lo contrario: una motivación aritmética, no inferida.

## Decisión
- Un motor determinista decide **si** actuar, **qué** hacer y **si está autorizado**.
- El modelo de lenguaje tiene dos funciones estrechas: entender la entrada (proponer una intención
  contra un catálogo cerrado) y redactar la salida. **Proponer no es decidir.**
- Todo lo que entra de fuera (mensajes, documentos, respuestas de herramientas, salidas del modelo)
  es **dato**, nunca instrucción.
- Si el proveedor cae, AIDA sigue decidiendo y solo pierde la voz. Cada texto redactado tiene un
  respaldo literal.

## Consecuencias
- Cambiar de modelo cambia la calidad de la prosa, nunca lo que AIDA hace.
- Ninguna salida del modelo muta estado: llega al motor como una entrada más, que el motor valida.
- Hay que mantener un respaldo literal de cada texto, y eso cuesta.

## Cómo se hace cumplir
- Test de arquitectura: `Aida.Engine` no referencia ningún proyecto ni paquete de cliente de modelo.
  Los clientes viven en su propio proyecto de adaptadores.
- Los logs dorados ([0013](0013-python-como-oraculo.md)) se generan sin proveedor de lenguaje.
- Cada flujo tiene un test con el proveedor caído.
