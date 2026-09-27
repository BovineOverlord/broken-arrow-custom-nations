using System.Buffers.Binary;

namespace BACustomNations;

public static class FlagFiles
{
    public const int MaxBytes = 8 * 1024 * 1024;
    public const int MaxDimension = 2048;

    public static string Resolve(string packsFolder, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) throw new InvalidDataException("Flag path is empty.");
        var parts = relativePath.Replace('\\', '/').Split('/');
        if (Path.IsPathRooted(relativePath) || relativePath.Contains(':') ||
            parts.Any(p => p is ".." or "." or ""))
            throw new InvalidDataException("Flag must be a relative path inside the packs folder.");
        if (!relativePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Flag must be a PNG file.");
        var root = Path.GetFullPath(packsFolder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(root, Path.Combine(parts)));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Flag path leaves the packs folder.");
        return path;
    }

    public static byte[] ReadPng(string path)
    {
        if (new FileInfo(path).Length > MaxBytes) throw new InvalidDataException("Flag exceeds 8 MiB.");
        var bytes = File.ReadAllBytes(path);
        InspectPng(bytes);
        return bytes;
    }

    public static (int Width, int Height) InspectPng(byte[] bytes)
    {
        byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
        if (bytes.Length < 33 || bytes.Length > MaxBytes || !bytes.AsSpan(0, 8).SequenceEqual(signature) ||
            BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(8, 4)) != 13 ||
            !bytes.AsSpan(12, 4).SequenceEqual(new byte[] { 73, 72, 68, 82 }))
            throw new InvalidDataException("Flag is not a PNG with a valid IHDR header.");
        int width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
        int height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
        if (width < 1 || height < 1 || width > MaxDimension || height > MaxDimension)
            throw new InvalidDataException("Flag dimensions must be between 1 and 2048 pixels.");
        return (width, height);
    }
}
