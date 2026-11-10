using System.Text;
using Aida.Engine.Hashing;

namespace Aida.Engine.Events;

/// <summary>Huella del contenido exacto de una entrada de fuera (<c>engine/events.py:53-70</c>).</summary>
/// <remarks>
/// Es BLAKE2b de 16 bytes, como en la 0.1, para que un journal pueda detectar que el contenido divergió sin
/// volver a tener el texto a mano. Es interna: solo <see cref="EventLog"/> la calcula.
/// </remarks>
internal static class ContentDigest
{
    private const int _size = 16;

    // Estricta: un sustituto suelto revienta, como content.encode("utf-8") en la 0.1.
    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>La huella del contenido exacto de una entrada de fuera: BLAKE2b de 16 bytes, en hexadecimal en minúsculas.</summary>
    /// <param name="content">El contenido, sin resumir ni normalizar.</param>
    /// <returns>La huella en hexadecimal minúsculas.</returns>
    /// <exception cref="EncoderFallbackException">Si el contenido tiene sustitutos UTF-16 sueltos.</exception>
    internal static string Of(string content) =>
        Convert.ToHexStringLower(Blake2b.Hash(_strictUtf8.GetBytes(content), _size));
}
