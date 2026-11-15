namespace Aida.Platform.Identity;

/// <summary>Cuenta los fallos por usuario y obliga a esperar tras cinco.</summary>
/// <remarks>
/// Es la regla de <c>identity.py:199-232</c> de la 0.1: el quinto fallo seguido bloquea 30 s y pone la cuenta a cero.
/// Se cuenta en memoria y por usuario escrito, exista o no: reiniciar el servidor la pone a cero, y es aceptable
/// porque reiniciarlo cuesta más que esperar. Lo que protege a la contraseña es que cada intento cueste.
/// </remarks>
internal sealed class LoginThrottle
{
    internal const int MaxFailures = 5;
    internal static readonly TimeSpan Penalty = TimeSpan.FromSeconds(30);

    private readonly TimeProvider _time;
    private readonly Dictionary<string, int> _failures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _blockedUntil = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    internal LoginThrottle(TimeProvider time) => _time = time;

    internal TimeSpan Wait(string key)
    {
        lock (_gate)
        {
            if (!_blockedUntil.TryGetValue(key, out var until))
            {
                return TimeSpan.Zero;
            }

            var left = until - _time.GetUtcNow();
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }
    }

    internal int AttemptsLeft(string key)
    {
        lock (_gate)
        {
            return MaxFailures - _failures.GetValueOrDefault(key);
        }
    }

    /// <summary>Anota un fallo.</summary>
    /// <returns><see langword="true"/> si este fallo obliga a esperar.</returns>
    internal bool Fail(string key)
    {
        lock (_gate)
        {
            var failures = _failures.GetValueOrDefault(key) + 1;
            var blocked = failures >= MaxFailures;
            if (blocked)
            {
                _blockedUntil[key] = _time.GetUtcNow() + Penalty;
                failures = 0;
            }

            _failures[key] = failures;
            return blocked;
        }
    }

    internal void Succeed(string key)
    {
        lock (_gate)
        {
            _failures.Remove(key);
        }
    }
}
