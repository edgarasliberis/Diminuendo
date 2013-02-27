using System;
using System.Runtime.Serialization;

namespace Diminuendo.Core.Exceptions
{
    [Serializable]
    public class AuthorizationFailureException : Exception
    {
        public AuthorizationFailureException()
        {
        }

        public AuthorizationFailureException(string message)
            : base(message)
        {
        }

        public AuthorizationFailureException(string message, Exception innerException) :
            base(message, innerException)
        {
        }

        protected AuthorizationFailureException(SerializationInfo info,
           StreamingContext context)
            : base(info, context)
        {
        }
    }  
}
