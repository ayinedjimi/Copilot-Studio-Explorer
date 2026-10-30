using System.Buffers.Binary;

namespace StudioDataverse;

public static class AgentIconPayload
{
    public static byte[]? Decode(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64)) return null;
        const string prefix = "data:image/png;base64,";
        var value = base64.Trim();
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) value = value[prefix.Length..];
        if (value.Length > 150_000) return null;
        byte[] bytes;
        try { bytes = Convert.FromBase64String(value); }
        catch (FormatException) { return null; }
        ReadOnlySpan<byte> png = [137, 80, 78, 71, 13, 10, 26, 10];
        if (bytes.Length < 33 || bytes.Length > 102_400 || !bytes.AsSpan(0, 8).SequenceEqual(png) ||
            BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8, 4)) != 13 ||
            !bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8)) return null;
        var width = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4));
        return width is > 0 and <= 2048 && height is > 0 and <= 2048 ? bytes : null;
    }
}
