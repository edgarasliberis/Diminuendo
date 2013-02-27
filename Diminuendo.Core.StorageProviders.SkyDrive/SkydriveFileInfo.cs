using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Diminuendo.Core.FileSystem;

namespace Diminuendo.Core.StorageProviders.SkyDrive
{
    [Serializable]
    public class SkydriveFileInfo : DFileInfo
    {
        public string Id { get; set; }
    }
}
