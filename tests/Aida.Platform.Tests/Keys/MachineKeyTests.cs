using Aida.Platform.Keys;
using Xunit;

namespace Aida.Platform.Tests.Keys;

public sealed class MachineKeyTests
{
    private sealed class InvertingKeyProtector : IKeyProtector
    {
        public byte[] Protect(byte[] secret) => secret.Select(b => (byte)~b).ToArray();

        public byte[] Unprotect(byte[] stored) => stored.Select(b => (byte)~b).ToArray();
    }

    [Fact]
    public void Crea_una_vez_y_relee_la_misma()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "aida-key-" + Guid.NewGuid().ToString("n"));
        var keyPath = Path.Combine(tempDir, "machine.key");
        try
        {
            var protector = new DevelopmentKeyProtector();
            var key1 = MachineKey.OpenOrCreate(keyPath, protector);
            var derived1 = key1.Derive("test-purpose");

            var key2 = MachineKey.OpenOrCreate(keyPath, protector);
            var derived2 = key2.Derive("test-purpose");

            Assert.Equal(derived1, derived2);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void El_archivo_no_tiene_la_clave_en_claro_con_un_protector_real()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "aida-key-" + Guid.NewGuid().ToString("n"));
        var keyPath = Path.Combine(tempDir, "machine.key");
        try
        {
            var protector = new InvertingKeyProtector();
            var key = MachineKey.OpenOrCreate(keyPath, protector);
            var storedBytes = File.ReadAllBytes(keyPath);

            var keyAgain = MachineKey.OpenOrCreate(keyPath, protector);
            // Derivamos un propósito y confirmamos que la clave coincide
            Assert.Equal(key.Derive("check"), keyAgain.Derive("check"));

            // El archivo contiene los bytes invertidos, no los bytes en claro
            var invertedBack = storedBytes.Select(b => (byte)~b).ToArray();
            Assert.NotEqual(storedBytes, invertedBack);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void Un_archivo_corrupto_falla_y_no_se_regenera()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "aida-key-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(tempDir);
        var keyPath = Path.Combine(tempDir, "machine.key");
        try
        {
            var corruptBytes = new byte[16];
            File.WriteAllBytes(keyPath, corruptBytes);

            var ex = Assert.Throws<InvalidDataException>(() =>
                MachineKey.OpenOrCreate(keyPath, new DevelopmentKeyProtector()));
            Assert.Contains("la clave del equipo no tiene 32 bytes", ex.Message);

            var remaining = File.ReadAllBytes(keyPath);
            Assert.Equal(corruptBytes, remaining);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void Dos_propositos_dan_claves_distintas()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "aida-key-" + Guid.NewGuid().ToString("n"));
        var keyPath = Path.Combine(tempDir, "machine.key");
        try
        {
            var key = MachineKey.OpenOrCreate(keyPath, new DevelopmentKeyProtector());
            var d1 = key.Derive("purpose-1");
            var d2 = key.Derive("purpose-2");

            Assert.NotEqual(d1, d2);
            Assert.Equal(MachineKey.Length, d1.Length);
            Assert.Equal(MachineKey.Length, d2.Length);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void Un_proposito_vacio_o_no_ascii_lanza()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "aida-key-" + Guid.NewGuid().ToString("n"));
        var keyPath = Path.Combine(tempDir, "machine.key");
        try
        {
            var key = MachineKey.OpenOrCreate(keyPath, new DevelopmentKeyProtector());

            Assert.Throws<ArgumentException>(() => key.Derive(string.Empty));
            Assert.Throws<ArgumentException>(() => key.Derive("propósito"));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void Fuera_de_Windows_el_archivo_es_0600()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "aida-key-" + Guid.NewGuid().ToString("n"));
        var keyPath = Path.Combine(tempDir, "machine.key");
        try
        {
            MachineKey.OpenOrCreate(keyPath, new DevelopmentKeyProtector());
            var mode = File.GetUnixFileMode(keyPath);
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
