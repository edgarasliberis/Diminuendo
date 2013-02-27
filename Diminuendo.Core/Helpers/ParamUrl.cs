using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diminuendo.Core.Helpers
{
    public class ParamUrl
    {
        public string Base { get; set; }
        private NameValueCollection parameters;

        public ParamUrl(string baseUrl)
        {
            Base = baseUrl;
            parameters = new NameValueCollection();
        }

        public void Add(string key, string value)
        {
            parameters.Add(key, value);
        }

        public override string ToString()
        {
            StringBuilder result = new StringBuilder(Base);
            for (int i = 0; i < parameters.Count; ++i)
            {
                result.Append(i == 0? '?' : '&');
                result.AppendFormat("{0}={1}", parameters.GetKey(i), parameters.Get(i));
            }
            return result.ToString();
        }
    }
}
