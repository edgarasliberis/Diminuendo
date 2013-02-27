using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Diminuendo.Core.Helpers;

namespace Diminuendo.Core.FileSystem
{
    [Serializable]
    public class DFileInfo
    {
        protected static char[] invalidNameChars = { '\\', ':', '?', '*', '<', '>', '"', '|', '/' };

        #region Properties

        private long _size = 0;
        private string _name = null;

        /// <summary>
        /// Indicates whether this object represents a directory. 
        /// If property is false, this object represents a file.
        /// </summary>
        public bool IsDirectory {
            get
            {
                return Contents != null;
            }
            set
            {
                // If new value is being set to 'false', means this entry should be
                // an ordinary file ('Contents' property should be set to null).
                // Else, this entry should be an empty directory 
                // ('Contents' property should be set to an empty dictionary).
                if (value == false)
                {
                    Contents = null;
                }
                else if(Contents == null)
                {
                    Contents = new Dictionary<string, DFileInfo>();
                    Size = 0;
                }
            }
        }

        /// <summary>
        /// Data provider for current file.
        /// </summary>
        public IStorageProvider Provider { get; set; }

        /// <summary>
        /// Parent directory of a current file.
        /// </summary>
        public DFileInfo Parent { get; set; }

        /// <summary>
        /// Indicates whether file is not avalable for editing and removal.
        /// </summary>
        public bool IsReadOnly { get; set; }

        /// <summary>
        /// For directories only. Contents of a directory are stored here.
        /// </summary>
        public Dictionary<string, DFileInfo> Contents { get; set; }

        /// <summary>
        /// File's size in bytes.
        /// </summary>
        public long Size
        {
            get
            {
                return _size;
            }
            set
            {
                if (value < 0) throw new InvalidOperationException("'Size' can not be less than 0 bytes");
                _size = value;
            }
        }
        
