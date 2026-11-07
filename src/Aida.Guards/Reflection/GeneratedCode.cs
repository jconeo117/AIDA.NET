using System.Runtime.CompilerServices;

namespace Aida.Guards.Reflection;

/// <summary>Reconoce los tipos que escribe el compilador y no una persona.</summary>
/// <remarks>Los guardianes de reflexión los saltan: su código no lo escribe nadie a mano y no se puede arreglar.</remarks>
internal static class GeneratedCode
{
    /// <summary>Indica si un tipo, o cualquiera de los que lo contienen, lo generó el compilador.</summary>
    /// <param name="type">El tipo a mirar.</param>
    /// <returns><c>true</c> si lleva <c>[CompilerGenerated]</c> él o su tipo contenedor.</returns>
    /// <remarks>Hace falta subir por los contenedores: los campos de respaldo de las lambdas de Razor viven en clases <c>&lt;&gt;c</c> anidadas en un tipo que sí es de la persona.</remarks>
    internal static bool IsGenerated(Type type)
    {
        for (var current = type; current is not null; current = current.DeclaringType)
        {
            if (current.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
            {
                return true;
            }
        }

        return false;
    }
}
