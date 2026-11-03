# Studio integration

OpenConquerStudio is a separate content-authoring application and build toolchain. It does not
own client networking, gameplay, world simulation or server authority.

The client solution has six source projects: Client, Content, Launcher, Networking, Platform,
and Rendering. Image decoding belongs to `OpenConquer.Content/Images`; map projection, puzzle
clocks, scenery ordering and world drawing belong to `OpenConquer.Rendering/World`. Rendering
references Content. Neither concern needs a separate assembly to support Studio.

Studio owns its application, Authoring, Runtime and ConquerImport projects, tools and tests.
Its repository carries source snapshots of the existing Content and Rendering projects under
`dependencies/OpenConquerClient`, with the source base commit and file SHA-256 hashes recorded
in `source.json`. This is ordinary tracked source, not a whole-client submodule or a reference
into a developer's neighboring checkout. Studio updates those copies deliberately and validates
actual client assets. Client and Studio changes do not propagate automatically between repositories.
No shared-library publishing or independent engine framework is required by this arrangement.

Compiled map `.ocmap` remains a candidate; the current game still loads original DMap content.
The server's `.ocbt` base terrain and `.ocmc` collision are different runtime projections.
An eventual authored map build must produce compatible client and server outputs from the
same source revision. Never replace authoritative server collision with client visual data.

Open a game in Studio by choosing **Open client project** and selecting this checkout, or pass
the game project directory to Studio. The current automatic authoring workspace is local and
ignored. For team assets, create a normal Studio project in a version-controlled content folder
and attach client source content through **Client content folder**. Studio does not yet replace
the client's runtime resources automatically.
