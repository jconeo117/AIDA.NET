# Avisos de terceros

## ARM optimized-routines

- **Origen:** https://github.com/ARM-software/optimized-routines
- **Commit:** `20859d3fd9e797dde592c489c8ef7d383f778620`
- **Licencia:** `MIT OR Apache-2.0 WITH LLVM-exception`. El repositorio usa la opción MIT, que es la que aplica aquí.
- **Decisión:** [ADR 0026](docs/adr/0026-pow-propio.md). Se portan las funciones de `pow` y sus tablas para que el
  motor calcule las potencias igual que la 0.1 en cualquier sistema operativo.

### Archivos portados

| Archivo de origen | Dónde vive en este repositorio | Copyright |
|---|---|---|
| `math/pow.c` | `src/Aida.Engine/Numerics/AorPow.cs` | `Copyright (c) 2018-2026, Arm Limited.` |
| `math/pow_log_data.c` | `src/Aida.Engine/Numerics/AorPowLogTable.cs` | `Copyright (c) 2018, Arm Limited.` |
| `math/exp_data.c` | `src/Aida.Engine/Numerics/AorExpTable.cs` | `Copyright (c) 2018-2023, Arm Limited.` |

Las tablas se generan desde `.claude/workflow/N004-anexo-tablas-pow.txt` (401 líneas de datos, SHA-256
`455927e49676ae67ecb1d61667a0af0b9923a1788bd7fcb54f0473bed4be3e6e` sobre las tablas reconstruidas) y no se editan
a mano.

### Licencia MIT (texto del `LICENSE` de ese commit)

```text
MIT License

Copyright (c) 1999-2022, Arm Limited.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
