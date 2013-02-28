using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Diminuendo.Core.FileSystem;
using Diminuendo.Core.Helpers;
using Diminuendo.Core.Exceptions;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace Diminuendo.Core.StorageProviders.SkyDrive
{
    [Serializable]
    public class SkydriveClient : IStorageProvider
    {
        #region Private fields
        private string _accessToken, _refreshToken, _clientID, _clientSecret;
        private SkydriveFileInfo _root;
        #endregion

        #region Properties
        public string Name { get; set; }
        public long Quota { get; private set; }
        public DFileInfo Root { get { return _root; } }
        #endregion

        #region Public methods
        /// <summary>
        /// Provides the plug-in with necessary keys to make API requests.
        /// </summary>
        /// <param name="clientID">Client ID obtained from Live Dev.</param>
        /// <param name="clientSecret">Client secret obtained from Live DEV.</param>
        public void SupplyAppKey(string clientID, string clientSecret)
        {
            _clientID = clientID;
            _clientSecret = clientSecret;
        }

        /// <summary>
        /// Returns an URL for user to visit and allow the application to use their SkyDrive.
        /// </summary>
        public Uri AuthUrl()
        {
            return new Uri(
                string.Format(SkydriveConfig.AuthUrlTemplate, _clientID, SkydriveConfig.Scopes));
        }

        /// <summary>
        /// Sets the access token for provider plug-in to use.
        /// </summary>
        /// <param name="token">Token obtained from Live Connect.</param>
        public void SupplyAccessToken(string token)
        {
            _accessToken = token;
        }

        /// <summary>
        /// Gets the access token providing that user has granted 
        /// the permission and refresh token is present.
        /// </summary>
        public async Task FetchAccessTokenAsync()
        {
            // We make a request to the server with refresh token
            // and get new access and refresh tokens.
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(SkydriveConfig.TokenUrl);
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            using (var writer = 
                new StreamWriter(await request.GetRequestStreamAsync().ConfigureAwait(false)))
            {
                string data = 
                    string.Format(SkydriveConfig.AccessTokenRequestTemplate, _clientID, _refreshToken);
                writer.Write(data);
            }

            string respString;
            try
            {
                var response = await request.GetResponseAsync().ConfigureAwait(false);
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    respString = await reader.ReadToEndAsync().ConfigureAwait(false);
                }
            }
            catch (WebException e)
            {
                throw parseException(e);
            }

            var parsedJson = JObject.Parse(respString);
            _refreshToken = (string)parsedJson["refresh_token"];
            _accessToken = (string)parsedJson["access_token"];
        }

        /// <summary>
        /// Exchanges code from Live Connect to access and refresh tokens.
        /// </summary>
        /// <param name="code">Code obtained from Live Connect.</param>
        public async Task SupplyCodeAsync(string code)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(SkydriveConfig.TokenUrl);
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            using (var writer = new StreamWriter(await request.GetRequestStreamAsync().ConfigureAwait(false)))
            {
                string data = string.Format(SkydriveConfig.TokenRequestTemplate, 
                    _clientID, _clientSecret, code);
                writer.Write(data);
            }

            var response = await request.GetResponseAsync().ConfigureAwait(false);
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                string respString = await reader.ReadToEndAsync().ConfigureAwait(false);
                var parsedJson = JObject.Parse(respString);
                if ((string)parsedJson["scope"] != SkydriveConfig.Scopes)
                    throw new InsufficientPermissionsException("Permissions for all scopes should be granted.");
                _refreshToken = (string)parsedJson["refresh_token"];
                _accessToken = (string)parsedJson["access_token"];
            }
        }


        #endregion

        #region Synchronous counterparts
        /// <summary>
        /// Exchanges code from Live Connect to access and refresh tokens.
        /// </summary>
        /// <param name="code">Code obtained from Live Connect.</param>
        public void SupplyCode(string code)
        {
            this.SupplyCodeAsync(code).Wait();
        }

        /// <summary>
        /// Gets the access token providing that user has granted 
        /// the permission and refresh token is present.
        /// </summary>
        public void FetchAccessToken()
        {
            this.FetchAccessTokenAsync().Wait();
        }
        #endregion

        #region Private helpers
        private string sign(string url)
        {
            return string.Format("{0}?access_token={1}", url, _accessToken);
        }

        private async Task<string> queryServer(string url, string method = WebRequestMethods.Http.Get)
        {
            bool authNeeded = false;
        Begin:
            string result = null;
            try
            {
                try
                {
                    result = await Http.ResponseToAsync(sign(url), method).ConfigureAwait(false);
                    authNeeded = false;
                }
                catch (WebException e)
                {
                    throw parseException(e);
                }
            }
            catch (AuthorizationFailureException e)
            {
                if (authNeeded) throw e;
                authNeeded = true;
            }

            if (authNeeded)
            {
                await FetchAccessTokenAsync().ConfigureAwait(false);
                goto Begin;
            }
            return result;
        }

        private Exception parseException(WebException e)
        {
            var code = e.HttpStatusCode();
            var message = e.ResponseString();

            JToken json = null;
            try { json = JToken.Parse(message); }
            catch (Exception) { }

            if(json != null && json["error"] != null)
            {
                message = (string)json["error"]["message"] + " (" + (string)json["error"]["code"] + ")";
            }

            if (code == 401) return new AuthorizationFailureException(message, e);
            return e;
        }

        private async Task<JToken> getMetadataFor(SkydriveFileInfo file)
        {
            // SkyDrive's entry metadata is discussed at:
            // http://msdn.microsoft.com/en-us/library/live/hh243648.aspx
            string url = string.Format(SkydriveConfig.FileInfoUrlTemplate, file.Id);
            string response = await queryServer(url).ConfigureAwait(false);
            return JToken.Parse(response);
        }

        private async Task<JArray> getFolderContentsMetadata(SkydriveFileInfo folder)
        {
            var url = string.Format(SkydriveConfig.FilesUrlTemplate, folder.Id);
            var response = await queryServer(url).ConfigureAwait(false);
            return (JArray)JToken.Parse(response)["data"];
        }

        private void setupFileWithMetadata(SkydriveFileInfo file, JToken metadata)
        {
            string type = (string)metadata["type"];
            file.IsDirectory = (type == "folder") || (type == "album");
            file.Name = (string)metadata["name"];
            file.Provider = this;
            file.Id = (string)metadata["id"];
            file.Size = (long)(metadata["size"] ?? 0);
        }

        private async Task<string> dataToServer(string data, string requestUri, string method = WebRequestMethods.Http.Get)
        {
            bool authNeeded = false;
        Begin:    
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestUri);
            request.Method = method;
            request.Headers["Authorization"] = "BEARER " + _accessToken;
            request.ContentType = "application/json";
            using (var writer = new StreamWriter(await request.GetRequestStreamAsync().ConfigureAwait(false)))
            {
                writer.Write(data);
            }

            string result = null;        
            try
            {
                try
                {
                    var response = await request.GetResponseAsync().ConfigureAwait(false);
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        result = await reader.ReadToEndAsync().ConfigureAwait(false);
                    }
                    authNeeded = false;
                }
                catch (WebException e)
                {
                    throw parseException(e);
                }
            }
            catch (AuthorizationFailureException e)
            {
                if (authNeeded) throw e;
                authNeeded = true;
            }

            if (authNeeded)
            {
                await FetchAccessTokenAsync();
                goto Begin;
            }

            return result;
        }

        private async Task<DFileInfo> fileOperation(SkydriveFileInfo file, SkydriveFileInfo destinationFolder, string operation)
        {
            if (destinationFolder.Contents.ContainsKey(file.Id))
                throw new FileConflictException("File or folder already exists here.");

            var sFile = (SkydriveFileInfo)file;
            var destFolder = (SkydriveFileInfo)destinationFolder;

            var url = string.Format(SkydriveConfig.FileInfoUrlTemplate, sFile.Id);
            var data = new JObject();
            data.Add("destination", destFolder.Id);

            var response = await dataToServer(data.ToString(), url, "MOVE").ConfigureAwait(false);
            var metadata = JToken.Parse(response);

            if (operation == "MOVE")
            {
                sFile.Parent.Contents.Remove(sFile.Id);
                sFile.Parent = destFolder;
                setupFileWithMetadata(sFile, metadata);
                destFolder.Contents.Add(sFile.Id, sFile);
                return sFile;
            }
            else
            {
                var newFile = (SkydriveFileInfo)file.Clone();
                newFile.Parent = destFolder;
                setupFileWithMetadata(newFile, metadata);
                destFolder.Contents.Add(newFile.Id, newFile);
                return newFile;
            }
        }
        #endregion

        #region IStorageProvider implementation

        public async Task LoadInfoAsync()
        {
            var quotaJson = await queryServer(SkydriveConfig.QuotaUrl).ConfigureAwait(false);
            Quota = (long)JToken.Parse(quotaJson)["quota"];
            Name = "SkyDrive";

            _root = new SkydriveFileInfo()
            {
                Id = "me/skydrive",
                Parent = null
            };

            var metadata = await getMetadataFor(_root).ConfigureAwait(false);
            setupFileWithMetadata(_root, metadata);
        }

        public async Task NavigatedToAsync(DFileInfo file)
        {
            if (!file.IsDirectory) return;
            var contents = await getFolderContentsMetadata((SkydriveFileInfo)file).ConfigureAwait(false);
            file.Contents.Clear();
            foreach (JToken entry in contents)
            {
                var newFile = new SkydriveFileInfo() { Parent = file };
                setupFileWithMetadata(newFile, entry);
                file.Contents.Add(newFile.Id, newFile);
            }
        }

        public async Task<DFileInfo> CreateFolderAsync(DFileInfo destinationFolder, string name)
        {
            var dest = (SkydriveFileInfo)destinationFolder;
            var url = string.Format(SkydriveConfig.FileInfoUrlTemplate, dest.Id);
            var data = "{ \"name\" : \"" + name + "\" }";
            string response = await dataToServer(data, url, "POST").ConfigureAwait(false);

            var newFolder = new SkydriveFileInfo() { Parent = destinationFolder };
            setupFileWithMetadata(newFolder, JToken.Parse(response));
            return newFolder;
        }

        public async Task<DFileInfo> UploadFileAsync(DFileInfo destinationFolder, string name, Stream stream, 
            CancellationToken cancellationToken, IProgress<int> progress, long fileSize)
        {
            var dest = (SkydriveFileInfo)destinationFolder;
            var url = sign(string.Format(SkydriveConfig.FilesUrlTemplate, dest.Id) + '/' + name) + "&overwrite=ChooseNewName";

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "PUT";
            if (progress != null)
            {
                request.ContentLength = fileSize;
                request.AllowWriteStreamBuffering = false;
            }

            try
            {
                using (var reqStream = await request.GetRequestStreamAsync())
                {
                    await stream.CopyToAsync(reqStream, SkydriveConfig.ChunkSize, cancellationToken, progress, fileSize);
                }

                var webResponse = await request.GetResponseAsync();
                using (var reader = new StreamReader(webResponse.GetResponseStream()))
                {
                    var response = await reader.ReadToEndAsync().ConfigureAwait(false);
                    var metadata = JToken.Parse(response);
                    var newFile = new SkydriveFileInfo() { Parent = destinationFolder };
                    setupFileWithMetadata(newFile, metadata);
                    dest.Contents.Add(newFile.Id, newFile);
                    return newFile;
                }
            }
            catch (WebException e)
            {
                throw parseException(e);
            }
        }

        public async Task<Stream> GetDownloadStreamAsync(DFileInfo file)
        {
            if (file.IsDirectory)
                throw new NotSupportedException("Downloading a folder is not supported");

            string url = string.Format(SkydriveConfig.FileContentUrlTemplate, ((SkydriveFileInfo)file).Id);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(sign(url));
            request.Method = WebRequestMethods.Http.Get;

            try
            {
                var response = await request.GetResponseAsync().ConfigureAwait(false);
                return response.GetResponseStream();
            }
            catch (WebException e)
            {
                throw parseException(e);
            }
        }

        public Task SynchronizeAsync()
        {
            return Task.Factory.StartNew(() => { });
        }

        public async Task<DFileInfo> RenameAsync(DFileInfo file, string newName)
        {
            var sFile = (SkydriveFileInfo)file;

            var url = string.Format(SkydriveConfig.FileInfoUrlTemplate, sFile.Id);
            var data = "{ \"name\": \"" + newName + "\" }";

            var response = await dataToServer(data, url, "PUT").ConfigureAwait(false);
            var metadata = JToken.Parse(response);

            setupFileWithMetadata(sFile, metadata);
            return sFile;
        }

        public Task<DFileInfo> MoveAsync(DFileInfo file, DFileInfo destinationFolder)
        {
            return fileOperation((SkydriveFileInfo)file, (SkydriveFileInfo)destinationFolder, "MOVE");
        }

        public Task<DFileInfo> CopyAsync(DFileInfo file, DFileInfo destinationFolder)
        {
            return fileOperation((SkydriveFileInfo)file, (SkydriveFileInfo)destinationFolder, "COPY");
        }

        public async Task DeleteAsync(DFileInfo file)
        {
            var sFile = (SkydriveFileInfo)file;
            var url = string.Format(SkydriveConfig.FileInfoUrlTemplate, sFile.Id);
            // Server returns empty response which is no interest.
            await queryServer(url, "DELETE");

            if (sFile.Parent != null)
            {
                sFile.Parent.Contents.Remove(sFile.Id);
            }
            file = null;
        }

        #endregion  
    }
}
