using Diminuendo.Core.Exceptions;
using Diminuendo.Core.FileSystem;
using Diminuendo.Core.Helpers;
using Diminuendo.Core.StorageProviders.Dropbox.OAuth;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Timers;
using System.Threading.Tasks;
using System.Threading;

namespace Diminuendo.Core.StorageProviders.Dropbox
{
    [Serializable]
    public class DropboxClient : IStorageProvider
    {
        #region Private fields
        private OAuthToken _requestToken, _accessToken;
        private OAuthManager _oAuth;
        private string _cursor = null;
        private DropboxFileInfo _root = new DropboxFileInfo() { IsDirectory = true };
        #endregion

        #region Public properties
        public DFileInfo Root { get { return _root; } }
        public long Quota { get; private set; }
        public string Name { get; set; }
        #endregion

        #region Public methods
        public DropboxClient()
        {
            Name = "Dropbox";
        }

        /// <summary>
        /// Returns an URL for user to visit and allow the application to use their Dropbox.
        /// </summary>
        public async Task<Uri> AuthUrlAsync()
        {
            if (_requestToken == null) await retrieveRequestToken().ConfigureAwait(false);
            return new Uri(DropboxConfig.AuthUrl + _requestToken.Token);
        }

        /// <summary>
        /// Returns an URL for user to visit and allow the application to use their Dropbox.
        /// </summary>
        public Uri AuthUrl()
        {
            return this.AuthUrlAsync().Result;
        }

        /// <summary>
        /// Provides the plug-in with necessary keys to make API requests.
        /// </summary>
        /// <param name="key">App key obtained from Dropbox.</param>
        /// <param name="secret">App secret obtained from Dropbox.</param>
        public void SupplyAppKey(string key, string secret)
        {
            _oAuth = new OAuthManager(key, secret);
        }
        #endregion

        #region Private helpers
        private void removeFile(string[] folders)
        {
            DropboxFileInfo currentFile = _root;
            for (int i = 1; i < folders.Length - 1; ++i)
            {
                DFileInfo nextFile;
                if (!currentFile.Contents.TryGetValue(folders[i], out nextFile)) return;
                currentFile = (DropboxFileInfo)nextFile;
            }
            currentFile.Contents.Remove(folders[folders.Length - 1]);
        }

        private void addFile(string[] folders, JToken metadata)
        {
            DropboxFileInfo currentFile = _root;
            for(int i = 1; i < folders.Length; ++i)
            {
                string name = folders[i];
                DFileInfo nextFile;
                if (!currentFile.Contents.TryGetValue(name, out nextFile))
                {
                    // There is a guarantee that if file/folder doesn't already exist, metadata for it will 
                    // arrive later. Thus all wrong assumptions made at this step (if any) will be corrected. 
                    nextFile = new DropboxFileInfo() { IsDirectory = true, Parent = currentFile };
                    currentFile.Contents.Add(name, nextFile);
                }
                currentFile = (DropboxFileInfo)nextFile;
            }
            setupFileWithMetadata(currentFile, metadata);
        }

        private void setupFileWithMetadata(DropboxFileInfo file, JToken metadata)
        {
            file.Provider = this;
            file.IsReadOnly = false;
            file.IsDirectory = (bool)metadata["is_dir"];
            file.Size = (long)metadata["bytes"];

            string hash = metadata.Value<string>("hash");
            if (hash != null) file.Hash = hash;

            string path = (string)metadata["path"];
            if (path == "/") file.Name = this.Name;
            else file.Name = path.Substring(path.LastIndexOf('/') + 1);
        }

        private async Task<string> queryServer(ParamUrl url, string method = WebRequestMethods.Http.Get)
        {
            try
            {
                var signedUrl = await signAsync(url).ConfigureAwait(false);
                return await Http.ResponseToAsync(signedUrl, method);
            }
            catch (WebException e)
            {
                throw parseException(e);
            }            
        }

        private async Task<string> signAsync(ParamUrl url)
        {
            if (_oAuth == null)
                throw new ProviderNotSetupException("Please provide app key and secret first.");
            if (_accessToken == null) await retrieveAccessToken().ConfigureAwait(false);
            return _oAuth.Sign(url, _accessToken);
        }

