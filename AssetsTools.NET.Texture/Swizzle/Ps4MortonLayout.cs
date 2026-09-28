using System;
using System.IO;

namespace AssetsTools.NET.Texture;

/// <summary>
/// Explicit PS4-style 8x8 Morton microtiles of pixels or BC blocks. This is not an
/// implementation of every PS4 surface mode, macrotiling, or mip tail layout.
/// </summary>
public sealed class Ps4MortonLayout
{
    public int BlockWidth { get; }
    public int BlockHeight => BlockWidth;
    public int BlocksWide { get; }
    public int BlocksHigh { get; }
    public int BytesPerBlock { get; }
    public int LinearSize { get; }
    public int TiledSize { get; }
    private readonly int tilesWide;

    // Unity PS4 preprocessing expands RGB24 to four-byte RGBA storage.
    public static TextureFormat GetStorageFormat(TextureFormat format)
        => format == TextureFormat.RGB24 ? TextureFormat.RGBA32 : format;

    public Ps4MortonLayout(int width, int height, TextureFormat format)
    {
        format = GetStorageFormat(format);
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        BlockWidth = format == TextureFormat.Alpha8 || format == TextureFormat.R8
            || format == TextureFormat.RGBA32 || format == TextureFormat.ARGB32
            || format == TextureFormat.BGRA32 ? 1 : 4;
        BytesPerBlock = format switch
        {
            TextureFormat.Alpha8 or TextureFormat.R8 => 1,
            TextureFormat.RGBA32 or TextureFormat.ARGB32 or TextureFormat.BGRA32 => 4,
            TextureFormat.DXT1 or TextureFormat.BC4 => 8,
            TextureFormat.DXT3 or TextureFormat.DXT5 or TextureFormat.BC5
                or TextureFormat.BC6H or TextureFormat.BC7 => 16,
            _ => throw new NotSupportedException(
                $"PS4 Morton 8x8 supports Alpha8/R8, RGBA32/ARGB32/BGRA32 and BC1-7, not {format}.")
        };
        checked
        {
            BlocksWide = (width + BlockWidth - 1) / BlockWidth;
            BlocksHigh = (height + BlockWidth - 1) / BlockWidth;
            tilesWide = (BlocksWide + 7) / 8;
            LinearSize = BlocksWide * BlocksHigh * BytesPerBlock;
            TiledSize = tilesWide * ((BlocksHigh + 7) / 8) * 64 * BytesPerBlock;
        }
    }

    private int TiledOffset(int x, int y)
    {
        int morton = (x & 1) | ((y & 1) << 1)
            | ((x & 2) << 1) | ((y & 2) << 2)
            | ((x & 4) << 2) | ((y & 4) << 3);
        return (((y / 8) * tilesWide + x / 8) * 64 + morton) * BytesPerBlock;
    }

    public byte[] Deswizzle(ReadOnlySpan<byte> tiled)
    {
        if (tiled.Length < TiledSize)
            throw new InvalidDataException(
                $"Incomplete PS4 tiled data: need {TiledSize} bytes for the padded top level, got {tiled.Length}. " +
                "Export from the original asset; a cropped PNG cannot supply the missing blocks.");

        byte[] linear = new byte[LinearSize];
        for (int y = 0; y < BlocksHigh; y++)
        for (int x = 0; x < BlocksWide; x++)
            tiled.Slice(TiledOffset(x, y), BytesPerBlock).CopyTo(
                linear.AsSpan((y * BlocksWide + x) * BytesPerBlock, BytesPerBlock));
        return linear;
    }

    /// <summary>Preserve existing padding while replacing every visible block.</summary>
    public byte[] Swizzle(ReadOnlySpan<byte> linear, ReadOnlySpan<byte> originalTiled)
    {
        if (linear.Length != LinearSize || originalTiled.Length != TiledSize)
            throw new InvalidDataException("PS4 import requires one complete mip with the original padded size.");

        byte[] tiled = originalTiled.ToArray();
        for (int y = 0; y < BlocksHigh; y++)
        for (int x = 0; x < BlocksWide; x++)
            linear.Slice((y * BlocksWide + x) * BytesPerBlock, BytesPerBlock).CopyTo(
                tiled.AsSpan(TiledOffset(x, y), BytesPerBlock));
        return tiled;
    }
}
