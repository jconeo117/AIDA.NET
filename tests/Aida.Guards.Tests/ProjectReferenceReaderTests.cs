using System.Xml;
using Aida.Guards.Dependencies;

namespace Aida.Guards.Tests;

public sealed class ProjectReferenceReaderTests
{
    [Fact]
    public void Lee_referencias_con_barra_inclinada_y_barra_invertida()
    {
        var xml = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\..\src\Aida.Engine\Aida.Engine.csproj" />
                <ProjectReference Include="src/Aida.Platform/Aida.Platform.csproj" />
              </ItemGroup>
            </Project>
            """;

        var references = ProjectReferenceReader.Read(xml);

        Assert.Equal(2, references.Count);
        Assert.Equal("Aida.Engine", references[0]);
        Assert.Equal("Aida.Platform", references[1]);
    }

    [Fact]
    public void Elimina_referencias_repetidas_y_ordena_por_ordinal()
    {
        var xml = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="src/Zeta/Zeta.csproj" />
                <ProjectReference Include="src/Alfa/Alfa.csproj" />
                <ProjectReference Include="..\Alfa\Alfa.csproj" />
                <ProjectReference Include="src/Beta/Beta.csproj" />
              </ItemGroup>
            </Project>
            """;

        var references = ProjectReferenceReader.Read(xml);

        Assert.Equal(3, references.Count);
        Assert.Equal("Alfa", references[0]);
        Assert.Equal("Beta", references[1]);
        Assert.Equal("Zeta", references[2]);
    }

    [Fact]
    public void Xml_roto_lanza_XmlException()
    {
        var malformedXml = "<Project><ItemGroup><ProjectReference Include='";
        Assert.Throws<XmlException>(() => ProjectReferenceReader.Read(malformedXml));
    }
}