        private Exception parseException(WebException e)
        {
            string message = e.ResponseString();
            Nullable<int> code = e.HttpStatusCode();
            if (!string.IsNullOrEmpty(message)) message = (string)JObject.Parse(message)["error"];
            else message = string.Empty;
            if (code.HasValue) message += string.Format(" ({0})", code.Value);

            switch (code)
            {
                case 401:
                    // Unauthorized.
                    return new AuthorizationFailureException(message, e); 
                case 403:
                    // Forbidden.
                    return new FileConflictException(message, e);
                case 404:
                    // Not found.
                    return new FileNotFoundException(message, e);
                case 503: //TODO: implement delayed calling
                default:
                    if (code.HasValue && code / 100 == 5) return new ProviderNotAvailableException(message, e);
                    else return e;
            }
        }

        private async Task<DFileInfo> copyOperation(DropboxFileInfo file, 
            DropboxFileInfo destFolder, bool move, string newName = null)
        {
            string nameKey = file.Name.ToLowerInvariant();
            string newNameKey = (newName == null)? nameKey : newName.ToLowerInvariant();
            // Storage provider should check possible file conflicts by itself, because
            // the outer system doesn't know how to access keys in dictionary correctly.
            if (destFolder.Contents.ContainsKey(newNameKey))
                throw new FileConflictException("Entry with specified name is already present.");

            string path = concatenatePath(destFolder.Path, newName ?? file.Name);

            var url = new ParamUrl(move ? DropboxConfig.MoveUrl : DropboxConfig.CopyUrl);
            url.Add("root", DropboxConfig.AccessType);
            url.Add("from_path", percentEncoding(file.Path));
            url.Add("to_path", percentEncoding(path));

            var response = await queryServer(url).ConfigureAwait(false);
            var metadata = JObject.Parse(response);
            
            if (move)
            {
                file.Parent.Contents.Remove(nameKey);
                file.Parent = destFolder;
                setupFileWithMetadata(file, metadata);
                destFolder.Contents.Add(newNameKey, file);
                return file;
            }
            else
            {
                var newFile = (DropboxFileInfo)file.Clone();
                newFile.Parent = destFolder;
                setupFileWithMetadata(newFile, metadata);
                destFolder.Contents.Add(newNameKey, newFile);
                return newFile;
            }
        }

        private static OAuthToken tokenFromResponse(string response)
        {
            var dict = Http.ParseResponse(response);
            return new OAuthToken(dict["oauth_token"], dict["oauth_token_secret"]);
        }

        private async Task retrieveRequestToken()
        {
            if (_oAuth == null)
                throw new ProviderNotSetupException("Please provide app key and secret first.");
            var url = _oAuth.Sign(new ParamUrl(DropboxConfig.RequestTokenUrl), null);
            var response = await Http.ResponseToAsync(url).ConfigureAwait(false);
            _requestToken = tokenFromResponse(response);
        }

        private async Task retrieveAccessToken()
        {
            if (_oAuth == null)
                throw new ProviderNotSetupException("Please provide app key and secret first.");
            var url = _oAuth.Sign(new ParamUrl(DropboxConfig.AccessTokenUrl), _requestToken);
            var response = await Http.ResponseToAsync(url).ConfigureAwait(false);
            _accessToken = tokenFromResponse(response);
        }

        private async Task<JToken> loadMetadata(string path, bool list = false, string hash = null)
        {
            var baseUrl = new StringBuilder(DropboxConfig.MetadataUrl);
            baseUrl.Append(DropboxConfig.AccessType);
            baseUrl.Append(path);

            var url = new ParamUrl(baseUrl.ToString());
            if (list) url.Add("file_limit", "25000");
            if (!list) url.Add("list", "false");
            if (hash != null) url.Add("hash", hash);

            string response;
            try
            {
                response = await queryServer(url).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                if(e.Message.Contains("(304)")) return null;
                else throw e;
            }
            
            return JToken.Parse(response);
        }

        /// <summary>
        /// Correctly appends new file's name to the path given.
        /// </summary>
        internal static string concatenatePath(string path, string name)
        {
            var builder = new StringBuilder();
            // The only path that can end with '/' is root's path, which is "/".
            if (path != "/") builder.Append(path);
            builder.Append('/');
            builder.Append(name);
            return builder.ToString();
        }

