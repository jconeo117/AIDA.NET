# Aida.Cli

- **Estado:** creado vacío en N000; se llena en M6.
- **ADR:** [0015](../adr/0015-forma-del-codigo.md)

## Responsabilidad
La línea de comandos del operador: versión, diagnóstico de captura, consulta por archivos, auditoría, licencias de prueba. Segunda raíz de composición.

## Depende de
Todos los proyectos de `src/`.

## Expone
_Se completa en la unidad que lo construye: las interfaces públicas y los puntos de extensión._

## Lo que no hace
No duplica la orquestación de la web: las dos llaman a los mismos servicios.
