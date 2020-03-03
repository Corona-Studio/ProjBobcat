using ProjBobcat.Class.Helper;
using ProjBobcat.Interface;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjBobcat.DefaultComponent.Launch
{
    public class DefaultMinecraftUWPCore
    {
        public static void LaunchMinecraftUWP()
        {
            if (SystemInfoHelper.IsMinecraftUWPInstalled() == false)
            {
                throw new InvalidOperationException();
            }
            using (Process process = new Process())
            {
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.FileName = "minecraft:";
                process.Exited += GameExit;
                process.Start();
            }
        }

        private static void GameExit(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        #region IDisposable Support

        // Dispose() calls Dispose(true)
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // NOTE: Leave out the finalizer altogether if this class doesn't
        // own unmanaged resources, but leave the other methods
        // exactly as they are.
        ~DefaultGameCore()
        {
            // Finalizer calls Dispose(false)
            Dispose(false);
        }

        // The bulk of the clean-up code is implemented in Dispose(bool)
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                VersionLocator = null;
            }
        }

        #endregion
    }
}
