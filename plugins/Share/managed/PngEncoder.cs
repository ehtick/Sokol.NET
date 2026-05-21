// Pure-C# PNG encoder — no external dependencies, no System.IO file access.
// Uses deflate level-0 (uncompressed) blocks; output is always valid PNG.
// For an RGBA 480×270 image the output is ~520 KB.
public static class PngEncoder
{
    // Encode rgba byte[] (W*H*4, row-major, top-to-bottom) as PNG bytes.
    public static byte[] Encode(byte[] rgba, int w, int h)
    {
        int stride  = w * 4;
        int rowSize = stride + 1;          // +1 for filter byte
        int rawLen  = h * rowSize;

        // Build filtered scan lines (filter type 0 = None for every row)
        byte[] raw = new byte[rawLen];
        for (int y = 0; y < h; y++)
        {
            raw[y * rowSize] = 0;          // filter = None
            System.Array.Copy(rgba, y * stride, raw, y * rowSize + 1, stride);
        }

        // Zlib stream: header + uncompressed deflate blocks + Adler-32
        byte[] zlib = ZlibStore(raw);

        // Assemble PNG
        int pngLen = 8                     // signature
                   + 25                    // IHDR (4 len + 4 type + 13 data + 4 crc)
                   + 12 + zlib.Length      // IDAT (4 len + 4 type + data + 4 crc)
                   + 12;                   // IEND
        byte[] png = new byte[pngLen];
        int pos = 0;

        // Signature
        png[pos++]=137; png[pos++]=80; png[pos++]=78; png[pos++]=71;
        png[pos++]=13;  png[pos++]=10; png[pos++]=26; png[pos++]=10;

        // IHDR
        byte[] ihdr = new byte[13];
        WriteU32BE(ihdr, 0, (uint)w);
        WriteU32BE(ihdr, 4, (uint)h);
        ihdr[8]  = 8;   // bit depth
        ihdr[9]  = 6;   // colour type: RGBA
        ihdr[10] = 0;   // compression
        ihdr[11] = 0;   // filter
        ihdr[12] = 0;   // interlace
        WriteChunk(png, ref pos, IhdrType, ihdr);

        // IDAT
        WriteChunk(png, ref pos, IdatType, zlib);

        // IEND
        WriteChunk(png, ref pos, IendType, System.Array.Empty<byte>());

        return png;
    }

    // ── Zlib store (level 0 — uncompressed deflate + zlib framing) ─────────────

    static byte[] ZlibStore(byte[] data)
    {
        const int MaxBlock = 65535;
        int blocks  = (data.Length + MaxBlock - 1) / MaxBlock;
        if (blocks == 0) blocks = 1;
        int outLen  = 2 + blocks * 5 + data.Length + 4;
        byte[] out2 = new byte[outLen];
        int p = 0;

        // Zlib header: CM=8 (deflate), CINFO=7 (window=32K), FCHECK makes it % 31 == 0
        out2[p++] = 0x78;
        out2[p++] = 0x01;

        int remaining = data.Length;
        int src       = 0;
        for (int b = 0; b < blocks; b++)
        {
            int blockLen  = System.Math.Min(remaining, MaxBlock);
            bool isFinal  = (b == blocks - 1);
            out2[p++]     = isFinal ? (byte)0x01 : (byte)0x00;  // BFINAL|BTYPE=00
            out2[p++]     = (byte)(blockLen & 0xFF);
            out2[p++]     = (byte)(blockLen >> 8);
            out2[p++]     = (byte)(~blockLen & 0xFF);
            out2[p++]     = (byte)((~blockLen >> 8) & 0xFF);
            System.Array.Copy(data, src, out2, p, blockLen);
            p         += blockLen;
            src       += blockLen;
            remaining -= blockLen;
        }

        // Adler-32 (big-endian)
        uint adler = Adler32(data);
        out2[p++] = (byte)(adler >> 24);
        out2[p++] = (byte)(adler >> 16);
        out2[p++] = (byte)(adler >>  8);
        out2[p++] = (byte) adler;

        return out2;
    }

    // ── Chunk writing ───────────────────────────────────────────────────────────

    static readonly byte[] IhdrType = { 73, 72, 68, 82 };  // IHDR
    static readonly byte[] IdatType = { 73, 68, 65, 84 };  // IDAT
    static readonly byte[] IendType = { 73, 69, 78, 68 };  // IEND

    static void WriteChunk(byte[] dst, ref int pos, byte[] type, byte[] data)
    {
        WriteU32BE(dst, pos, (uint)data.Length); pos += 4;
        type.CopyTo(dst, pos);                   pos += 4;
        data.CopyTo(dst, pos);                   pos += data.Length;
        uint crc = Crc32Init();
        crc = Crc32Update(crc, type);
        crc = Crc32Update(crc, data);
        WriteU32BE(dst, pos, Crc32Final(crc));   pos += 4;
    }

    static void WriteU32BE(byte[] dst, int offset, uint v)
    {
        dst[offset]     = (byte)(v >> 24);
        dst[offset + 1] = (byte)(v >> 16);
        dst[offset + 2] = (byte)(v >>  8);
        dst[offset + 3] = (byte) v;
    }

    // ── Adler-32 ────────────────────────────────────────────────────────────────

    static uint Adler32(byte[] data)
    {
        uint s1 = 1, s2 = 0;
        foreach (byte b in data)
        {
            s1 = (s1 + b) % 65521;
            s2 = (s2 + s1) % 65521;
        }
        return (s2 << 16) | s1;
    }

    // ── CRC-32 (ISO 3309 / PNG) ─────────────────────────────────────────────────

    static uint Crc32Init() => 0xFFFFFFFF;
    static uint Crc32Final(uint crc) => crc ^ 0xFFFFFFFF;

    static uint Crc32Update(uint crc, byte[] data)
    {
        foreach (byte b in data)
            crc = (crc >> 8) ^ s_crc32[(crc ^ b) & 0xFF];
        return crc;
    }

    // Standard CRC-32 table (polynomial 0xEDB88320, reflected)
    static readonly uint[] s_crc32 = BuildCrc32Table();

    static uint[] BuildCrc32Table()
    {
        var t = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[n] = c;
        }
        return t;
    }
}
