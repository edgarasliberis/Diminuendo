using System;
using System.Runtime.Serialization;

namespace Diminuendo.Core.Exceptions
{
    [Serializable]
    public class ProviderNotSetupException : Exception
    {
        public ProviderNotSetupException()
        {
        }

        public ProviderNotSetupException(string message)
            : base(message)
        {
        }

        public ProviderNotSetupException(string message, Exception innerException) :
            base(message, innerException)
        {
        }

        protected ProviderNotSetupException(SerializationInfo info,
           StreamingContext context)
            : base(info, context)
        {
        }
    }
}