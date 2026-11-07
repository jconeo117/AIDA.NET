using System.Runtime.CompilerServices;
using Aida.Guards.Reflection;

namespace Aida.Guards.Tests;

public sealed class SynchronousSurfaceGuardTests
{
    private sealed class CasoMetodosAsincronos
    {
        public Task MetodoTask() => Task.CompletedTask;

        public ValueTask<int> MetodoValueTask() => ValueTask.FromResult(1);

        public IAsyncEnumerable<int> MetodoAsyncEnumerable() => throw new NotImplementedException();

        public IAsyncEnumerator<int> MetodoAsyncEnumerator() => throw new NotImplementedException();

        public async void MetodoAsyncVoid()
        {
            await Task.Yield();
        }

        private Task MetodoPrivadoTask() => Task.CompletedTask;
    }

    private sealed class CasoMetodosSincronos
    {
        public int Calcular(int a, int b) => a + b;

        public string ObtenerTexto() => "hola";

        public void Ejecutar()
        {
        }

        private bool EsValido() => true;
    }

    [CompilerGenerated]
    private sealed class CasoGenerado
    {
        public Task MetodoIgnorado() => Task.CompletedTask;
    }

    private readonly SynchronousSurfaceGuard _guard = new();

    [Fact]
    public void Metodos_asincronos_fallan_incluyendo_privados()
    {
        var violations = _guard.CheckTypes([typeof(CasoMetodosAsincronos)]);

        Assert.Equal(6, violations.Count);
        Assert.All(violations, v =>
        {
            Assert.Equal("C6", v.Rule);
            Assert.Equal(0, v.Line);
            Assert.Contains("El motor es síncrono", v.Message);
        });

        Assert.Contains(violations, v => v.Location.EndsWith(".MetodoTask"));
        Assert.Contains(violations, v => v.Location.EndsWith(".MetodoValueTask"));
        Assert.Contains(violations, v => v.Location.EndsWith(".MetodoAsyncEnumerable"));
        Assert.Contains(violations, v => v.Location.EndsWith(".MetodoAsyncEnumerator"));
        Assert.Contains(violations, v => v.Location.EndsWith(".MetodoAsyncVoid"));
        Assert.Contains(violations, v => v.Location.EndsWith(".MetodoPrivadoTask"));
    }

    [Fact]
    public void Metodos_sincronos_pasan()
    {
        var violations = _guard.CheckTypes([typeof(CasoMetodosSincronos)]);
        Assert.Empty(violations);
    }

    [Fact]
    public void Tipo_generado_por_compilador_se_ignora()
    {
        var violations = _guard.CheckTypes([typeof(CasoGenerado)]);
        Assert.Empty(violations);
    }

    [Fact]
    public void Check_con_ensamblado_ejecuta_la_comprobacion_de_todos_sus_tipos()
    {
        var violations = _guard.Check(typeof(SynchronousSurfaceGuard).Assembly);
        Assert.Empty(violations);
    }
}
