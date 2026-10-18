using OpenConquer.Launcher.Play;

namespace OpenConquer.Launcher.Tests.Play;

public sealed class LauncherHomePageTests
{
    private static readonly LauncherHomePage s_page = new(new Uri("https://www.example.org/news/"));

    [Theory]
    [InlineData("https://www.example.org/news/2026")]
    [InlineData("https://example.org/")]
    [InlineData("https://consent.example.org/form")]
    public void SameSiteHttpsPages_StayInThePanel(string target) =>
        Assert.True(s_page.KeepsInPanel(new Uri(target)));

    [Theory]
    [InlineData("http://www.example.org/")]
    [InlineData("https://www.example.org.attacker.test/")]
    [InlineData("https://notexample.org/")]
    [InlineData("https://user@www.example.org/")]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    public void OtherAddresses_LeaveThePanel(string target) =>
        Assert.False(s_page.KeepsInPanel(new Uri(target)));

    [Theory]
    [InlineData("https://other.test/", true)]
    [InlineData("http://other.test/", false)]
    [InlineData("file:///tmp/x", false)]
    public void OnlyHttpsLinks_OpenInTheSystemBrowser(string target, bool expected) =>
        Assert.Equal(expected, LauncherHomePage.OpensExternally(new Uri(target)));

    [Theory]
    [InlineData("http://www.example.org/")]
    [InlineData("https://user:secret@www.example.org/")]
    public void HomePage_MustBeHttpsWithoutCredentials(string address) =>
        Assert.Throws<ArgumentException>(() => new LauncherHomePage(new Uri(address)));

    [Fact]
    public void LauncherAssembly_CarriesAnHttpsHomePage()
    {
        LauncherHomePage page = LauncherHomePage.FromAssembly(typeof(LauncherHomePage).Assembly);

        Assert.Equal(Uri.UriSchemeHttps, page.Address.Scheme);
    }

    [Fact]
    public void AssemblyWithoutAHomePage_IsRejected() =>
        Assert.Throws<InvalidOperationException>(() => LauncherHomePage.FromAssembly(typeof(LauncherHomePageTests).Assembly));
}
