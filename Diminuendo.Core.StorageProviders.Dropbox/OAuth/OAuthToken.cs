using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diminuendo.Core.StorageProviders.Dropbox.OAuth
{
    [Serializable]
    public class OAuthToken
    {
        public string Token { get; set; }
        public string Secret { get; set; }
        public OAuthToken(string token, string secret)
        {
            Token = token;
            Secret = secret;
        }
    }
}