        /// <summary>
        /// User friendly file's name. 
        /// </summary>
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                NameCheck(value);
                _name = value.Trim();
            }
        }
        #endregion

        #region Public methods
        public DFileInfo this[string name]
        {
            get { return this.Contents[name]; }
            set { this.Contents[name] = value; }
        }

        /// <summary>
        /// Navigates to a file denoted by a path. Separate folder names using a slash (any).
        /// </summary>
        /// <param name="relPath">Relative path of a file.</param>
        /// <returns>File info of resulting file. Null if not found.</returns>
        public DFileInfo NavigateTo(string relPath)
        {
            if(string.IsNullOrEmpty(relPath))
                throw new InvalidOperationException(ExceptionMessage.IsNullOrInvalid("relPath"));
            var names = (IEnumerable<string>)relPath.Split('\\', '/');
            var enumerator = (IEnumerator<string>)names.GetEnumerator();
            if(!enumerator.MoveNext()) return null;
            return traverseFileSystem(this, enumerator);
        }

        /// <summary>
        /// Searches for a file with a specific name in the directory.
        /// </summary>
        /// <param name="name">Name of the file.</param>
        /// <returns>File info of resulting file. Null if not found.</returns>
        public DFileInfo Find(string name)
        {
            if (!IsDirectory) return null;
            return Contents.Values.FirstOrDefault(file => file.Name == name);
        }

        /// <summary>
        /// Creates a folder in this directory.
        /// </summary>
        /// <param name="name">Name of the new file. Note that some characters may be forbidden.</param>
        /// <returns>File info of the new folder.</returns>
        public Task<DFileInfo> CreateFolderAsync(string name)
        {
            NameCheck(name);
            ValidityCheck();
            return Provider.CreateFolderAsync(this, name.Trim());
        }

        /// <summary>
        /// Uploads a file to this directory. 
        /// </summary>
        /// <param name="name">Name of the new file. Note that some characters may be forbidden.</param>
        /// <param name="stream">Stream of file's contents.</param>
        /// <param name="cancellationToken">Cancellation token for upload operation.</param>
        /// <param name="progress">Progress reporting in percentages.</param>
        /// <param name="fileSize">
        /// Size of file to upload. Specify it when you need progress reporting and stream is not seekable.
        /// </param>
        /// <returns>File info of the new file.</returns>
        public Task<DFileInfo> CreateFileAsync(string name, Stream stream, CancellationToken cancellationToken, 
            IProgress<int> progress = null, long fileSize = -1)
        {
            NameCheck(name);
            ValidityCheck();
            if (stream == null)
                throw new NullReferenceException(ExceptionMessage.IsNullOrInvalid("stream"));
            // If user wants to report progress, system must know the amount of data to upload.
            // It can be specified either using fileSize parameter or by using a seekable stream.
            if (progress != null && fileSize == -1)
            {
                if (!stream.CanSeek)
                    throw new InvalidOperationException("You must specify either a 'fileSize' parameter or use a seekable stream for progress reporting to work.");
                fileSize = stream.Length;
            }
            return Provider.UploadFileAsync(this, name.Trim(), stream, cancellationToken, progress, fileSize);
        }

        /// <summary>
        /// Gets the download stream for the current file.
        /// </summary>
        /// <returns>A stream of file's contents.</returns>
        public Task<Stream> GetDownloadStreamAsync()
        {
            if (IsDirectory) 
                throw new InvalidOperationException("Downloading directories is not supported.");
            return Provider.GetDownloadStreamAsync(this);
        }

        /// <summary>
        /// Renames this file of folder.
        /// </summary>
        /// <param name="newName">New name of the file. Note that some characters may be forbidden.</param>
        /// <returns>File info of the new file.</returns>
        public Task<DFileInfo> RenameAsync(string newName)
        {
            NameCheck(newName);
            return Provider.RenameAsync(this, newName);
        }

        /// <summary>
        /// Removes this file from the storage provider.
        /// </summary>
        public Task DeleteAsync()
        {
            if (IsReadOnly)
                throw new InvalidOperationException(ExceptionMessage.ReadOnly);
            return Provider.DeleteAsync(this);
        }

        /// <summary>
        /// This method should be called in order to inform provider that user
        /// has navigated to current file/folder. Provider may want to react
        /// accordingly, for example load contents of it.
        /// </summary>
        /// <returns>A task for a future result of method.</returns>
        public Task NavigatedAsync()
        {
            return Provider.NavigatedToAsync(this);
        }

        /// <summary>
        /// Moves this file or a folder to the different location.
        /// </summary>
        /// <param name="destinationFolder">The new location of the file.</param>
        /// <returns>File info of the moved file.</returns>
        public Task<DFileInfo> MoveAsync(DFileInfo destinationFolder)
        {
            if (IsReadOnly)
                throw new InvalidOperationException(ExceptionMessage.ReadOnly);
            return localFileOperationAsync(destinationFolder, true);          
        }

        /// <summary>
        /// Copies this file or a folder to the different location.
        /// </summary>
        /// <param name="destinationFolder">The new location of the file.</param>
        /// <returns>File info of the copied file.</returns>
        public Task<DFileInfo> CopyAsync(DFileInfo destinationFolder)
        {
            return localFileOperationAsync(destinationFolder, false);
        }

        /// <summary>
        /// Makes a clone of this file or a folder (incl. contents).
        /// </summary>
        /// <returns>File info of the new file.</returns>
        public DFileInfo Clone()
        {
            var serStream = new MemoryStream();
            SaveState.Serialize(this, serStream);
            serStream.Seek(0, SeekOrigin.Begin);
            var clone = SaveState.Deserialize<DFileInfo>(serStream);
            return clone;
        }

        #endregion

        #region Synchronous counterparts
        /// <summary>
        /// Creates a folder in this directory.
        /// </summary>
        /// <param name="name">Name of the new file. Note that some characters may be forbidden.</param>
        /// <returns>File info of the new folder.</returns>
        public DFileInfo CreateFolder(string name)
        {
            return this.CreateFolderAsync(name).Result;
        }

        /// <summary>
        /// Uploads a file to this directory. 
        /// </summary>
        /// <param name="name">Name of the new file. Note that some characters may be forbidden.</param>
        /// <param name="stream">Stream of file's contents.</param>
        /// <param name="cancellationToken">Cancellation token for upload operation.</param>
        /// <param name="progress">Progress reporting in percentages.</param>
        /// <param name="fileSize">
        /// Size of file to upload. Specify it when you need progress reporting and stream is not seekable.
        /// </param>
        /// <returns>File info of the new file.</returns>
        public DFileInfo CreateFile(string name, Stream stream, CancellationToken cancellationToken,
            IProgress<int> progress = null, long fileSize = -1)
        {
            return this.CreateFileAsync(name, stream, cancellationToken, progress, fileSize).Result;
        }

        /// <summary>
        /// Gets the download stream for the current file.
        /// </summary>
        /// <returns>A stream of file's contents.</returns>
        public Stream GetDownloadStream()
        {
            return this.GetDownloadStreamAsync().Result;
        }

        /// <summary>
        /// Renames this file of folder.
        /// </summary>
        /// <param name="newName">New name of the file. Note that some characters may be forbidden.</param>
        /// <returns>File info of the new file.</returns>
        public DFileInfo Rename(string newName)
        {
            return this.RenameAsync(newName).Result;
        }

        /// <summary>
        /// Removes this file from the storage provider.
        /// </summary>
        public void Delete()
        {
            this.DeleteAsync().Wait();
        }

        /// <summary>
        /// This method should be called in order to inform provider that user
        /// has navigated to current file/folder. Provider may want to react
        /// accordingly, for example load contents of it.
        /// </summary>
        public void Navigated()
        {
            this.NavigatedAsync().Wait();
        }

        /// <summary>
        /// Moves this file or a folder to the different location.
        /// </summary>
        /// <param name="destinationFolder">The new location of the file.</param>
        /// <returns>File info of the moved file.</returns>
        public DFileInfo Move(DFileInfo destinationFolder)
        {
            return this.MoveAsync(destinationFolder).Result;
        }

        /// <summary>
        /// Copies this file or a folder to the different location.
        /// </summary>
        /// <param name="destinationFolder">The new location of the file.</param>
        /// <returns>File info of the copied file.</returns>
        public DFileInfo Copy(DFileInfo destinationFolder)
        {
            return this.CopyAsync(destinationFolder).Result;
        }
        #endregion

        #region Private/Protected methods
        private static DFileInfo traverseFileSystem(DFileInfo currentFile, IEnumerator<string> nameEnumerator)
        {
            if (currentFile == null) return null;
            currentFile.Navigated();
            string name = nameEnumerator.Current;
            var nextFile = currentFile.Find(name);
            if (nameEnumerator.MoveNext())
                return traverseFileSystem(nextFile, nameEnumerator);
            else return nextFile;
        }

        private async Task<DFileInfo> localFileOperationAsync(DFileInfo destinationFolder, bool move)
        {
            if (destinationFolder == null)
                throw new NullReferenceException(ExceptionMessage.IsNullOrInvalid("destinationFolder"));
            if (destinationFolder.IsReadOnly)
                throw new InvalidOperationException(ExceptionMessage.ReadOnly);
            if (!destinationFolder.IsDirectory)
                throw new InvalidOperationException("Destination is not a folder. Impossible to move/copy to a file.");
            if (this.Provider == destinationFolder.Provider)
            {
                if (move)
                {
                    return await Provider.MoveAsync(this, destinationFolder);
                }
                else
                {
                    return await Provider.CopyAsync(this, destinationFolder);
                }
            }
            else
            {
                if (this.IsDirectory)
                    throw new InvalidOperationException("Copying or moving folders between providers is not supported.");
                Stream thisFile = await this.GetDownloadStreamAsync();
                var file = await destinationFolder.CreateFileAsync(this.Name, thisFile, CancellationToken.None);
                if (move) await this.DeleteAsync();
                return file;
            }
        }

        protected static bool ContainsInvalidChars(string str, char[] bannedChars)
        {
            return str.IndexOfAny(bannedChars) > -1;
        }

        protected void NameCheck(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new NullReferenceException(ExceptionMessage.IsNullOrInvalid("name"));
            if (ContainsInvalidChars(name, invalidNameChars))
                throw new InvalidOperationException(ExceptionMessage.InvalidChars("name", invalidNameChars));

        }

        protected void ValidityCheck()
        {
            if (!IsDirectory)
                throw new InvalidOperationException("Impossible to create entry in a file.");
            if (IsReadOnly)
                throw new InvalidOperationException(ExceptionMessage.ReadOnly);
        }
        #endregion
    }
}
