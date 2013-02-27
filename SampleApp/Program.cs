using Diminuendo.Core;
using Diminuendo.Core.StorageProviders.Dropbox;
using System;
using System.Diagnostics;

namespace SampleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var manager = new DManager();

            // Setup new Dropbox plug-in.
            var dropbox = new DropboxClient();
            dropbox.SupplyAppKey("(key)", "(secret)");
            Uri authSite = dropbox.AuthUrl();
            Console.WriteLine("Please allow application access and press Enter to continue...");
            Process.Start(authSite.ToString());
            Console.ReadLine();

            manager.Load(dropbox);
            
            // We can use same reference to the provider plug-in.
            var file = dropbox.Root.Find("myfile.zip");
            file.Delete();
        }
    }
}
