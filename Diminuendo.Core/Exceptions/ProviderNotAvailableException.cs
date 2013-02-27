using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Diminuendo.Core.Exceptions
{
    [Serializable]
    public class ProviderNotAvailableException : Exception
    {
        public ProviderNotAvailableException()
        {
        }

        public ProviderNotAvailableException(string message)
            : base(message)
        {
        }

        public ProviderNotAvailableException(string message, Exception innerException) :
            base(message, innerException)
        {
        }

        protected ProviderNotAvailableException(SerializationInfo info,
           StreamingContext context)
            : base(info, context)
        {
        }
    }
}