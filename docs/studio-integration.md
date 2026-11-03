# Studio integration

OpenConquerStudio is a separate content-authoring application and build toolchain. It does not
own client networking, gameplay, world simulation or server authority.

The extraction moves the Studio application, Authoring, Runtime and ConquerImport projects,
their command-line/render-check tools, Studio tests and Studio documentation. The game solution
continues to use Content, Rendering, `OpenConquer.Imaging` and `OpenConquer.Rendering.World`.
Those shared implementations remain authoritative here; no parser or renderer fork is created.
All eight source projects are grouped under `src` in the client solution. The unused Gameplay
assembly was removed; gameplay code should acquire a separate assembly only when it has an implementation.

Studio initially consumes an exact client commit through its read-only source dependency.
Only the shared library projects are built, not the Client or Launcher applications. Changes
to those libraries, including character rendering, are reviewed and tested in this repository
before Studio advances its pinned dependency. Studio's source dependency can later be replaced
with versioned shared-library packages once their ownership, notices, CI and release workflow
are established. Repository separation does not itself establish dependency independence.

Studio's current pin predates the library naming cleanup and therefore still uses the previous
assembly/namespace names inside its immutable dependency. Advancing that pin requires updating
its project references and world-rendering imports together, then running Studio's tests and GPU
check. Do not modify the pinned source in place or add forwarding copies to this repository.

Compiled map `.ocmap` remains a candidate; the current game still loads original DMap content.
The server's `.ocbt` base terrain and `.ocmc` collision are different runtime projections.
An eventual authored map build must produce compatible client and server outputs from the
same source revision. Never replace authoritative server collision with client visual data.

Open a game in Studio by choosing **Open client project** and selecting this checkout, or pass
the game project directory to Studio. The current automatic authoring workspace is local and
ignored. For team assets, create a normal Studio project in a version-controlled content folder
and attach client source content through **Client content folder**. Studio does not yet replace
the client's runtime resources automatically.
