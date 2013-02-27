using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Diminuendo.Core.Exceptions
{
    [Serializable]
    public class InsufficientPermissionsException : Exception
    {
        public InsufficientPermissionsException()
        {
        }

        public InsufficientPermissionsException(string message)
            : base(message)
        {
        }

        public InsufficientPermissionsException(string message, Exception innerException) :
            base(message, innerException)
        {
        }

        protected InsufficientPermissionsException(SerializationInfo info,
           StreamingContext context)
            : base(info, context)
        {
        }
    }  
}
