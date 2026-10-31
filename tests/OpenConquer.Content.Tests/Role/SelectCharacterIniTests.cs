using System.Text;
using OpenConquer.Content.Role;

namespace OpenConquer.Content.Tests.Role;

public sealed class SelectCharacterIniTests
{
    [Fact]
    public void Parse_ReadsSectionsAndTurnsTildesIntoSpaces()
    {
        SelectCharacterIni ini = Parse("[logo]\r\nxPos=776\r\nyPos=2\r\n\r\n[info]\r\ninfo0=~~~~The taoist\\n~~~~is great\r\n");

        Assert.Equal("776", ini.Get("logo", "xPos"));
        Assert.Equal("    The taoist\\n    is great", ini.Get("info", "info0"));
        Assert.Null(ini.Get("logo", "zPos"));
    }

    [Theory]
    [InlineData("[a]\r\nkey=one=two\r\n")]
    [InlineData("[a]\r\nbad key=value\r\n")]
    [InlineData("[a]\r\n=value\r\n")]
    public void Parse_SkipsLinesCIniRejects(string file)
    {
        Assert.Empty(Parse(file).Section("a"));
    }

    [Fact]
    public void Parse_CutsCommentsDropsTheLastCharacterOfEachLineAndUrlDecodes()
    {
        SelectCharacterIni ini = Parse("[s]\r\nurl=http://x\r\nplus=a+b%41\r\nlf=abc\nlast=end");

        Assert.Equal("http:", ini.Get("s", "url"));
        Assert.Equal("a bA", ini.Get("s", "plus"));
        Assert.Equal("ab", ini.Get("s", "lf"));
        Assert.Equal("end", ini.Get("s", "last"));
    }

    [Fact]
    public void Parse_ARepeatedSectionStartsOver()
    {
        SelectCharacterIni ini = Parse("[s]\r\na=1\r\n[s]\r\nb=2\r\n");

        Assert.Null(ini.Get("s", "a"));
        Assert.Equal("2", ini.Get("s", "b"));
    }

    [Fact]
    public void Load_ValidatesOriginalRetail5517IniFromTrackedPayload()
    {
        string retailRoot = Path.Combine(AppContext.BaseDirectory, "RetailFixture");
        SelectCharacterIni ini = SelectCharacterIni.Load(new ClientContentRoot(retailRoot));

        Assert.Equal("776", ini.Get("logo", "xPos"));
        Assert.Equal("Back", ini.Get("tip", "returnLogin"));
        Assert.Equal("Monk", ini.Get("affirmPanel", "vocation10"));
        Assert.Equal("Saint", ini.Get("affirmPanel", "vocation11"));
    }

    [Fact]
    public void Load_WithoutOriginalRetailIni_FailsClosed()
    {
        string root = Directory.CreateTempSubdirectory("oc-role-ini-").FullName;
        try
        {
            Assert.Throws<FileNotFoundException>(() => SelectCharacterIni.Load(new ClientContentRoot(root)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Load_ParsesLooseRoleConfigurationWithoutEmbeddedProvenanceRules()
    {
        string root = Directory.CreateTempSubdirectory("oc-role-ini-").FullName;
        try
        {
            string path = Path.Combine(root, "ini", "selectCharacter.ini");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "[logo]\r\nxPos=42\r\n", Encoding.ASCII);

            SelectCharacterIni configuration = SelectCharacterIni.Load(new ClientContentRoot(root));

            Assert.Equal("42", configuration.Get("logo", "xPos"));
            Assert.Null(configuration.Get("affirmPanel", "vocation10"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static SelectCharacterIni Parse(string file) => SelectCharacterIni.Parse(Encoding.ASCII.GetBytes(file));
}
