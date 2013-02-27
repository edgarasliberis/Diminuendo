using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Diminuendo.Core.StorageProviders.Dropbox
{
    internal static class DropboxConfig
    {
        public const string AccessType = "dropbox";
        public const string RequestTokenUrl = @"https://api.dropbox.com/1/oauth/request_token";
        public const string AccessTokenUrl = @"https://api.dropbox.com/1/oauth/access_token";
        public const string AuthUrl = @"https://www.dropbox.com/1/oauth/authorize?oauth_token=";
        public const string UserInfoUrl = @"https://api.dropbox.com/1/account/info";
        public const string MetadataUrl = @"https://api.dropbox.com/1/metadata/";
        public const string CreateFolderUrl = @"https://api.dropbox.com/1/fileops/create_folder";
        public const string MoveUrl = @"https://api.dropbox.com/1/fileops/move";
        public const string CopyUrl = @"https://api.dropbox.com/1/fileops/copy";
        public const string DeleteUrl = @"https://api.dropbox.com/1/fileops/delete";
        public const string DownloadUrl = @"https://api-content.dropbox.com/1/files/";
        public const string UploadUrl = @"https://api-content.dropbox.com/1/chunked_upload";
        public const string UploadFinishUrl = @"https://api-content.dropbox.com/1/commit_chunked_upload/";
        public const string DeltaUrl = @"https://api.dropbox.com/1/delta";
        // 4MB chunk for Dropbox uploads.
        public const int ChunkSize = 4194304;
    }
}
