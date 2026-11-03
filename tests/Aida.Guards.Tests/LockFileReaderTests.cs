using System.Text.Json;
using Aida.Guards.Dependencies;

namespace Aida.Guards.Tests;

public sealed class LockFileReaderTests
{
    [Fact]
    public void Ignora_dependencias_con_type_project()
    {
        var json = """
            {
              "version": 2,
              "dependencies": {
                "net10.0": {
                  "Aida.Engine": {
                    "type": "Project",
                    "dependencies": {}
                  },
                  "xunit": {
                    "type": "Direct",
                    "requested": "[2.9.3, )",
                    "resolved": "2.9.3"
                  }
                }
              }
            }
            """;

        var packages = LockFileReader.ReadPackages(json);

        var package = Assert.Single(packages);
        Assert.Equal("xunit", package);
    }

    [Fact]
    public void Reune_varios_marcos_sin_repetidos_y_ordenados()
    {
        var json = """
            {
              "version": 2,
              "dependencies": {
                "net10.0": {
                  "ZetaPackage": {
                    "type": "Direct"
                  },
                  "AlfaPackage": {
                    "type": "Transitive"
                  }
                },
                "net8.0": {
                  "AlfaPackage": {
                    "type": "Direct"
                  },
                  "BetaPackage": {
                    "type": "Direct"
                  }
                }
              }
            }
            """;

        var packages = LockFileReader.ReadPackages(json);

        Assert.Equal(3, packages.Count);
        Assert.Equal("AlfaPackage", packages[0]);
        Assert.Equal("BetaPackage", packages[1]);
        Assert.Equal("ZetaPackage", packages[2]);
    }

    [Fact]
    public void Json_sin_seccion_dependencies_lanza_JsonException()
    {
        var json = """{ "version": 2 }""";
        Assert.Throws<JsonException>(() => LockFileReader.ReadPackages(json));
    }

    [Fact]
    public void Json_con_formato_invalido_lanza_JsonException()
    {
        var invalidJson = "{ esto no es un json valido";
        Assert.ThrowsAny<JsonException>(() => LockFileReader.ReadPackages(invalidJson));
    }
}
