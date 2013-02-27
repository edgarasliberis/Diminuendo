using Diminuendo.Core.Helpers;
using Diminuendo.Core.FileSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Diminuendo.Core.StorageProviders.Dropbox
{
    [Serializable]
    public class DropboxFileInfo : DFileInfo
    {
        private static char[] invalidPathChars = { '\\', ':', '?', '*', '<', '>', '"', '|' };

        /// <summary>
        /// File's path. Must be in the following format: \"/folder1/.../name(.ext)\".
        /// </summary>
        public string Path
        {
            get
            {
                var parent = (DropboxFileInfo)Parent;
                if(parent == null) return "/";
                else return DropboxClient.concatenatePath(parent.Path, Name);
            }
        }

        public string Hash { get; set; }
    }
}
