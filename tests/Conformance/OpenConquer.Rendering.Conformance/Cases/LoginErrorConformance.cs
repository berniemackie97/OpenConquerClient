using System.IO.Compression;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Client.UI.Login.Status;
using OpenConquer.Content.Login;
using OpenConquer.Platform.Geometry;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Rendering.Conformance.Cases;

internal static class LoginErrorConformance
{
    internal static void Run(OpenGLGraphicsDevice device, PixelSize framebufferSize, string colorFormat)
    {
        LogicalRenderSize size = new(1024, 768);
        LoginStageLayout layout = LoginStageLayout.Create(size);
        using OpenGLRenderer renderer = device.CreateRenderer(size, framebufferSize.Width, framebufferSize.Height);
        // Recreate all owned textures/font resources, as happens after losing a graphics context.
        for (int generation = 0; generation < 2; generation++)
        {
            using LoginErrorPresentation presentation = new(device, layout);
            LoginErrorDialog dialog = new(layout);
            for (int frame = 1; frame <= 10; frame++)
            {
                if (frame == 2)
                {
                    dialog.Show(string.Empty);
                }
                else if (frame == 7)
                {
                    dialog.PointerPressed(true, 500, 428);
                    dialog.PointerReleased(true, 500, 428);
                }
                else if (frame > 2)
                {
                    dialog.Advance(1);
                }

                renderer.BeginFrame();
                renderer.DrawSolidRectangle(0, 0, 1024, 768, SpriteColor.White);
                presentation.Draw(renderer, dialog);
                byte[] actual = renderer.ReadFrameTopLeftRgba();
                renderer.EndFrame();

                Verify($"frame{frame}", actual, colorFormat);
            }

            foreach (bool quit in new[] { false, true })
            {
                foreach (bool loading in quit ? new[] { false, true } : new[] { false })
                {
                    foreach (LoginDialogButton button in Enum.GetValues<LoginDialogButton>())
                    {
                        if (!quit && button == LoginDialogButton.Cancel)
                        {
                            continue;
                        }
                        foreach (LoginButtonState state in Enum.GetValues<LoginButtonState>())
                        {
                            LoginErrorDialog variant = new(layout, quit, loading);
                            variant.Show(string.Empty);
                            variant.Advance(4);
                            LoginRect bounds = variant.Bounds(button);
                            if (state == LoginButtonState.Over)
                            {
                                variant.PointerMoved(true, bounds.X + 10, bounds.Y + 10);
                            }
                            else if (state == LoginButtonState.Down)
                            {
                                variant.PointerPressed(true, bounds.X + 10, bounds.Y + 10);
                            }
                            renderer.BeginFrame();
                            renderer.DrawSolidRectangle(0, 0, 1024, 768, SpriteColor.White);
                            presentation.Draw(renderer, variant);
                            byte[] actual = renderer.ReadFrameTopLeftRgba();
                            renderer.EndFrame();
                            string fixture = state == LoginButtonState.Up ? quit ? "QuitUp" : "frame6"
                                : $"{(quit ? "Quit" : "Error")}{button}{state}";
                            Verify(loading ? "Loading" + fixture : fixture, actual, colorFormat);
                        }
                    }
                }
            }

            // Dynamic text is not part of the independent SWF artwork fixtures.
            // Exercise glyph upload, long wrapped lines, newline clipping and replacement in the real GL path.
            string[] samples = ["", "Please Enter Your User Name.", "Failed to login: Invalid Account ID or Password(1)",
                "Your password must be 10-14 characters. Please retrieve your password on the website.",
                "The server is full. Please try again later.", new string('A', 100), "Line one\r\nLine two\rLine three\nLine four", "<b>%s%d</b>"];
            for (int sample = 0; sample < samples.Length; sample++)
            {
                dialog.Show(samples[sample]);
                dialog.Advance(4);
                renderer.BeginFrame();
                renderer.DrawSolidRectangle(0, 0, 1024, 768, SpriteColor.White);
                presentation.Draw(renderer, dialog);
                if (Environment.GetEnvironmentVariable("OPENCONQUER_CONFORMANCE_CAPTURE_DIRECTORY") is { Length: > 0 } captureDirectory)
                {
                    Directory.CreateDirectory(captureDirectory);
                    File.WriteAllBytes(Path.Combine(captureDirectory, $"text-{sample}.rgba"), renderer.ReadFrameTopLeftRgba());
                }
                renderer.EndFrame();
            }

            presentation.Dispose();
        }

        Console.WriteLine("Login error: ten timeline frames and all button states for error, idle quit and loading quit, text upload/clipping and resource recreation passed.");
    }
    private static void Verify(string fixture, byte[] actual, string colorFormat)
    {
        using Stream resource = typeof(LoginErrorConformance).Assembly.GetManifestResourceStream(
            $"OpenConquer.Rendering.Conformance.Fixtures.LoginError.{fixture}.rgba.zlib")!;
        using ZLibStream decompressor = new(resource, CompressionMode.Decompress);
        byte[] expected = new byte[1024 * 768 * 4];
        decompressor.ReadExactly(expected);
        for (int index = 0; index < expected.Length; index++)
        {
            int channel = index % 4;
            int alpha = expected[index - channel + 3];
            int value = expected[index];
            int bits = channel == 1 && colorFormat == "RGB565" ? 6 : 5;
            int maximum = (1 << bits) - 1;
            double target = channel == 3 ? 255 : (value * alpha + 255 * (255 - alpha)) / 255.0;
            // Half a target quantization step plus half an output byte permits
            // hardware rounding at a boundary, without hiding a whole step.
            double tolerance = channel == 3 ? 0 : (255.0 / maximum + 1) / 2;
            if (Math.Abs(target - actual[index]) > tolerance)
            {
                throw new InvalidDataException($"{fixture} mismatch at byte {index}: expected {target}, actual {actual[index]}.");
            }
        }
    }

}
