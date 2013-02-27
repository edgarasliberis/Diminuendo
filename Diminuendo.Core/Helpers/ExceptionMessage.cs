using Diminuendo.Core.FileSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diminuendo.Core.Helpers
{
    public static class ExceptionMessage
    {
        public static string IsNullOrInvalid(string what)
        {
            return string.Format("'{0}' is null or has invalid value.", what);
        }

        public static string InvalidChars(string what, char[] bannedChars)
        {
            StringBuilder str = new StringBuilder();
            str.AppendFormat("'{0}' contains one or more invalid characters:", what);
            foreach (var ch in bannedChars)
            {
                str.Append(' ');
                str.Append(ch);
            }
            return str.ToString();
        }

        public static string ReadOnly = @"This file or directory is read only.";
    }
}
