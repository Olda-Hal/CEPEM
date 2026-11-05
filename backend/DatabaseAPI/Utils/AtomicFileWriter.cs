namespace DatabaseAPI.Utils;

/// <summary>
/// Writes a file through a temporary sibling and an atomic rename, so a crash
/// or concurrent reader never observes a partially written encrypted file.
/// </summary>
public static class AtomicFileWriter
{
    public static async Task WriteAllBytesAsync(
        string path,
        byte[] data,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, data, cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch
                {
                    // Cleanup is best-effort; the real write already succeeded
                    // or the original exception must be preserved.
                }
            }
        }
    }
}
