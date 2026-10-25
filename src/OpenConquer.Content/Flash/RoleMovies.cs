using OpenConquer.Content.Media;

namespace OpenConquer.Content.Flash;

/// <summary>
/// The retail 5517 create-role movies, baked offline (see <c>docs/startup/ROLE-001A.md</c>) and embedded under
/// <c>Assets/Role/</c>.
/// </summary>
public static class RoleMovies
{
    /// <summary><c>data/main/role.dat</c>: the create-role screen.</summary>
    public static FlashMovie LoadRole() =>
        Load("role", 349550, "D5276A46D7E2D8D0207B7D062DADA83A944B0C6A3E79FB3BEF2F604F5FAC50AD");

    /// <summary><c>data/main/flash/characterInAndOut.swf</c>: the class portraits role.dat loads into its stage.</summary>
    public static FlashMovie LoadCharacterInAndOut() =>
        Load("characterInAndOut", 271221, "F8FBFE17DF5E8F9D9675ACFD0FA28691B82D23C4EEF05D280F7DC8677CCC8F35");

    private static FlashMovie Load(string name, int length, string sha256)
    {
        string directory = $"Role/{name}";
        byte[] json = EmbeddedMedia.Read($"{directory}/{name}.movie.json.br", length, sha256, compressed: true);
        return FlashMovieReader.Read(json, directory);
    }
}
