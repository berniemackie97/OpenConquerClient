using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace OpenConquer.Launcher.Settings;

internal static partial class SettingsFile
{
    private const uint DeleteAccess = 0x0001_0000;
    private const uint OpenReparsePoint = 0x0020_0000;
    private const int FileRenameInfoEx = 22;
    private const uint ReplaceIfExists = 0x1;
    private const uint PosixSemantics = 0x2;

    internal static FileStream OpenRead(string path)
    {
        FileAttributes attributes = File.GetAttributes(path);
        if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory | FileAttributes.Device)) != 0)
        {
            throw new IOException("Unsupported settings file.");
        }

        if (OperatingSystem.IsWindows())
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        }

        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS() || RuntimeInformation.ProcessArchitecture is not (Architecture.X64 or Architecture.Arm64))
        {
            throw new PlatformNotSupportedException();
        }

        int flags = OperatingSystem.IsMacOS() ? 0x4 | 0x100 | 0x1000000 : 0x800 | 0x80000 | 0x20000;
        int descriptor = Open(path, flags);
        if (descriptor < 0)
        {
            int error = Marshal.GetLastPInvokeError();
            if (error == 2)
            {
                throw new FileNotFoundException();
            }

            if (error == 13)
            {
                throw new UnauthorizedAccessException("Cannot read settings.");
            }

            throw new IOException("Cannot open settings.", new Win32Exception(error));
        }

        SafeFileHandle handle = new(descriptor, ownsHandle: true);
        try
        {
            return new FileStream(handle, FileAccess.Read);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Writes a private file beside the destination, flushes it, then atomically replaces the destination.
    /// Cancellation before the replacement leaves the destination untouched and removes the staged file.
    /// </summary>
    internal static void Replace(string directory, string fileName, ReadOnlySpan<byte> contents, bool destinationExists, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Directory.CreateDirectory(directory);
        string? temporary = Path.Combine(directory, "." + Path.GetFileNameWithoutExtension(fileName) + "-" + Guid.NewGuid().ToString("N") + ".tmp");

        try
        {
            FileStreamOptions options = new()
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None
            };

            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            using (FileStream stream = new(temporary, options))
            {
                stream.Write(contents);
                stream.Flush(flushToDisk: true);
            }

            token.ThrowIfCancellationRequested();
            Commit(temporary, Path.Combine(directory, fileName), destinationExists);
            temporary = null;
        }
        finally
        {
            // Cleanup failures remain observable instead of silently accumulating staged files.
            if (temporary is not null)
            {
                File.Delete(temporary);
            }
        }
    }

    /// <summary>
    /// Renames the staged file over the destination in one step. Readers open with <see cref="FileShare.Delete"/>, so a
    /// reader that holds the previous document keeps reading it and every later open sees the replacement.
    /// </summary>
    /// <remarks>
    /// <c>ReplaceFile</c> is not used: a reader opening the destination while it runs can find the file missing or
    /// locked by a sharing violation.
    /// </remarks>
    internal static void Commit(string temporaryPath, string destinationPath, bool destinationExists)
    {
        try
        {
            File.Move(temporaryPath, destinationPath, overwrite: destinationExists);
        }
        catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows() && destinationExists)
        {
            // MoveFileEx will not replace a file that has open handles. POSIX rename semantics replace it and leave those
            // handles on the previous document; when a handle lacks delete sharing or the volume lacks those semantics,
            // the original failure stands.
            if (!ReplaceOpenDestination(temporaryPath, destinationPath))
            {
                throw;
            }
        }
    }

    private static unsafe bool ReplaceOpenDestination(string temporaryPath, string destinationPath)
    {
        using SafeFileHandle staged = CreateFile(temporaryPath, DeleteAccess, FileShare.ReadWrite | FileShare.Delete, 0, FileMode.Open, OpenReparsePoint, 0);
        if (staged.IsInvalid)
        {
            return false;
        }

        // The name continues past the end of the structure, whose size leaves room for the terminating null.
        uint length = (uint)(sizeof(RenameInformation) + destinationPath.Length * sizeof(char));
        RenameInformation* rename = (RenameInformation*)NativeMemory.AllocZeroed(length);
        try
        {
            rename->Flags = ReplaceIfExists | PosixSemantics;
            rename->FileNameLength = (uint)(destinationPath.Length * sizeof(char));
            destinationPath.CopyTo(new Span<char>(&rename->FileName, destinationPath.Length));
            return SetFileInformationByHandle(staged, FileRenameInfoEx, rename, length);
        }
        finally
        {
            NativeMemory.Free(rename);
        }
    }

    /// <summary><c>FILE_RENAME_INFO</c> as read by <c>FileRenameInfoEx</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct RenameInformation
    {
        public uint Flags;
        public nint RootDirectory;
        public uint FileNameLength;
        public char FileName;
    }

    [LibraryImport("libc", EntryPoint = "open", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int Open(string path, int flags);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFile(string path, uint access, FileShare share, nint security, FileMode mode, uint flags, nint template);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool SetFileInformationByHandle(SafeFileHandle file, int informationClass, RenameInformation* information, uint length);
}
