using System.Text;
using OpenConquer.Client.UI.Role;
using OpenConquer.Content.Text;

namespace OpenConquer.Client.Tests.UI.Role;

public sealed class RoleCreationTextTests
{
    private static readonly RoleCreationText s_text = new(
        ClientStringResources.Parse(Encoding.ASCII.GetBytes("10037=First Name should be 4-15 characters!\r\n11238=Initializing...\r\n11240=Starting... Please wait...\r\n")),
        ClientKeyedStringResources.Parse(Encoding.ASCII.GetBytes("STR_CREATE_ROLE_SUCCESS=Your character has been successfully created!\r\n")),
        Encoding.Latin1);

    [Fact]
    public void Texts_ComeFromTheClientStringTables()
    {
        Assert.Equal("First Name should be 4-15 characters!", s_text.NameLength);
        Assert.Equal("Initializing...", s_text.Initializing);
        Assert.Equal("Starting... Please wait...", s_text.Starting);
        Assert.Equal("Your character has been successfully created!", s_text.Created);
    }

    [Fact]
    public void Refusal_StopsAtTheNulAndBlanksControlCharacters()
    {
        byte[] refusal = [.. Encoding.ASCII.GetBytes("Name\ttaken|x"), 0, .. Encoding.ASCII.GetBytes("garbage")];

        Assert.Equal("Name taken|x", s_text.Refusal(refusal));
    }
}
