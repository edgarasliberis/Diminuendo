using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diminuendo.Core.StorageProviders.SkyDrive
{
    public static class SkydriveConfig
    {
        public const string Scopes = @"wl.offline_access wl.skydrive_update";
        public const string AuthUrlTemplate = @"https://login.live.com/oauth20_authorize.srf?client_id={0}&redirect_uri=https://login.live.com/oauth20_desktop.srf&response_type=code&scope={1}";
        public const string QuotaUrl = @"https://apis.live.net/v5.0/me/skydrive/quota";
        public const string FilesUrlTemplate = @"https://apis.live.net/v5.0/{0}/files";
        public const string FileInfoUrlTemplate = @"https://apis.live.net/v5.0/{0}";
        public const string FileContentUrlTemplate = @"https://apis.live.net/v5.0/{0}/content";
        public const string TokenUrl = @"https://login.live.com/oauth20_token.srf";
        public const string TokenRequestTemplate = @"client_id={0}&redirect_uri=https://login.live.com/oauth20_desktop.srf&client_secret={1}&code={2}&grant_type=authorization_code";
        public const string AccessTokenRequestTemplate = @"client_id={0}&redirect_uri=https://login.live.com/oauth20_desktop.srf&grant_type=refresh_token&refresh_token={1}";
        public const int ChunkSize = 4194304;
    }
}
