using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Aida.Engine.Hashing;
using Aida.Engine.Ontology;

namespace Aida.Engine.Randomness;

/// <summary>La fuente de aleatoriedad de una corrida: cada sorteo sale de una clave, no de una secuencia compartida.</summary>
/// <remarks>
/// <para>Es <c>RngSource</c> de la 0.1 (<c>rng.py:43-62</c>). El generador de una clave es función pura de la semilla y
/// la clave: <c>BLAKE2b(semilla \x1f parte \x1f …)</c> de 8 bytes, leída en orden de red, siembra un Mersenne
/// Twister. Así se puede recalcular un sorteo sin rehacer los anteriores, y ponerse al día de golpe da lo mismo que
/// hacerlo por tramos (ADR 0023).</para>
/// <para>El separador <c>U+001F</c> evita que <c>("ab", "c")</c> y <c>("a", "bc")</c> den la misma clave. Las partes
/// enteras se escriben como <c>str(int)</c> de Python, que es <see cref="long.ToString(IFormatProvider)"/> con la
/// cultura invariante.</para>
/// </remarks>
internal sealed class RngSource
{
    private const char Separator = '\u001f';

    /// <summary>Crea la fuente de una corrida.</summary>
    /// <param name="seed">La semilla, que se escribe al abrir el registro para poder reproducir la corrida.</param>
    internal RngSource(long seed) => Seed = seed;

    /// <summary>La semilla de la corrida.</summary>
    internal long Seed { get; }

    /// <summary>El generador propio de una clave.</summary>
    /// <param name="key">Las partes de la clave, ya como texto.</param>
    /// <returns>Un generador nuevo. La misma clave da siempre la misma secuencia.</returns>
    internal MersenneTwister Stream(params string[] key)
    {
        var material = string.Join(Separator, [Seed.ToString(CultureInfo.InvariantCulture), .. key]);
        var digest = Blake2b.Hash(Encoding.UTF8.GetBytes(material), 8);
        return MersenneTwister.FromSeed(BinaryPrimitives.ReadUInt64BigEndian(digest));
    }

    /// <summary>El generador del sorteo de una deliberación: la clave <c>("decide", agente, tick)</c>.</summary>
    /// <param name="agent">El agente que delibera.</param>
    /// <param name="tick">El tick absoluto de la deliberación.</param>
    /// <returns>El generador de <c>decide.py:335</c>.</returns>
    internal MersenneTwister ForDecision(AgentId agent, long tick) =>
        Stream("decide", agent.Value, tick.ToString(CultureInfo.InvariantCulture));
}
