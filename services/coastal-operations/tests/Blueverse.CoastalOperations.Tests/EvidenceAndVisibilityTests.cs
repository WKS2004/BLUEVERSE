using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Blueverse.CoastalOperations.Application;
using Blueverse.CoastalOperations.Contracts;
using Blueverse.CoastalOperations.Domain;
using Microsoft.AspNetCore.Http;

namespace Blueverse.CoastalOperations.Tests;

public sealed class EvidenceAndVisibilityTests
{
    [Fact(DisplayName = "COASTAL-EVIDENCE-001 valid PNG evidence is checked and ancillary metadata is stripped")]
    public async Task COASTAL_EVIDENCE_001_SanitizesImageContent()
    {
        var sanitizer = new AssessmentEvidenceSanitizer();
        var original = BuildPng(includeMetadata: true);

        var sanitized = await sanitizer.SanitizePngAsync(new MemoryStream(original), "image/png", CancellationToken.None);

        Assert.True(sanitized.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }));
        Assert.False(ContainsBytes(sanitized, Encoding.ASCII.GetBytes("tEXt")));
        Assert.False(ContainsBytes(sanitized, Encoding.ASCII.GetBytes("private-location")));
        Assert.True(ContainsBytes(sanitized, Encoding.ASCII.GetBytes("IDAT")));
        Assert.True(ContainsBytes(sanitized, Encoding.ASCII.GetBytes("IEND")));
    }

    [Fact(DisplayName = "COASTAL-EVIDENCE-002 unsupported image media type is rejected")]
    public async Task COASTAL_EVIDENCE_002_RejectsUnsupportedMediaType()
    {
        var exception = await Assert.ThrowsAsync<CoastalOperationsException>(() =>
            new AssessmentEvidenceSanitizer().SanitizePngAsync(
                new MemoryStream([1, 2, 3]), "image/jpeg", CancellationToken.None));

        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, exception.StatusCode);
        Assert.Equal("evidence_media_type_unsupported", exception.Code);
    }

    [Fact(DisplayName = "COASTAL-EVIDENCE-003 malformed, corrupt and trailing PNG data are rejected")]
    public async Task COASTAL_EVIDENCE_003_RejectsMalformedPng()
    {
        var sanitizer = new AssessmentEvidenceSanitizer();
        foreach (var invalid in new[]
                 {
                     new byte[] { 137, 80, 78, 71 },
                     CorruptLastCrc(BuildPng(includeMetadata: false)),
                     AppendTrailingByte(BuildPng(includeMetadata: false))
                 })
        {
            var exception = await Assert.ThrowsAsync<CoastalOperationsException>(() =>
                sanitizer.SanitizePngAsync(new MemoryStream(invalid), "image/png", CancellationToken.None));
            Assert.Equal(StatusCodes.Status422UnprocessableEntity, exception.StatusCode);
            Assert.Equal("evidence_image_invalid", exception.Code);
        }
    }

    [Fact(DisplayName = "COASTAL-EVIDENCE-004 oversized upload is rejected before image decoding")]
    public async Task COASTAL_EVIDENCE_004_RejectsOversizedUpload()
    {
        var input = new MemoryStream(new byte[AssessmentEvidenceSanitizer.MaximumInputBytes + 1]);

        var exception = await Assert.ThrowsAsync<CoastalOperationsException>(() =>
            new AssessmentEvidenceSanitizer().SanitizePngAsync(input, "image/png", CancellationToken.None));

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, exception.StatusCode);
        Assert.Equal("evidence_too_large", exception.Code);
    }

    [Fact(DisplayName = "COASTAL-EVIDENCE-005 target baseline requires a current confirmed Member 1 identity")]
    public void COASTAL_EVIDENCE_005_OnlyConfirmedExperienceTargetsBootstrap()
    {
        static ComponentDependencyResult Result(string status, string availability) => new(
            "member-1-experience", "experience-availability", status, 1, 0, false, null,
            "test", DateTimeOffset.UtcNow,
            new ComponentDependencyEvidence(availability, null, null, "target-v1", null,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(5), []));

        Assert.True(TargetOperationalStateBootstrapPolicy.IsConfirmedTarget(Result("RESPONDED", "AVAILABLE")));
        Assert.True(TargetOperationalStateBootstrapPolicy.IsConfirmedTarget(Result("RESPONDED", "UNAVAILABLE")));
        Assert.False(TargetOperationalStateBootstrapPolicy.IsConfirmedTarget(Result("RESPONDED", "UNKNOWN")));
        Assert.False(TargetOperationalStateBootstrapPolicy.IsConfirmedTarget(Result("STALE", "AVAILABLE")));
        Assert.False(TargetOperationalStateBootstrapPolicy.IsConfirmedTarget(null));
    }

    [Fact(DisplayName = "COASTAL-ALERT-001 non-managers only see currently active public alerts")]
    public void COASTAL_ALERT_001_FiltersNonPublicAndInactiveAlerts()
    {
        var now = DateTimeOffset.UtcNow;
        var alerts = new[]
        {
            Alert("PUBLIC", "ACTIVE", now.AddMinutes(-1), now.AddMinutes(1)),
            Alert("OPERATIONS", "ACTIVE", now.AddMinutes(-1), now.AddMinutes(1)),
            Alert("PUBLIC", "PROPOSED", now.AddMinutes(-1), now.AddMinutes(1)),
            Alert("PUBLIC", "ACTIVE", now.AddMinutes(-2), now.AddMinutes(-1)),
            Alert("PUBLIC", "RESOLVED", now.AddMinutes(-1), now.AddMinutes(1))
        }.AsQueryable();

        var visible = AlertVisibilityPolicy.Apply(alerts, canManage: false, now).ToArray();

        Assert.Single(visible);
        Assert.Equal("PUBLIC", visible[0].Visibility);
        Assert.Equal("ACTIVE", visible[0].Lifecycle);
    }

    [Fact(DisplayName = "COASTAL-ALERT-002 managers retain access to all alert audiences and lifecycles")]
    public void COASTAL_ALERT_002_ManagerCanReviewAllAlerts()
    {
        var now = DateTimeOffset.UtcNow;
        var alerts = new[]
        {
            Alert("PUBLIC", "ACTIVE", now.AddMinutes(-1), now.AddMinutes(1)),
            Alert("OPERATIONS", "PROPOSED", now.AddMinutes(-1), now.AddMinutes(1))
        }.AsQueryable();

        Assert.Equal(2, AlertVisibilityPolicy.Apply(alerts, canManage: true, now).Count());
    }

    private static OperationalAlert Alert(string visibility, string lifecycle, DateTimeOffset starts, DateTimeOffset ends) => new()
    {
        Id = Guid.NewGuid(),
        TargetType = "ACTIVITY",
        TargetId = Guid.NewGuid(),
        Title = "Test alert",
        Description = "Synthetic fixture",
        Severity = "LOW",
        Visibility = visibility,
        Lifecycle = lifecycle,
        ValidFrom = starts,
        ValidUntil = ends
    };

    private static byte[] BuildPng(bool includeMetadata)
    {
        using var output = new MemoryStream();
        output.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var header = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4, 4), 1);
        header[8] = 8;
        header[9] = 6;
        WriteChunk(output, "IHDR", header);
        if (includeMetadata) WriteChunk(output, "tEXt", Encoding.ASCII.GetBytes("Comment\0private-location"));
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(new byte[] { 0, 20, 40, 60, 255 });
        WriteChunk(output, "IDAT", compressed.ToArray());
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    private static void WriteChunk(Stream output, string name, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
        output.Write(length);
        var type = Encoding.ASCII.GetBytes(name);
        output.Write(type);
        output.Write(data);
        var crcInput = new byte[type.Length + data.Length];
        type.CopyTo(crcInput, 0);
        data.CopyTo(crcInput, type.Length);
        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, ComputeCrc(crcInput));
        output.Write(crc);
    }

    private static uint ComputeCrc(ReadOnlySpan<byte> data)
    {
        var crc = uint.MaxValue;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) == 1 ? 0xedb88320U ^ (crc >> 1) : crc >> 1;
        }
        return ~crc;
    }

    private static byte[] CorruptLastCrc(byte[] png)
    {
        png[^1] ^= 0xff;
        return png;
    }

    private static byte[] AppendTrailingByte(byte[] png) => [.. png, 0xff];

    private static bool ContainsBytes(byte[] source, byte[] needle) =>
        source.AsSpan().IndexOf(needle) >= 0;
}
