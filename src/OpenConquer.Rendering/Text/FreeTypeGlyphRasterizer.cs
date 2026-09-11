using System.Runtime.InteropServices;
using System.Text;
using FreeTypeSharp;
using Microsoft.Win32.SafeHandles;
using static FreeTypeSharp.FT;
using static FreeTypeSharp.FT_LOAD;
using static FreeTypeSharp.FT_Pixel_Mode_;
using static FreeTypeSharp.FT_Render_Mode_;

namespace OpenConquer.Rendering.Text;

/// <summary>
/// Rasterizes glyphs from one resolved font face using FreeType.
/// </summary>
internal sealed unsafe class FreeTypeGlyphRasterizer : IGlyphRasterizer
{
    private readonly FreeTypeLibrary _library;
    private readonly SafeFreeTypeFaceHandle _face;

    public FreeTypeGlyphRasterizer(FreeTypeLibrary library, string fontFilePath, int pixelHeight)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentException.ThrowIfNullOrWhiteSpace(fontFilePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelHeight);

        _library = library;
        _face = library.UseHandle(libraryHandle => CreateFace(libraryHandle, fontFilePath, pixelHeight));
    }

    public bool TryRasterizeGlyph(Rune character, out RasterizedGlyph? glyph)
    {
        bool addedReference = false;

        try
        {
            _face.DangerousAddRef(ref addedReference);

            ObjectDisposedException.ThrowIf(_face.IsInvalid || _face.IsClosed, this);

            FT_FaceRec_* face = (FT_FaceRec_*)_face.DangerousGetHandle();
            uint glyphIndex = FT_Get_Char_Index(face, checked((uint)character.Value));

            if (glyphIndex == 0)
            {
                glyph = null;
                return false;
            }

            FT_Error error = FT_Load_Glyph(face, glyphIndex, FT_LOAD_DEFAULT);

            if (error != FT_Error.FT_Err_Ok)
            {
                glyph = null;
                return false;
            }

            error = FT_Render_Glyph(face->glyph, FT_RENDER_MODE_NORMAL);

            if (error != FT_Error.FT_Err_Ok)
            {
                glyph = null;
                return false;
            }

            FT_GlyphSlotRec_* glyphSlot = face->glyph;
            FT_Bitmap bitmap = glyphSlot->bitmap;

            if (bitmap.pixel_mode != FT_PIXEL_MODE_GRAY)
            {
                glyph = null;
                return false;
            }

            int widthPixels = checked((int)bitmap.width);
            int heightPixels = checked((int)bitmap.rows);
            byte[] coverage = CopyCoverage(bitmap, widthPixels, heightPixels);

            glyph = new RasterizedGlyph(
                widthPixels,
                heightPixels,
                glyphSlot->bitmap_left,
                glyphSlot->bitmap_top,
                Convert26Dot6ToPixels(glyphSlot->advance.x),
                coverage);

            return true;
        }
        finally
        {
            if (addedReference)
            {
                _face.DangerousRelease();
            }
        }
    }

    public void Dispose()
    {
        _face.Dispose();
    }

    private static SafeFreeTypeFaceHandle CreateFace(FT_LibraryRec_* library, string fontFilePath, int pixelHeight)
    {
        nint path = Marshal.StringToCoTaskMemUTF8(fontFilePath);

        try
        {
            FT_FaceRec_* face = null;
            FT_Error error = FT_New_Face(library, (byte*)path, 0, &face);

            if (error != FT_Error.FT_Err_Ok || face is null)
            {
                throw new InvalidOperationException(
                    $"FreeType could not open font '{fontFilePath}' (error {(int)error}).");
            }

            SafeFreeTypeFaceHandle handle = new(face);

            try
            {
                error = FT_Set_Pixel_Sizes(face, 0, checked((uint)pixelHeight));

                if (error != FT_Error.FT_Err_Ok)
                {
                    throw new InvalidOperationException(
                        $"FreeType could not configure font '{fontFilePath}' at {pixelHeight} pixels (error {(int)error}).");
                }

                return handle;
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(path);
        }
    }

    private static byte[] CopyCoverage(FT_Bitmap bitmap, int widthPixels, int heightPixels)
    {
        int coverageLength = checked(widthPixels * heightPixels);

        if (coverageLength == 0)
        {
            return [];
        }

        if (bitmap.buffer is null)
        {
            throw new InvalidOperationException("FreeType returned a non-empty glyph bitmap with no pixel buffer.");
        }

        int pitch = bitmap.pitch;

        if (pitch == 0)
        {
            throw new InvalidOperationException("FreeType returned a non-empty glyph bitmap with zero pitch.");
        }

        int absolutePitch = checked(Math.Abs(pitch));

        if (absolutePitch < widthPixels)
        {
            throw new InvalidOperationException(
                $"FreeType glyph bitmap pitch {pitch} is smaller than its {widthPixels}-pixel width.");
        }

        byte[] coverage = new byte[coverageLength];

        for (int row = 0; row < heightPixels; row++)
        {
            int sourceRow = pitch > 0 ? row : heightPixels - 1 - row;
            byte* source = bitmap.buffer + checked(sourceRow * absolutePitch);

            new ReadOnlySpan<byte>(source, widthPixels).CopyTo(
                coverage.AsSpan(checked(row * widthPixels), widthPixels));
        }

        return coverage;
    }

    private static int Convert26Dot6ToPixels(nint value)
    {
        long fixedPointValue = value;

        long rounded = fixedPointValue >= 0
            ? (fixedPointValue + 32) >> 6
            : -(((-fixedPointValue) + 32) >> 6);

        return checked((int)rounded);
    }

    private sealed class SafeFreeTypeFaceHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeFreeTypeFaceHandle(FT_FaceRec_* face)
            : base(ownsHandle: true)
        {
            SetHandle((nint)face);
        }

        protected override bool ReleaseHandle()
        {
            return FT_Done_Face((FT_FaceRec_*)handle) == FT_Error.FT_Err_Ok;
        }
    }
}
