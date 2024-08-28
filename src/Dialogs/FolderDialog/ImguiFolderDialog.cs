using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Toolbox.Core;

namespace MapStudio.UI
{
    public class ImguiFolderDialog
    {
        public string Title { get; set; } = "Folder Select";

        public string SelectedPath { get; set; } = "";

        public bool ShowDialog()
        {
            string ofd = null;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                FolderBrowserEx.FolderBrowserDialog dialog = new FolderBrowserEx.FolderBrowserDialog() { Title = Title, InitialFolder = GlobalSettings.Current.CachedFolderSelectPath };
                dialog.ShowDialog();
                ofd = dialog.SelectedFolder;
            }
            else
            {
                ofd = TinyFileDialog.SelectFolderDialog(Title, SelectedPath);
            }


            if (!string.IsNullOrEmpty(ofd))
            {
                SelectedPath = ofd;

                // Cache Path for next pick
                GlobalSettings.Current.CachedFolderSelectPath = ofd;
                GlobalSettings.Current.Save();
                return true;
            }

            return false;
        }
    }
}
