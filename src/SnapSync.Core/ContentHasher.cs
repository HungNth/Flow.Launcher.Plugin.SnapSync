using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace SnapSync.Core;

/// <summary>
/// Streams file contents to compute SHA-256 hashes with cancellation support and bounded memory usage.
/// </summary>
internal static class ContentHasher
{
    private const int BufferSize = 81920;

    /// <summary>
    /// Computes the SHA-256 hash of a file as a lowercase hex string.
    /// </summary>
    public static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        using var sha256 = SHA256.Create();
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

        var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(hashBytes);
    }
}
