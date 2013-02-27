using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Diminuendo.Core.Helpers
{
    public static class Http
    {
        /// <summary>
        /// Makes a GET request to a specified URI.
        /// </summary>
        /// <param name="requestUri">An URI to make request to.</param>
        /// <returns>Server response.</returns>
        public static async Task<string> ResponseToAsync(string requestUri, string method = WebRequestMethods.Http.Get)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestUri);
            request.Method = method;
            var response = await request.GetResponseAsync();
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                return await reader.ReadToEndAsync();
            }
        }

        /// <summary>
        /// Parses response of type "key1=value1&amp;key2=value2..." into a dictionary.
        /// </summary>
        /// <param name="response">A string to parse.</param>
        /// <returns>Key-value dictionary with parsed values.</returns>
        public static Dictionary<string, string> ParseResponse(string response)
        {
            var dict = new Dictionary<string, string>();
            string[] parts = response.Split('&');
            foreach (string str in parts)
            {
                int pos = str.IndexOf('=');
                dict[str.Substring(0, pos)] = str.Substring(pos + 1);
            }
            return dict;
        }

        /// <summary>
        /// Extracts HTTP status code from an exception.
        /// </summary>
        /// <param name="exception">Exception to parse.</param>
        /// <returns>HTTP status code or null, if no code is available.</returns>
        public static Nullable<int> HttpStatusCode(this WebException exception)
        {
            if (exception.Status == WebExceptionStatus.ProtocolError)
            {
                HttpWebResponse response = exception.Response as HttpWebResponse;
                if(response != null)
                    return (int)response.StatusCode;
            }
            return null;
        }

        /// <summary>
        /// Returns the server's response for exception.
        /// </summary>
        /// <param name="exception">Exception to parse.</param>
        /// <returns>Server response.</returns>
        public static string ResponseString(this WebException exception)
        {
            if (exception.Response == null) return string.Empty;
            StreamReader reader = new StreamReader(exception.Response.GetResponseStream());
            string result = reader.ReadToEnd();
            reader.Close();
            return result;
        }
    }
}
