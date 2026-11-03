using System.Collections;
using System.Reflection;

namespace Aida.Guards.Reflection;

/// <summary>Regla C4: sin estado estático mutable.</summary>
/// <remarks>
/// El estado estático mutable es estado compartido que ningún evento registra: rompe B7 y hace que dos
/// tests se pisen. Se marca un campo <c>static</c> que no es <c>const</c> ni <c>readonly</c>, o uno
/// <c>static readonly</c> cuyo tipo declarado es un array, <c>List&lt;&gt;</c>, <c>Dictionary&lt;,&gt;</c>,
/// <c>HashSet&lt;&gt;</c>, <c>Queue&lt;&gt;</c>, <c>Stack&lt;&gt;</c>, <c>SortedDictionary&lt;,&gt;</c>,
/// <c>SortedList&lt;,&gt;</c>, <c>SortedSet&lt;&gt;</c>, <c>LinkedList&lt;&gt;</c>, <c>ArrayList</c> o
/// <c>Hashtable</c>. Se usa <c>ImmutableArray</c> o las colecciones congeladas. Los tipos generados por el
/// compilador no cuentan; los campos de respaldo de una propiedad automática sí, porque
/// <c>static int X { get; set; }</c> es estado mutable.
/// </remarks>
public sealed class StaticStateGuard
{
    /// <summary>Comprueba todos los tipos de un ensamblado.</summary>
    /// <param name="assembly">El ensamblado.</param>
    /// <returns>Lo mismo que <see cref="CheckTypes"/> sobre <c>assembly.GetTypes()</c>.</returns>
    /// <remarks>Es la entrada que usa la regla sobre el repositorio.</remarks>
    public IReadOnlyList<Violation> Check(System.Reflection.Assembly assembly) => CheckTypes(assembly.GetTypes());

    /// <summary>Comprueba unos tipos concretos.</summary>
    /// <param name="types">Los tipos; los anidados no se recorren solos, hay que pasarlos.</param>
    /// <returns>Un <c>C4</c> por campo, con <c>Location</c> igual a <c>Tipo.campo</c> y línea 0.</returns>
    /// <remarks>Existe para probar con tipos de un test sin cargar un ensamblado entero.</remarks>
    public IReadOnlyList<Violation> CheckTypes(IReadOnlyList<Type> types)
    {
        var violations = new List<Violation>();
        foreach (var type in types.Where(candidate => !GeneratedCode.IsGenerated(candidate)))
        {
            var fields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            foreach (var field in fields)
            {
                if (field.IsLiteral || (field.IsInitOnly && !IsMutableCollection(field.FieldType)))
                {
                    continue;
                }

                var location = $"{type.FullName ?? type.Name}.{FieldName(field)}";
                violations.Add(new Violation(
                    "C4",
                    location,
                    0,
                    $"Estado estático mutable en {location}. Hazlo const, static readonly de un tipo inmutable (ImmutableArray o colección congelada) o pásalo a una instancia que emita eventos (00-task.md, C4)."));
            }
        }

        return violations.OrderBy(violation => violation.Location, StringComparer.Ordinal).ToList();
    }

    private static bool IsMutableCollection(Type type) =>
        type.IsArray
        || type == typeof(ArrayList)
        || type == typeof(Hashtable)
        || (type.IsGenericType && IsMutableDefinition(type.GetGenericTypeDefinition()));

    private static bool IsMutableDefinition(Type definition) =>
        definition == typeof(List<>)
        || definition == typeof(Dictionary<,>)
        || definition == typeof(HashSet<>)
        || definition == typeof(Queue<>)
        || definition == typeof(Stack<>)
        || definition == typeof(SortedDictionary<,>)
        || definition == typeof(SortedList<,>)
        || definition == typeof(SortedSet<>)
        || definition == typeof(LinkedList<>);

    // El respaldo de una propiedad automática se llama «<Nombre>k__BackingField»; el informe muestra «Nombre».
    private static string FieldName(FieldInfo field) =>
        field.Name.StartsWith('<') && field.Name.Contains('>')
            ? field.Name[1..field.Name.IndexOf('>')]
            : field.Name;
}
