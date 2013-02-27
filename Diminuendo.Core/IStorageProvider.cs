using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Diminuendo.Core.FileSystem;

namespace Diminuendo.Core
{
    public interface IStorageProvider
    {
        /// <summary>
        /// User-friendly name for current storage provider.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Amount of available storage in bytes. 
        /// </summary>
        long Quota { get; }

        /// <summary>
        /// Loads information about storage provider, such as quota and updates file graph.
        /// </summary>
        Task LoadInfoAsync();

        /// <summary>
        /// Root object of file graph.
        /// </summary>
        DFileInfo Root { get; }

        /// <summary>
        /// Informs the provider that user has navigated to the specific folder, 
        /// so it can react accordingly.
        /// </summary>
        Task NavigatedToAsync(DFileInfo file);

        /// <summary>
        /// Creates a folder in a local file graph and on a provider's server.
        /// </summary>
        /// <param name="destinationFolder">Folder to create a new folder in.</param>
        /// <param name="name">Name of the new folder. Note that some characters may be forbidden.</param>
        /// <returns>File info of the new folder.</returns>
        Task<DFileInfo> CreateFolderAsync(DFileInfo destinationFolder, string name);

        /// <summary>
        /// Uploads a file to the storage provider. 
        /// </summary>
        /// <param name="destinationFolder">Folder to create a new file in.</param>
        /// <param name="name">Name of the new file. Note that some characters may be forbidden.</param>
        /// <param name="stream">Stream of file's contents.</param>
        /// <param name="cancellationToken">Cancellation token for upload operation.</param>
        /// <param name="progress">Progress reporting in percentages.</param>
        /// <param name="fileSize">
        /// Size of file to upload. Specify it when you need progress reporting and stream is not seekable.
        /// </param>
        /// <returns>File info of the new file.</returns>
        Task<DFileInfo> UploadFileAsync(DFileInfo destinationFolder, string name, Stream stream, 
            CancellationToken cancellationToken, IProgress<int> progress, long fileSize = -1);

        /// <summary>
        /// Downloads a file from the storage provider.
        /// </summary>
        /// <param name="file">A file to download.</param>
        /// <returns>A stream of file's contents.</returns>
        Task<Stream> GetDownloadStreamAsync(DFileInfo file);

        /// <summary>
        /// Tells the provider plug-in to syncronize local state with remote server.
        /// </summary>
        Task SynchronizeAsync();

        /// <summary>
        /// Renames a file.
        /// </summary>
        /// <param name="file">A file to rename.</param>
        /// <param name="newName">New name of the file. Note that some characters may be forbidden.</param>
        /// <returns>File info of the new file.</returns>
        Task<DFileInfo> RenameAsync(DFileInfo file, string newName);

        /// <summary>
        /// Moves a file or a folder to the different location.
        /// </summary>
        /// <param name="file">A file to move.</param>
        /// <param name="destinationFolder">The new location of the file.</param>
        /// <returns>File info of the moved file.</returns>
        Task<DFileInfo> MoveAsync(DFileInfo file, DFileInfo destinationFolder);

        /// <summary>
        /// Copies a file or a folder to the different location.
        /// </summary>
        /// <param name="file">A file to copy.</param>
        /// <param name="destinationFolder">The new location of the file.</param>
        /// <returns>File info of the copied file.</returns>
        Task<DFileInfo> CopyAsync(DFileInfo file, DFileInfo destinationFolder);

        /// <summary>
        /// Removes a specified file from the storage provider.
        /// </summary>
        /// <param name="file">A file to delete.</param>
        Task DeleteAsync(DFileInfo file);
    }
}
