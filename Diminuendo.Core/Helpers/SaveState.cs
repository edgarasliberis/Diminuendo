using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace Diminuendo.Core.Helpers
{
    public static class SaveState
    {
        public static void Serialize(Object value, Stream serializationStream)
        {
            IFormatter formatter = new BinaryFormatter();
            formatter.Serialize(serializationStream, value);
        }

        public static T Deserialize<T>(Stream sourceStream)
        {
            IFormatter formatter = new BinaryFormatter();
            return (T)formatter.Deserialize(sourceStream);
        }
    }
}
