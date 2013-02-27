using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Diminuendo.Core.Helpers;

namespace Diminuendo.Core.StorageProviders.Dropbox.OAuth
{
    [Serializable]
    public class OAuthManager
    {
        private Random random = new Random();
        private string consumerKey, consumerSecret;

        public OAuthManager(string key, string secret)
        {
            consumerKey = key;
            consumerSecret = secret;
        }

        public string Sign(ParamUrl requestUrl, OAuthToken authToken)
        {
            string timestamp = timeStamp();
            string nonce = random.Next(0, int.MaxValue).ToString();
            string token = (authToken == null)? String.Empty : authToken.Token;
            string secret = (authToken == null)? String.Empty : authToken.Secret;
            string signature = string.Format("{0}&{1}", consumerSecret, secret);

            requestUrl.Add("oauth_consumer_key", consumerKey);
            requestUrl.Add("oauth_nonce", nonce);
            requestUrl.Add("oauth_timestamp", timestamp);
            requestUrl.Add("oauth_signature_method", "PLAINTEXT");
            requestUrl.Add("oauth_version", "1.0");
            requestUrl.Add("oauth_signature", Uri.EscapeDataString(signature));
            if (!String.IsNullOrEmpty(token)) requestUrl.Add("oauth_token", token);

            return requestUrl.ToString();
        }

        private static string timeStamp()
        {
            TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return Convert.ToInt64(ts.TotalSeconds).ToString();
        }
    }
}
