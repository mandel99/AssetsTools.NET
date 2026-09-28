using System;
using System.IO;

namespace AssetsTools.NET.Texture;

/// <summary>PS4 Morton microtiles with independently padded mip levels. Packed tails and macrotiling are not implemented.</summary>
public sealed class Ps4Swizzle : ISwizzler
{
    private readonly int width, height;
    private readonly Ps4MortonLayout layout;
    private readonly byte[] original;
    private readonly TextureFormat format;

    public Ps4Swizzle(int width, int height, TextureFormat format, byte[] original = null)
    {
        this.width = width;
        this.height = height;
        this.format = format;
        layout = new Ps4MortonLayout(width, height, format);
        this.original = original;
    }

    public bool CanBeSwizzled() => true;

    public byte[] PreprocessDeswizzle(byte[] rawData, out int decodedWidth, out int decodedHeight)
    {
        decodedWidth = checked(layout.BlocksWide * layout.BlockWidth);
        decodedHeight = checked(layout.BlocksHigh * layout.BlockHeight);
        if (rawData == null) throw new InvalidDataException("PS4 texture data is missing.");
        return layout.Deswizzle(rawData);
    }

    public byte[] PostprocessDeswizzle(byte[] rawData)
    {
        if (rawData == null) return null;
        int stride = checked(layout.BlocksWide * layout.BlockWidth * 4);
        var result = new byte[checked(width * height * 4)];
        for (int y = 0; y < height; y++)
            Buffer.BlockCopy(rawData, y * stride, result, y * width * 4, width * 4);
        return result;
    }

    public byte[] ProcessSwizzle(byte[][] mips, out int[] mipOffsets)
    {
        var chain = new Ps4MipChain(width, height, format, mips.Length);
        mipOffsets = (int[])chain.Offsets.Clone();
        return chain.Swizzle(mips, original);
    }

    // PS4 preprocessing does not require a Switch-style platform blob.
    // The caller retains existing metadata rather than inventing a new blob.
    public byte[] MakePlatformBlob(int[] mipOffsets, uint completeImageSize) => Array.Empty<byte>();
}
