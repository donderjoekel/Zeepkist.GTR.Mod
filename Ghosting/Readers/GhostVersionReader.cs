using System.IO;

namespace TNRD.Zeepkist.GTR.Ghosting.Readers;

internal static class GhostVersionReader
{
    public static int ReadBinary(byte[] buffer)
    {
        if (buffer == null || buffer.Length < sizeof(int))
            throw new InvalidDataException("Ghost data is truncated.");

        using BinaryReader reader = new(new MemoryStream(buffer, false));
        return reader.ReadInt32();
    }

    public static int ReadProtobuf(byte[] buffer)
    {
        if (buffer == null || buffer.Length == 0)
            throw new InvalidDataException("Ghost data is truncated.");

        int offset = 0;
        ulong key = ReadVarint(buffer, ref offset);
        int fieldNumber = (int)(key >> 3);
        int wireType = (int)(key & 7);
        if (fieldNumber != 1 || wireType != 0)
            throw new InvalidDataException("Ghost protobuf payload does not start with a version field.");

        ulong version = ReadVarint(buffer, ref offset);
        if (version > int.MaxValue)
            throw new InvalidDataException("Ghost version is invalid.");

        return (int)version;
    }

    private static ulong ReadVarint(byte[] buffer, ref int offset)
    {
        ulong value = 0;
        for (int shift = 0; shift < 64; shift += 7)
        {
            if (offset >= buffer.Length)
                throw new InvalidDataException("Ghost protobuf varint is truncated.");

            byte current = buffer[offset++];
            value |= (ulong)(current & 0x7f) << shift;
            if ((current & 0x80) == 0)
                return value;
        }

        throw new InvalidDataException("Ghost protobuf varint is invalid.");
    }
}