        private static string percentEncoding(string s)
        {
            // This type of encoding (uppercase) seems suitable for Dropbox.
            // Looks like the server doesn't mind that '(' and ')' are not replaced.
            return Uri.EscapeDataString(s);
        }
        #endregion

        #region IStorageProvider implementation
        public async Task NavigatedToAsync(DFileInfo file)
        {
            // File system updates on hash mismatch.
            var dFile = (DropboxFileInfo)file;
            var metadata = await loadMetadata(dFile.Path, true, dFile.Hash).ConfigureAwait(false);
            if (metadata != null)
            {
                setupFileWithMetadata(dFile, metadata);
                await SynchronizeAsync().ConfigureAwait(false);
            }
        }

        public async Task<DFileInfo> CreateFolderAsync(DFileInfo destinationFolder, string name)
        {
            string nameKey = name.ToLowerInvariant();
            if (destinationFolder.Contents.ContainsKey(nameKey))
                throw new FileConflictException("Entry with name specified is already present.");

            var dParent = (DropboxFileInfo)destinationFolder;
            string path = concatenatePath(dParent.Path, name);

            var url = new ParamUrl(DropboxConfig.CreateFolderUrl);
            url.Add("root", DropboxConfig.AccessType);
            url.Add("path", path);

            var response = await queryServer(url).ConfigureAwait(false);
            var metadata = JObject.Parse(response);

            var newFolder = new DropboxFileInfo() { Parent = dParent };
            setupFileWithMetadata(newFolder, metadata);

            dParent.Contents.Add(nameKey, newFolder);
            return newFolder;
        }

        public async Task<DFileInfo> UploadFileAsync(DFileInfo destinationFolder, string name, Stream stream, 
            CancellationToken cancellationToken, IProgress<int> progress, long fileSize)
        {
            var dDestFolder = (DropboxFileInfo)destinationFolder;
            string uploadId = null;

            using (stream)
            {
                if (progress != null) progress.Report(0);
                var buffer = new byte[DropboxConfig.ChunkSize];
                long totalUploaded = 0, offset = 0;

                // Some data is read from the buffer until it is full and we 
                // attempt to push it to the server until it acknowledges all of it.
                while (true)
                {
                    int bytesRead = 0, bytesReadThisTime = 0;
                    while (bytesRead < buffer.Length)
                    {
                        bytesReadThisTime =
                            await stream.ReadAsync(buffer, bytesRead, buffer.Length - bytesRead).ConfigureAwait(false);
                        bytesRead += bytesReadThisTime;
                        if (bytesReadThisTime == 0) break;
                    }

                    if (bytesRead == 0) break;

                    int relativeOffset = 0;
                    // relativeOffset shows how many bytes server already accepted.
                    while (relativeOffset < bytesRead)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var url = new ParamUrl(DropboxConfig.UploadUrl);
                        url.Add("offset", (offset + relativeOffset).ToString());
                        if (uploadId != null) url.Add("upload_id", uploadId);

                        var signedUrl = await signAsync(url).ConfigureAwait(false);
                        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(signedUrl);
                        request.Method = WebRequestMethods.Http.Put;
                        request.ContentLength = bytesRead - relativeOffset;

                        using (var requestStream = await request.GetRequestStreamAsync().ConfigureAwait(false))
                        {
                            requestStream.Write(buffer, relativeOffset, bytesRead - relativeOffset);
                        }

                        JToken response;
                        try
                        {
                            var webResponse = await request.GetResponseAsync().ConfigureAwait(false);
                            StreamReader reader = new StreamReader(webResponse.GetResponseStream());
                            var jsonResult = reader.ReadToEnd();
                            reader.Close();
                            response = JToken.Parse(jsonResult);
                        }
                        catch (WebException e)
                        {
                            string message = e.ResponseString();
                            if (e.HttpStatusCode() == 400 && !string.IsNullOrEmpty(message))
                            {
                                response = JToken.Parse(message);
                            }
                            else throw parseException(e);
                        }

                        uploadId = (string)response["upload_id"];
                        relativeOffset = (int)((long)response["offset"] - offset);
                        totalUploaded = offset + relativeOffset;
                        if (progress != null) progress.Report((int)(totalUploaded * 100 / fileSize));
                    } 

                    offset += bytesRead;
                }           

            }

