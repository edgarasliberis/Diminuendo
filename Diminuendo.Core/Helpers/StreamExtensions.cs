using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Diminuendo.Core.Helpers
{
    public static class StreamExtensions
    {
        // Stream.CopyToAsync() implementation, that supports progress reporting.
        // Taken from Microsoft TAP document and adapted.
        public static async Task CopyToAsync(this Stream source, Stream destination, int bufferSize, CancellationToken cancellationToken, IProgress<int> progress, long fileSize = -1)
        {
            var buffer = new byte[bufferSize];
            int bytesRead;
            long totalRead = 0;
            if (progress != null && fileSize == -1) fileSize = source.Length;
            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
            {
                await destination.WriteAsync(buffer, 0, bytesRead).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (progress != null)
                {
                    totalRead += bytesRead;
                    progress.Report((int)(totalRead * 100 / fileSize));
                }
            }
        }
    }
}
