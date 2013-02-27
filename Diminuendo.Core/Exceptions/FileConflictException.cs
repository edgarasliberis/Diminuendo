using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Diminuendo.Core.Exceptions
{
    [Serializable]
    public class FileConflictException : Exception
    {
        public FileConflictException()
        {
        }

        public FileConflictException(string message)
            : base(message)
        {
        }

        public FileConflictException(string message, Exception innerException) :
            base(message, innerException)
        {
        }

        protected FileConflictException(SerializationInfo info,
           StreamingContext context)
            : base(info, context)
        {
        }
    }
}