            var baseUrl = new StringBuilder(DropboxConfig.UploadFinishUrl);
            baseUrl.Append(DropboxConfig.AccessType);
            baseUrl.Append(concatenatePath(dDestFolder.Path, name));

            var finalUrl = new ParamUrl(baseUrl.ToString());
            finalUrl.Add("upload_id", uploadId);
            var finishResponse = await queryServer(finalUrl, WebRequestMethods.Http.Post).ConfigureAwait(false);

            DropboxFileInfo file = new DropboxFileInfo() { Parent = destinationFolder };
            setupFileWithMetadata(file, JToken.Parse(finishResponse));
            destinationFolder.Contents.Add(name.ToLowerInvariant(), file);
            return file;
        }

        public async Task SynchronizeAsync()
        {
            bool hasMore = true;
            while (hasMore)
            {
                var url = new ParamUrl(DropboxConfig.DeltaUrl);
                if (!string.IsNullOrEmpty(_cursor)) url.Add("cursor", _cursor);
                string responseString = await queryServer(url, WebRequestMethods.Http.Post).ConfigureAwait(false);
                var response = JObject.Parse(responseString);

                hasMore = (bool)response["has_more"];
                // If the reset flag is received, local state should be cleared.
                if ((bool)response["reset"]) _root.Contents.Clear();
                // Set the cursor string for further queries.
                _cursor = (string)response["cursor"];

                foreach (JToken entry in (JArray)response["entries"])
                {
                    // The first element is path of the file/folder and the second is metadata.
                    // If the second element is null, file/folder does not exist anymore.
                    var path = (string)entry.First;
                    var metadata = entry.Last;
                    // When splitting the path (/folder/folder/...), the first entry will be empty.
                    var folders = path.Split('/');
                    // If the second value of the entry is not null, 
                    // then it's a metadata for new file or folder.
                    // If it is null, file/folder was deleted.
                    if (metadata.HasValues) addFile(folders, metadata);
                    else removeFile(folders);
                }
            }
        }

        public Task<DFileInfo> RenameAsync(DFileInfo file, string newName)
        {
            return copyOperation((DropboxFileInfo)file, (DropboxFileInfo)file.Parent, true, newName);
        }

        public Task<DFileInfo> MoveAsync(DFileInfo file, DFileInfo destFolder)
        {
            return copyOperation((DropboxFileInfo)file, (DropboxFileInfo)destFolder, true);
        }

        public Task<DFileInfo> CopyAsync(DFileInfo file, DFileInfo destFolder)
        {
            return copyOperation((DropboxFileInfo)file, (DropboxFileInfo)destFolder, false);
        }

        public async Task DeleteAsync(DFileInfo file)
        {
            var dFile = (DropboxFileInfo)file;
            var url = new ParamUrl(DropboxConfig.DeleteUrl);
            url.Add("root", DropboxConfig.AccessType);
            url.Add("path", percentEncoding(dFile.Path));

            await queryServer(url).ConfigureAwait(false);

            file.Parent.Contents.Remove(file.Name.ToLowerInvariant());
            file = null;
        }

        public async Task<Stream> GetDownloadStreamAsync(DFileInfo file)
        {
            if (file.IsDirectory)
                throw new NotSupportedException("Downloading a folder is not supported.");

            var dFile = (DropboxFileInfo)file;
            var url = new StringBuilder(DropboxConfig.DownloadUrl);
            url.Append(DropboxConfig.AccessType);
            url.Append(dFile.Path);
            ParamUrl queryUrl = new ParamUrl(url.ToString());

            try
            {
                var signedUrl = await signAsync(queryUrl).ConfigureAwait(false);
                var request = (HttpWebRequest)WebRequest.Create(signedUrl);
                request.Method = WebRequestMethods.Http.Get;
                var response = await request.GetResponseAsync().ConfigureAwait(false);
                return response.GetResponseStream();
            }
            catch (WebException e)
            {
                throw parseException(e);
            }
        }

        public async Task LoadInfoAsync()
        {
            var response = await queryServer(new ParamUrl(DropboxConfig.UserInfoUrl)).ConfigureAwait(false);
            Quota = (long)JObject.Parse(response)["quota_info"]["quota"];

            var metadata = await loadMetadata("/").ConfigureAwait(false);
            setupFileWithMetadata(_root, metadata);            
            await SynchronizeAsync().ConfigureAwait(false);
        }
        #endregion
    }
}
