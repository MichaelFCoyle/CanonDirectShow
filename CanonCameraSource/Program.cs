using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WinFormsUI
{
    static class Program
    {
        /// <summary>
        /// Der Haupteinstiegspunkt für die Anwendung.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Register("CanonCaptureFilter.dll");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            Unregister("CanonCaptureFilter.dll");
        }

        private static bool Register(string name)
        {
            try
            {
                // get the current directory
                var root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var path = Path.Combine(root, name);
                Assembly asm = Assembly.LoadFile(path);
                RegistrationServices regAsm = new RegistrationServices();
                return regAsm.RegisterAssembly(asm, AssemblyRegistrationFlags.SetCodeBase);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error registering assembly:\r\n{0}", ex);
                return false;
            }
        }

        private static bool Unregister(string name)
        {
            try
            {
                // get the current directory
                var root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var path = Path.Combine(root, name);
                Assembly asm = Assembly.LoadFile(path);
                RegistrationServices regAsm = new RegistrationServices();
                return regAsm.UnregisterAssembly(asm);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error unregistering assembly:\r\n{0}", ex);
                return false;
            }

        }
    }
}
