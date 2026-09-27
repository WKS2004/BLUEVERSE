using Microsoft.Extensions.Options;

namespace Blueverse.CoastalOperations.Application;

public sealed class EvidenceStorageOptions
{
    public string? RootPath { get; set; }
}

public interface IAssessmentEvidenceStorage
{
    Task StoreAsync(Guid evidenceId, byte[] content, CancellationToken cancellationToken);
    Task<byte[]?> ReadAsync(Guid evidenceId, CancellationToken cancellationToken);
    Task DeleteAsync(Guid evidenceId, CancellationToken cancellationToken);
}

public sealed class FileSystemAssessmentEvidenceStorage(IOptions<EvidenceStorageOptions> options) : IAssessmentEvidenceStorage
{
    private readonly string _rootPath = Path.GetFullPath(string.IsNullOrWhiteSpace(options.Value.RootPath)
        ? Path.Combine(AppContext.BaseDirectory, "private-evidence")
        : options.Value.RootPath);

    public async Task StoreAsync(Guid evidenceId, byte[] content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_rootPath);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(_rootPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var finalPath = GetPath(evidenceId);
        var temporaryPath = Path.Combine(_rootPath, $".{evidenceId:N}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(content, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(temporaryPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.Move(temporaryPath, finalPath, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public async Task<byte[]?> ReadAsync(Guid evidenceId, CancellationToken cancellationToken)
    {
        try
        {
            return await File.ReadAllBytesAsync(GetPath(evidenceId), cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    public Task DeleteAsync(Guid evidenceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetPath(evidenceId);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string GetPath(Guid evidenceId) => Path.Combine(_rootPath, $"{evidenceId:N}.png");
}
