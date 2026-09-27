using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Blueverse.CoastalOperations.Application;
using Microsoft.AspNetCore.Http;

namespace Blueverse.CoastalOperations.Tests;

public sealed class AssessmentEvidenceSanitizerBoundaryTests
{
    [Theory(DisplayName = "COASTAL-EVIDENCE-017 unsupported PNG dimensions, color modes and header methods are rejected")]
    [Trait("TestId", "COASTAL-EVIDENCE-017")]
    [InlineData("zero-width")]
    [InlineData("width-limit")]
    [InlineData("pixel-limit")]
    [InlineData("bit-depth")]
    [InlineData("indexed-color")]
    [InlineData("compression")]
    [InlineData("filter-method")]
    [InlineData("interlace")]
    public async Task UnsupportedPngHeaderIsRejected(string scenario)
    {
        var png = BuildPng([0, 10, 20, 30, 255]);
        var header = png.AsSpan(16, 13).ToArray();
        switch (scenario)
        {
            case "zero-width": BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), 0); break;
            case "width-limit": BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), 4097); break;
            case "pixel-limit":
                BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), 4096);
                BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4, 4), 4096);
                break;
            case "bit-depth": header[8] = 16; break;
            case "indexed-color": header[9] = 3; break;
            case "compression": header[10] = 1; break;
            case "filter-method": header[11] = 1; break;
            case "interlace": header[12] = 1; break;
        }
        var invalid = ReplaceChunkData(png, "IHDR", header);

        var exception = await Assert.ThrowsAsync<CoastalOperationsException>(() => Sanitize(invalid));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, exception.StatusCode);
        Assert.Equal("evidence_image_invalid", exception.Code);
    }

    [Theory(DisplayName = "COASTAL-EVIDENCE-018 invalid PNG scanlines and compressed streams are rejected")]
    [Trait("TestId", "COASTAL-EVIDENCE-018")]
    [InlineData("invalid-filter")]
    [InlineData("short-scanline")]
    [InlineData("extra-scanline")]
    [InlineData("invalid-zlib")]
    public async Task InvalidImageDataIsRejected(string scenario)
    {
        var png = scenario switch
        {
            "invalid-filter" => BuildPng([5, 10, 20, 30, 255]),
            "short-scanline" => BuildPng([0, 10]),
            "extra-scanline" => BuildPng([0, 10, 20, 30, 255, 0]),
            "invalid-zlib" => ReplaceChunkData(BuildPng([0, 10, 20, 30, 255]), "IDAT", [1, 2, 3, 4]),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

        var exception = await Assert.ThrowsAsync<CoastalOperationsException>(() => Sanitize(png));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, exception.StatusCode);
        Assert.Equal("evidence_image_invalid", exception.Code);
    }

    [Theory(DisplayName = "COASTAL-EVIDENCE-019 prohibited PNG chunk ordering and animation are rejected")]
    [Trait("TestId", "COASTAL-EVIDENCE-019")]
    [InlineData("unknown-critical")]
    [InlineData("animated")]
    [InlineData("split-idat")]
    public async Task ProhibitedChunkFormsAreRejected(string scenario)
    {
        var png = BuildPng([0, 10, 20, 30, 255]);
        var invalid = scenario switch
        {
            "unknown-critical" => InsertBefore(png, "IDAT", Chunk("ABCD", [])),
            "animated" => InsertBefore(png, "IDAT", Chunk("acTL", [0, 0, 0, 1])),
            "split-idat" => SplitIdatAroundMetadata(png),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

        var exception = await Assert.ThrowsAsync<CoastalOperationsException>(() => Sanitize(invalid));

        Assert.Equal("evidence_image_invalid", exception.Code);
    }

    [Fact(DisplayName = "COASTAL-EVIDENCE-020 a valid image at the exact 5 MiB limit is accepted and metadata is removed")]
    [Trait("TestId", "COASTAL-EVIDENCE-020")]
    public async Task ExactMaximumSizeIsAccepted()
    {
        var png = BuildPng([0, 10, 20, 30, 255]);
        var metadataBytes = AssessmentEvidenceSanitizer.MaximumInputBytes - png.Length - 12;
        var exactLimit = InsertBefore(png, "IEND", Chunk("tEXt", new byte[metadataBytes]));

        Assert.Equal(AssessmentEvidenceSanitizer.MaximumInputBytes, exactLimit.Length);
        var sanitized = await Sanitize(exactLimit);

        Assert.True(sanitized.Length < exactLimit.Length);
        Assert.False(sanitized.AsSpan().IndexOf(Encoding.ASCII.GetBytes("tEXt")) >= 0);
        Assert.True(sanitized.AsSpan().IndexOf(Encoding.ASCII.GetBytes("IDAT")) >= 0);
    }

    [Fact(DisplayName = "COASTAL-EVIDENCE-021 one byte above the valid PNG size limit is rejected")]
    [Trait("TestId", "COASTAL-EVIDENCE-021")]
    public async Task OneByteAboveMaximumIsRejected()
    {
        var png = BuildPng([0, 10, 20, 30, 255]);
        var metadataBytes = AssessmentEvidenceSanitizer.MaximumInputBytes - png.Length - 12 + 1;
        var overLimit = InsertBefore(png, "IEND", Chunk("tEXt", new byte[metadataBytes]));

        Assert.Equal(AssessmentEvidenceSanitizer.MaximumInputBytes + 1, overLimit.Length);
        var exception = await Assert.ThrowsAsync<CoastalOperationsException>(() => Sanitize(overLimit));
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, exception.StatusCode);
        Assert.Equal("evidence_too_large", exception.Code);
    }

    [Fact(DisplayName = "COASTAL-EVIDENCE-022 a pre-cancelled evidence upload propagates cancellation")]
    [Trait("TestId", "COASTAL-EVIDENCE-022")]
    public async Task CancelledInputIsNotDecodedOrStored()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new AssessmentEvidenceSanitizer().SanitizePngAsync(
            new MemoryStream(BuildPng([0, 10, 20, 30, 255])), "image/png", cancelled.Token));
    }

    private static Task<byte[]> Sanitize(byte[] bytes) => new AssessmentEvidenceSanitizer().SanitizePngAsync(
        new MemoryStream(bytes), "image/png", CancellationToken.None);

    private static byte[] BuildPng(byte[] rawScanlines)
    {
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true)) zlib.Write(rawScanlines);
        return Join(
            [137, 80, 78, 71, 13, 10, 26, 10],
            Chunk("IHDR", Header(1, 1, 8, 6, 0, 0, 0)),
            Chunk("IDAT", compressed.ToArray()),
            Chunk("IEND", []));
    }

    private static byte[] Header(uint width, uint height, byte bitDepth, byte colorType, byte compression, byte filter, byte interlace)
    {
        var header = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = bitDepth;
        header[9] = colorType;
        header[10] = compression;
        header[11] = filter;
        header[12] = interlace;
        return header;
    }

    private static byte[] Chunk(string name, byte[] data)
    {
        var type = Encoding.ASCII.GetBytes(name);
        var chunk = new byte[12 + data.Length];
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(0, 4), (uint)data.Length);
        type.CopyTo(chunk, 4);
        data.CopyTo(chunk, 8);
        var crcInput = new byte[type.Length + data.Length];
        type.CopyTo(crcInput, 0);
        data.CopyTo(crcInput, type.Length);
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(8 + data.Length, 4), Crc(crcInput));
        return chunk;
    }

    private static byte[] ReplaceChunkData(byte[] png, string name, byte[] data)
    {
        var chunkStart = FindChunkStart(png, name);
        var chunkLength = checked((int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(chunkStart, 4))) + 12;
        return Join(png[..chunkStart], Chunk(name, data), png[(chunkStart + chunkLength)..]);
    }

    private static byte[] InsertBefore(byte[] png, string followingChunk, byte[] inserted)
    {
        var offset = FindChunkStart(png, followingChunk);
        return Join(png[..offset], inserted, png[offset..]);
    }

    private static byte[] SplitIdatAroundMetadata(byte[] png)
    {
        var start = FindChunkStart(png, "IDAT");
        var length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(start, 4)));
        var idat = png.AsSpan(start + 8, length).ToArray();
        var half = Math.Max(1, idat.Length / 2);
        return Join(png[..start], Chunk("IDAT", idat[..half]), Chunk("tEXt", [0]),
            Chunk("IDAT", idat[half..]), png[(start + length + 12)..]);
    }

    private static int FindChunkStart(byte[] png, string name)
    {
        var marker = Encoding.ASCII.GetBytes(name);
        var typeOffset = png.AsSpan().IndexOf(marker);
        if (typeOffset < 4) throw new InvalidOperationException($"Missing {name} PNG chunk.");
        return typeOffset - 4;
    }

    private static byte[] Join(params byte[][] parts)
    {
        using var output = new MemoryStream();
        foreach (var part in parts) output.Write(part);
        return output.ToArray();
    }

    private static uint Crc(ReadOnlySpan<byte> bytes)
    {
        var value = uint.MaxValue;
        foreach (var item in bytes)
        {
            value ^= item;
            for (var bit = 0; bit < 8; bit++) value = (value & 1) == 1 ? 0xedb88320U ^ (value >> 1) : value >> 1;
        }
        return ~value;
    }
}
