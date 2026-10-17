using System.Text;
using OpenConquer.Content.Login;

namespace OpenConquer.Content.Tests.Login;

public sealed class ClientVersionLabelTests
{
    [Theory]
    [InlineData("5517", "version: 5517")]
    [InlineData("5517\r\n", "version: 5517")]
    [InlineData("5517=abc", "version: 5517")]
    [InlineData("v5517b", "version: v5517b")]
    [InlineData("", "version: ")]
    public void Format_KeepsTheLeadingLettersAndDigitsLikeTheLoadVarsRoundTrip(string file, string expected)
    {
        Assert.Equal(expected, ClientVersionLabel.Format(Encoding.ASCII.GetBytes(file)));
    }
}
