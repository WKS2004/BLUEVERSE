using System.Buffers.Binary;
using System.IO.Compression;
namespace Blueverse.CoastalOperations.Application;

/// <summary>
/// Accepts a deliberately small PNG subset, fully inflates and validates its
/// scanlines, then emits only image-bearing chunks. Ancillary metadata and
/// trailing/polyglot bytes are discarded.
/// </summary>
public sealed class AssessmentEvidenceSanitizer
{
    public const int MaximumInputBytes = 5 * 1024 * 1024;
    private const int MaximumDimension = 4096;
    private const long MaximumPixels = 12_000_000;
    private static ReadOnlySpan<byte> Signature => [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] CrcTable = BuildCrcTable();

    public async Task<byte[]> SanitizePngAsync(Stream input, string? declaredContentType, CancellationToken cancellationToken)
    {
        if (!string.Equals(declaredContentType, "image/png", StringComparison.OrdinalIgnoreCase))
        {
            throw new CoastalOperationsException(StatusCodes.Status415UnsupportedMediaType,
                "evidence_media_type_unsupported", "Image type is not supported", "Upload a PNG image.");
        }

        using var source = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var read = await input.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0) break;
            if (source.Length + read > MaximumInputBytes)
            {
                throw new CoastalOperationsException(StatusCodes.Status413PayloadTooLarge,
                    "evidence_too_large", "Image is too large", "The maximum PNG upload size is 5 MiB.");
            }

