using System;
using System.IO;

namespace AssetsTools.NET.Texture;

/// <summary>
/// PS5 AMD Gen5/GFX10 standard thin-2D swizzler.
/// First implementation intentionally supports one mip only and standard
/// non-XOR modes 256B_S/4KB_S/64KB_S.
/// </summary>
public sealed class Ps5Swizzle : ISwizzler
{
    private readonly int width;
    private readonly int height;
    private readonly TextureFormat format;
    private readonly byte[] original;
    private readonly int storedSizeHint;

    public Ps5Swizzle(int width, int height, TextureFormat format, byte[] original = null, int storedSizeHint = 0)
    {
        this.width = width;
        this.height = height;
        this.format = format;
        this.original = original;
        this.storedSizeHint = storedSizeHint > 0 ? storedSizeHint : original?.Length ?? 0;
    }

    public bool CanBeSwizzled() => Ps5GfxLayout.IsSupportedFormat(format);

    public byte[] PreprocessDeswizzle(byte[] rawData, out int decodedWidth, out int decodedHeight)
    {
        if (rawData == null || rawData.Length == 0)
            throw new InvalidDataException("PS5 texture data is missing.");

        int tileMode = Ps5GfxLayout.InferTileMode(width, height, format, rawData.Length);
        var layout = new Ps5GfxLayout(width, height, format, tileMode);

        decodedWidth = checked(layout.ElementsWide * layout.PixelBlockSize);
        decodedHeight = checked(layout.ElementsHigh * layout.PixelBlockSize);
        return layout.Deswizzle(rawData);
    }

    public byte[] PostprocessDeswizzle(byte[] rawData)
    {
        if (rawData == null)
            return null;

        if (!Ps5GfxLayout.TryGetElementInfo(format, out int pixelBlockSize, out _))
            throw new NotSupportedException($"Unsupported PS5 texture format: {format}.");

        int decodedWidth = checked(((width + pixelBlockSize - 1) / pixelBlockSize) * pixelBlockSize);
        int decodedHeight = checked(((height + pixelBlockSize - 1) / pixelBlockSize) * pixelBlockSize);

        int srcStride = checked(decodedWidth * 4);
        int dstStride = checked(width * 4);
        if (rawData.Length < checked(srcStride * decodedHeight))
            throw new InvalidDataException("Decoded PS5 texture buffer is smaller than expected.");

        if (decodedWidth == width && decodedHeight == height)
            return rawData;

        byte[] result = new byte[checked(width * height * 4)];
        for (int y = 0; y < height; y++)
            Buffer.BlockCopy(rawData, y * srcStride, result, y * dstStride, dstStride);
        return result;
    }

    public byte[] ProcessSwizzle(byte[][] mips, out int[] mipOffsets)
    {
        if (mips == null || mips.Length != 1 || mips[0] == null)
            throw new NotSupportedException("PS5 Gen5 import currently supports exactly one mip level.");

        int sizeHint = original != null && original.Length > 0 ? original.Length : storedSizeHint;
        int tileMode = Ps5GfxLayout.InferTileMode(width, height, format, sizeHint);

        var layout = new Ps5GfxLayout(width, height, format, tileMode);
        if (mips[0].Length != layout.LinearSize)
            throw new InvalidDataException(
                $"Encoded PS5 mip size mismatch: expected {layout.LinearSize}, got {mips[0].Length}.");

        mipOffsets = new[] { 0 };
        if (original != null && original.Length == layout.TiledSize)
            return layout.Swizzle(mips[0], original);
        return layout.Swizzle(mips[0]);
    }

    // PS5 preprocessing does not use Switch-style platform metadata here.
    public byte[] MakePlatformBlob(int[] mipOffsets, uint completeImageSize) => Array.Empty<byte>();
}
