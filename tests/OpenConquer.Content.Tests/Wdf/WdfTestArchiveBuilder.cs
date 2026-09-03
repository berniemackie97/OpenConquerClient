using System.Buffers.Binary;
using OpenConquer.Content.Wdf;

namespace OpenConquer.Content.Tests.Wdf;

/// <summary>
/// Constructs minimal synthetic WDF archives for format and package-boundary tests.
/// </summary>
internal sealed class WdfTestArchiveBuilder
{
    private readonly List<TestEntry> _entries = [];

    public WdfTestArchiveBuilder AddEntry(string contentPath, ReadOnlySpan<byte> payload)
    {
        return AddEntry(WdfPathHash.Compute(contentPath), payload);
    }

    public WdfTestArchiveBuilder AddEntry(uint id, ReadOnlySpan<byte> payload, uint reserved = 0)
    {
        _entries.Add(new TestEntry(id, payload.ToArray(), reserved));

        return this;
    }

    public byte[] Build(bool sortEntries = true)
    {
        TestEntry[] entries = [.. _entries];

        if (sortEntries)
        {
            Array.Sort(entries, static (left, right) => left.Id.CompareTo(right.Id));
        }

        int payloadLength = 0;

        foreach (TestEntry entry in entries)
        {
            payloadLength = checked(payloadLength + entry.Payload.Length);
        }

        int tableOffset = checked(WdfArchive.HeaderLength + payloadLength);
        int archiveLength = checked(tableOffset + entries.Length * WdfArchive.EntryLength);

        byte[] archive = new byte[archiveLength];

        BinaryPrimitives.WriteUInt32LittleEndian(archive, WdfArchive.Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(archive.AsSpan(4), checked((uint)entries.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(archive.AsSpan(8), checked((uint)tableOffset));

        int payloadOffset = WdfArchive.HeaderLength;
        int entryOffset = tableOffset;

        foreach (TestEntry entry in entries)
        {
            entry.Payload.CopyTo(archive.AsSpan(payloadOffset));

            BinaryPrimitives.WriteUInt32LittleEndian(archive.AsSpan(entryOffset), entry.Id);
            BinaryPrimitives.WriteUInt32LittleEndian(archive.AsSpan(entryOffset + 4), checked((uint)payloadOffset));
            BinaryPrimitives.WriteUInt32LittleEndian(archive.AsSpan(entryOffset + 8), checked((uint)entry.Payload.Length));
            BinaryPrimitives.WriteUInt32LittleEndian(archive.AsSpan(entryOffset + 12), entry.Reserved);

            payloadOffset = checked(payloadOffset + entry.Payload.Length);
            entryOffset = checked(entryOffset + WdfArchive.EntryLength);
        }

        return archive;
    }

    public static byte[] CreateSingleEntry(string contentPath, ReadOnlySpan<byte> payload)
    {
        return new WdfTestArchiveBuilder().AddEntry(contentPath, payload).Build();
    }

    private readonly record struct TestEntry(uint Id, byte[] Payload, uint Reserved);
}
