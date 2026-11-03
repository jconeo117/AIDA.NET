using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Aida.Guards.Reflection;

/// <summary>Regla C6: el motor es síncrono.</summary>
/// <remarks>
/// Con un motor síncrono, la decisión depende solo de la entrada y no del orden en que terminan unas
/// tareas (ADR 0004). Se marca todo método declarado, de cualquier visibilidad, de instancia o estático,
/// que devuelve <c>Task</c> (o un derivado), <c>Task&lt;T&gt;</c>, <c>ValueTask</c>, <c>ValueTask&lt;T&gt;</c>,
/// <c>IAsyncEnumerable&lt;T&gt;</c> o <c>IAsyncEnumerator&lt;T&gt;</c>, o que lleva <c>AsyncStateMachine</c> o
/// <c>AsyncIteratorStateMachine</c>. Es más estricto que «ningún método público» porque <c>async</c> solo
/// existe en los adaptadores. Los tipos generados por el compilador no cuentan.
/// </remarks>
public sealed class SynchronousSurfaceGuard
{
    /// <summary>Comprueba todos los tipos de un ensamblado.</summary>
    /// <param name="assembly">En el repositorio, <c>Aida.Engine</c>.</param>
    /// <returns>Lo mismo que <see cref="CheckTypes"/> sobre <c>assembly.GetTypes()</c>.</returns>
    /// <remarks>Es la entrada que usa la regla sobre el repositorio.</remarks>
    public IReadOnlyList<Violation> Check(System.Reflection.Assembly assembly) => CheckTypes(assembly.GetTypes());

    /// <summary>Comprueba unos tipos concretos.</summary>
    /// <param name="types">Los tipos.</param>
    /// <returns>Un <c>C6</c> por método, con <c>Location</c> igual a <c>Tipo.Método</c> y línea 0.</returns>
    /// <remarks>Existe para probar con tipos de un test sin cargar un ensamblado entero.</remarks>
    public IReadOnlyList<Violation> CheckTypes(IReadOnlyList<Type> types)
    {
        var violations = new List<Violation>();
        foreach (var type in types.Where(candidate => !GeneratedCode.IsGenerated(candidate)))
        {
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            foreach (var method in methods.Where(IsAsync))
            {
                var location = $"{type.FullName ?? type.Name}.{method.Name}";
                violations.Add(new Violation(
                    "C6",
                    location,
                    0,
                    $"Método asíncrono {location}. El motor es síncrono: devuelve el resultado directamente; lo asíncrono va en un adaptador (ADR 0004, 00-task.md C6)."));
            }
        }

        return violations.OrderBy(violation => violation.Location, StringComparer.Ordinal).ToList();
    }

    private static bool IsAsync(MethodInfo method) =>
        IsTaskLike(method.ReturnType) || method.IsDefined(typeof(AsyncStateMachineAttribute), inherit: false);

    private static bool IsTaskLike(Type type) =>
        type == typeof(Task)
        || type == typeof(ValueTask)
        || type.IsSubclassOf(typeof(Task))
        || (type.IsGenericType && IsAsyncGenericDefinition(type.GetGenericTypeDefinition()));

    private static bool IsAsyncGenericDefinition(Type definition) =>
        definition == typeof(ValueTask<>)
        || definition == typeof(IAsyncEnumerable<>)
        || definition == typeof(IAsyncEnumerator<>);
}
