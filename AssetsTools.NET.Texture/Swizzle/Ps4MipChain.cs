using System;
using System.IO;

namespace AssetsTools.NET.Texture;

/// <summary>Consecutive, independently padded Morton mip levels (no packed mip tail).</summary>
public sealed class Ps4MipChain
{
    public Ps4MortonLayout[] Levels { get; }
    public int[] Offsets { get; }
    public int TiledSize { get; }
    public int LinearSize { get; }

    public Ps4MipChain(int width, int height, TextureFormat format, int mipCount)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        int max = 1;
        for (int size = Math.Max(width, height); size > 1; size >>= 1) max++;
        if (mipCount < 1 || mipCount > max) throw new ArgumentOutOfRangeException(nameof(mipCount));
        Levels = new Ps4MortonLayout[mipCount];
        Offsets = new int[mipCount];
        for (int i = 0; i < mipCount; i++)
        {
            var level = Levels[i] = new Ps4MortonLayout(Math.Max(1, width >> i), Math.Max(1, height >> i), format);
            Offsets[i] = TiledSize;
            TiledSize = checked(TiledSize + level.TiledSize);
            LinearSize = checked(LinearSize + level.LinearSize);
        }
    }

    public byte[][] Deswizzle(byte[] tiled)
    {
        if (tiled == null || tiled.Length != TiledSize)
            throw new InvalidDataException("PS4 mip chain size does not match the complete padded layout.");
        var result = new byte[Levels.Length][];
        for (int i = 0; i < result.Length; i++)
            result[i] = Levels[i].Deswizzle(tiled.AsSpan(Offsets[i], Levels[i].TiledSize));
        return result;
    }

    public byte[][] SplitLinear(byte[] linear)
    {
        if (linear == null || linear.Length != LinearSize)
            throw new InvalidDataException("Linear mip chain size does not match the declared levels.");
        var result = new byte[Levels.Length][];
        int offset = 0;
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = linear.AsSpan(offset, Levels[i].LinearSize).ToArray();
            offset += Levels[i].LinearSize;
        }
        return result;
    }

    public byte[] Swizzle(byte[][] mips, byte[] original = null)
    {
        if (mips == null || mips.Length != Levels.Length)
            throw new InvalidDataException("Incorrect PS4 mip count.");
        if (original != null && original.Length != TiledSize)
            throw new InvalidDataException("Original PS4 padding buffer has the wrong size.");
        var result = original == null ? new byte[TiledSize] : (byte[])original.Clone();
        for (int i = 0; i < mips.Length; i++)
        {
            var level = Levels[i].Swizzle(mips[i], result.AsSpan(Offsets[i], Levels[i].TiledSize));
            Buffer.BlockCopy(level, 0, result, Offsets[i], level.Length);
        }
        return result;
    }
}