            await source.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return Sanitize(source.ToArray(), cancellationToken);
    }

    private static byte[] Sanitize(byte[] input, CancellationToken cancellationToken)
    {
        if (input.Length < Signature.Length || !input.AsSpan(0, Signature.Length).SequenceEqual(Signature))
            throw InvalidImage();

        var offset = Signature.Length;
        byte[]? header = null;
        byte[]? palette = null;
        byte[]? transparency = null;
        using var imageData = new MemoryStream();
        var sawData = false;
        var dataEnded = false;
        var sawEnd = false;
        var width = 0;
        var height = 0;
        var colorType = -1;
        var bytesPerPixel = 0;

        while (offset < input.Length)
        {
            if (input.Length - offset < 12) throw InvalidImage();
            var dataLength = BinaryPrimitives.ReadUInt32BigEndian(input.AsSpan(offset, 4));
            if (dataLength > MaximumInputBytes || dataLength > int.MaxValue ||
                (long)offset + 12 + dataLength > input.Length)
                throw InvalidImage();

            var type = input.AsSpan(offset + 4, 4);
            if (!IsValidChunkType(type)) throw InvalidImage();
            var chunkData = input.AsSpan(offset + 8, (int)dataLength);
            var expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(input.AsSpan(offset + 8 + (int)dataLength, 4));
            if (ComputeCrc(type, chunkData) != expectedCrc) throw InvalidImage();

            var chunkName = System.Text.Encoding.ASCII.GetString(type);
            offset += 12 + (int)dataLength;

            if (header is null)
            {
                if (chunkName != "IHDR" || chunkData.Length != 13) throw InvalidImage();
                header = chunkData.ToArray();
                var rawWidth = BinaryPrimitives.ReadUInt32BigEndian(chunkData[..4]);
                var rawHeight = BinaryPrimitives.ReadUInt32BigEndian(chunkData.Slice(4, 4));
                colorType = chunkData[9];
                if (rawWidth is 0 or > MaximumDimension || rawHeight is 0 or > MaximumDimension ||
                    (long)rawWidth * rawHeight > MaximumPixels || chunkData[8] != 8 ||
                    colorType is not (2 or 6) || chunkData[10] != 0 || chunkData[11] != 0 || chunkData[12] != 0)
                    throw InvalidImage();
                width = (int)rawWidth;
                height = (int)rawHeight;
                bytesPerPixel = colorType == 6 ? 4 : 3;
                continue;
            }

            if (chunkName == "IHDR") throw InvalidImage();
            if (chunkName == "IDAT")
            {
                if (dataEnded || sawEnd) throw InvalidImage();
                sawData = true;
                imageData.Write(chunkData);
                continue;
            }

            if (sawData) dataEnded = true;
            if (chunkName == "IEND")
            {
                if (!sawData || dataLength != 0 || offset != input.Length) throw InvalidImage();
                sawEnd = true;
                break;
            }

            if (chunkName is "acTL" or "fcTL" or "fdAT") throw InvalidImage();
            if (chunkName == "PLTE")
            {
                if (sawData || palette is not null || chunkData.Length is 0 or > 768 || chunkData.Length % 3 != 0)
                    throw InvalidImage();
                palette = chunkData.ToArray();
                continue;
            }

            if (chunkName == "tRNS")
            {
                if (sawData || colorType != 2 || transparency is not null || chunkData.Length != 6)
                    throw InvalidImage();
                transparency = chunkData.ToArray();
                continue;
            }

            // Unknown critical chunks affect image decoding and cannot be
            // safely removed. Ancillary chunks are intentionally stripped.
            if ((type[0] & 0x20) == 0) throw InvalidImage();
        }

        if (!sawEnd || header is null || !sawData || imageData.Length == 0) throw InvalidImage();
        if (colorType == 6 && transparency is not null) throw InvalidImage();

        imageData.Position = 0;
        try
        {
            using var zlib = new ZLibStream(imageData, CompressionMode.Decompress, leaveOpen: true);
            var row = new byte[checked(width * bytesPerPixel + 1)];
            for (var rowIndex = 0; rowIndex < height; rowIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    zlib.ReadExactly(row);
                }
                catch (EndOfStreamException)
                {
                    throw InvalidImage();
                }

                if (row[0] > 4) throw InvalidImage();
            }

            if (zlib.ReadByte() != -1) throw InvalidImage();
        }
        catch (InvalidDataException)
        {
            throw InvalidImage();
        }

        using var sanitized = new MemoryStream(input.Length);
        sanitized.Write(Signature);
        WriteChunk(sanitized, "IHDR", header);
        if (palette is not null) WriteChunk(sanitized, "PLTE", palette);
        if (transparency is not null) WriteChunk(sanitized, "tRNS", transparency);
        imageData.Position = 0;
        var idat = imageData.ToArray();
        WriteChunk(sanitized, "IDAT", idat);
        WriteChunk(sanitized, "IEND", []);
        return sanitized.ToArray();
    }

    private static void WriteChunk(Stream output, string name, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)data.Length));
        output.Write(length);
        Span<byte> type = stackalloc byte[4];
        System.Text.Encoding.ASCII.GetBytes(name, type);
        output.Write(type);
        output.Write(data);
        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, ComputeCrc(type, data));
        output.Write(crc);
    }

    private static uint ComputeCrc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = uint.MaxValue;
        foreach (var value in type) crc = CrcTable[(crc ^ value) & 0xff] ^ (crc >> 8);
        foreach (var value in data) crc = CrcTable[(crc ^ value) & 0xff] ^ (crc >> 8);
        return ~crc;
    }

    private static bool IsValidChunkType(ReadOnlySpan<byte> type)
    {
        if (type.Length != 4 || (type[2] >= (byte)'a' && type[2] <= (byte)'z')) return false;
        foreach (var value in type)
        {
            if (!((value >= (byte)'A' && value <= (byte)'Z') ||
                  (value >= (byte)'a' && value <= (byte)'z')))
                return false;
        }

        return true;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            var value = index;
            for (var bit = 0; bit < 8; bit++)
                value = (value & 1) == 1 ? 0xedb88320U ^ (value >> 1) : value >> 1;
            table[index] = value;
        }
        return table;
    }

    private static CoastalOperationsException InvalidImage() => new(
        StatusCodes.Status422UnprocessableEntity,
        "evidence_image_invalid",
        "Image content is invalid",
        "The upload is not a valid, supported static PNG image.");
}
