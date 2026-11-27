using System.Security.Cryptography;
using System.Text;
using Aida.Platform.Records;
using Xunit;

namespace Aida.Platform.Tests.Records;

public sealed class RecordFieldsTests
{
    [Fact]
    public void Canonical_ordena_ordinal_y_escapa_no_ascii()
    {
        var fields = new Dictionary<string, string>
        {
            ["b"] = "2",
            ["a"] = "ñ",
        };

        var canonical = RecordFields.Canonical(fields);
        Assert.Equal("{\"a\":\"\\u00F1\",\"b\":\"2\"}", canonical);
    }

    [Fact]
    public void Digest_de_vacio_es_sha256_de_llaves_vacias()
    {
        var empty = new Dictionary<string, string>();
        var digest = RecordFields.Digest(empty);
        var expectedSha = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("{}")));

        Assert.Equal(expectedSha, digest);
        Assert.Equal(64, digest.Length);
    }

    [Fact]
    public void Digest_es_consistente_sin_importar_orden_de_insercion()
    {
        var f1 = new Dictionary<string, string> { ["x"] = "1", ["y"] = "2" };
        var f2 = new Dictionary<string, string> { ["y"] = "2", ["x"] = "1" };

        Assert.Equal(RecordFields.Digest(f1), RecordFields.Digest(f2));
    }
}
