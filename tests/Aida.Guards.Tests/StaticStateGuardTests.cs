using System.Collections;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Aida.Guards.Reflection;

namespace Aida.Guards.Tests;

public sealed class StaticStateGuardTests
{
    private sealed class CasoEstaticoMutable
    {
        public static int Contador = 0;
    }

    private sealed class CasoInmutable
    {
        public const int Constante = 42;
        public static readonly int Numero = 100;
        public static readonly ImmutableArray<int> Elementos = ImmutableArray<int>.Empty;
    }

    private sealed class CasoColeccionMutable
    {
        public static readonly List<int> Lista = [];
        public static readonly int[] Arreglo = [];
        public static readonly Dictionary<string, int> Diccionario = [];
        public static readonly HashSet<int> Conjunto = [];
        public static readonly Queue<int> Cola = new();
        public static readonly Stack<int> Pila = new();
        public static readonly SortedDictionary<string, int> DiccionarioOrdenado = [];
        public static readonly SortedList<string, int> ListaOrdenada = [];
        public static readonly SortedSet<int> ConjuntoOrdenado = [];
        public static readonly LinkedList<int> ListaEnlazada = new();
        public static readonly ArrayList ListaVieja = [];
        public static readonly Hashtable TablaHash = [];
    }

    private sealed class CasoPropiedadAutomatica
    {
        public static int Total { get; set; }
    }

    [CompilerGenerated]
    private sealed class CasoGeneradoPorCompilador
    {
        public static int CampoInterno = 1;
    }

    private readonly StaticStateGuard _guard = new();

    [Fact]
    public void Campo_estatico_mutable_falla()
    {
        var violations = _guard.CheckTypes([typeof(CasoEstaticoMutable)]);

        var violation = Assert.Single(violations);
        Assert.Equal("C4", violation.Rule);
        Assert.Contains("Contador", violation.Location);
        Assert.Equal(0, violation.Line);
        Assert.Contains("Estado estático mutable", violation.Message);
    }

    [Fact]
    public void Campo_static_readonly_inmutable_y_const_pasan()
    {
        var violations = _guard.CheckTypes([typeof(CasoInmutable)]);
        Assert.Empty(violations);
    }

    [Fact]
    public void Campo_static_readonly_con_coleccion_mutable_falla()
    {
        var violations = _guard.CheckTypes([typeof(CasoColeccionMutable)]);

        Assert.Equal(12, violations.Count);
        Assert.All(violations, v => Assert.Equal("C4", v.Rule));
    }

    [Fact]
    public void Campo_static_readonly_con_ImmutableArray_pasa()
    {
        var violations = _guard.CheckTypes([typeof(CasoInmutable)]);
        Assert.Empty(violations);
    }

    [Fact]
    public void Propiedad_estatica_automatica_con_setter_falla()
    {
        var violations = _guard.CheckTypes([typeof(CasoPropiedadAutomatica)]);

        var violation = Assert.Single(violations);
        Assert.Equal("C4", violation.Rule);
        Assert.EndsWith(".Total", violation.Location);
    }

    [Fact]
    public void Tipo_generado_por_el_compilador_se_ignora()
    {
        var violations = _guard.CheckTypes([typeof(CasoGeneradoPorCompilador)]);
        Assert.Empty(violations);
    }

    [Fact]
    public void Check_con_ensamblado_ejecuta_la_comprobacion_de_todos_sus_tipos()
    {
        var violations = _guard.Check(typeof(StaticStateGuard).Assembly);
        Assert.Empty(violations);
    }
}
