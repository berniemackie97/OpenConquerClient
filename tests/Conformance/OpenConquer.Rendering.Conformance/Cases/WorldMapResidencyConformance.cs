using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using OpenConquer.Content;
using OpenConquer.Content.Map;
using OpenConquer.Rendering.Conformance.Support;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.World;

namespace OpenConquer.Rendering.Conformance.Cases;

internal static class WorldMapResidencyConformance
{
    private const int Width = 640;
    private const int Height = 480;
    private const int TextureTarget = 24;
    private const long PixelByteTarget = 6 * 1024 * 1024;

    internal static async Task RunAsync(string contentRoot)
    {
        PackagedClientContentSource source = PackagedClientContentSource.Open(contentRoot);
        GameMapCatalog catalog = GameMapCatalog.Load(source);
        WorldMapScene reference = WorldMapScene.Load(source, catalog, 1010, 61, 101, 0);
        WorldMapScene scene = WorldMapScene.Load(source, catalog, 1010, 61, 101, 0);
        WorkerSource workerSource = new(source, Environment.CurrentManagedThreadId);
        await using WorldMapImageLoader loader = new(workerSource);
        OpenGLConformanceHost.Run((device, framebuffer) =>
        {
            using WorldMapTextureRenderer textures = new(loader, device, TextureTarget, PixelByteTarget);
            using OpenGLRenderer renderer = device.CreateRenderer(new LogicalRenderSize(Width, Height),
                framebuffer.Width, framebuffer.Height, colorFormat: RenderColorFormat.Rgba8);
            (int X, int Y)[] route = [(61, 101), (64, 105), (72, 116), (90, 120), (128, 143), (114, 57), (52, 61)];
            Dictionary<(int, int), string> expected = [];
            using (WorldMapTextureRenderer synchronous = new(source, device))
            {
                foreach ((int x, int y) in route)
                {
                    reference.SetCameraTile(x, y);
                    _ = reference.Prepare(synchronous, Width, Height, 1000);
                    expected[(x, y)] = Capture(reference, synchronous, renderer);
                }

                Console.WriteLine($"Unbounded reference: {synchronous.ResidentTextureCount} textures, {synchronous.ResidentPixelBytes} RGBA bytes");
            }

            string? displayed = null;
            int pendingComparisons = 0;
            int maximumCount = 0;
            long maximumBytes = 0;
            for (int repeat = 0; repeat < 3; repeat++)
            {
                for (int stop = 0; stop <= route.Length; stop++)
                {
                    (int x, int y) = route[stop % route.Length];
                    int opensBefore = workerSource.Opens;
                    scene.SetCameraTile(x, y);
                    long start = Stopwatch.GetTimestamp();
                    while (!scene.Prepare(textures, Width, Height, 1000, textureBudget: 4))
                    {
                        if (displayed is not null)
                        {
                            AssertMatch(displayed, Capture(scene, textures, renderer), "pending");
                            pendingComparisons++;
                        }

                        if (Stopwatch.GetElapsedTime(start) > TimeSpan.FromSeconds(5))
                        {
                            throw new TimeoutException($"Map textures did not prepare for {x},{y}.");
                        }

                        Thread.Sleep(1);
                    }

                    displayed = Capture(scene, textures, renderer);
                    AssertMatch(expected[(x, y)], displayed, $"completed {x},{y}");
                    maximumCount = Math.Max(maximumCount, textures.ResidentTextureCount);
                    maximumBytes = Math.Max(maximumBytes, textures.ResidentPixelBytes);
                    if (stop == route.Length && opensBefore == workerSource.Opens)
                    {
                        throw new InvalidOperationException("The return view did not reload evicted textures.");
                    }
                }
            }

            if (maximumCount > TextureTarget || maximumBytes > PixelByteTarget || pendingComparisons == 0)
            {
                throw new InvalidOperationException($"Unexpected residency: {maximumCount} textures, {maximumBytes} RGBA bytes, {pendingComparisons} pending comparisons.");
            }

            Console.WriteLine($"Map residency: {3 * (route.Length + 1)} completed views, {pendingComparisons} pending comparisons, {maximumCount} textures, {maximumBytes} RGBA bytes");
        });
    }

    private static string Capture(WorldMapScene scene, WorldMapTextureRenderer textures, OpenGLRenderer renderer)
    {
        renderer.BeginFrame();
        try
        {
            scene.Draw(textures, renderer);
            return ConformanceHash.Sha256(renderer.ReadFrameTopLeftRgba());
        }
        finally
        {
            renderer.EndFrame();
        }
    }

    private static void AssertMatch(string expected, string actual, string view)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Map {view} framebuffer mismatch. Expected {expected}, observed {actual}.");
        }
    }

    private sealed class WorkerSource(IClientContentSource source, int contextThread) : IClientContentSource
    {
        private int _opens;
        internal int Opens => Volatile.Read(ref _opens);

        public bool TryOpenRead(string contentPath, ContentLookupMode mode, [NotNullWhen(true)] out Stream? stream)
        {
            ObserveOpen();
            return source.TryOpenRead(contentPath, mode, out stream);
        }

        public Stream OpenRequiredRead(string contentPath, ContentLookupMode mode)
        {
            ObserveOpen();
            return source.OpenRequiredRead(contentPath, mode);
        }

        private void ObserveOpen()
        {
            if (Environment.CurrentManagedThreadId == contextThread)
            {
                throw new InvalidOperationException("Streaming artwork opened on the OpenGL context thread.");
            }

            Interlocked.Increment(ref _opens);
        }
    }
}
