using OpenConquer.Client.UI.Login.Account;

namespace OpenConquer.Client.Tests.UI.Login.Account;

public sealed class LoginAccountHistoryTests
{
    [Fact]
    public void History_IsBoundedToTenEntriesAndDeduplicatedInListOrder()
    {
        string folder = Directory.CreateTempSubdirectory("oc-login-history-").FullName;
        try
        {
            LoginAccountHistory history = new(folder);
            for (int index = 0; index < 15; index++)
            {
                Assert.True(history.TryRecord($"Player_{index}"));
            }

            Assert.Equal(10, history.Accounts.Count);
            Assert.Equal("Player_5", history.Accounts[0]);
            Assert.Equal("Player_14", history.Accounts[^1]);
            Assert.True(history.TryRecord("Player_10"));
            Assert.Equal("Player_5", history.Accounts[0]);
            Assert.Equal(10, history.Accounts.Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(history.Accounts.ToArray(), new LoginAccountHistory(folder).Accounts.ToArray());
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Theory]
    [InlineData("{invalid")]
    [InlineData("{\"schemaVersion\":999,\"accounts\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"accounts\":[\"A\",\"A\"]}")]
    [InlineData("{\"schemaVersion\":1,\"accounts\":[\"bad name\"]}")]
    public void MalformedOrUnknownHistory_IsNeverOverwritten(string contents)
    {
        string folder = Directory.CreateTempSubdirectory("oc-login-invalid-").FullName;
        try
        {
            string path = Path.Combine(folder, "login-accounts.json");
            File.WriteAllText(path, contents);
            LoginAccountHistory history = new(folder);

            Assert.False(history.Writable);
            Assert.Empty(history.Accounts);
            Assert.False(history.TryRecord("Player"));
            Assert.Equal(contents, File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
