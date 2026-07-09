using System;
using System.Windows.Forms;
using ObsPortableStreamDeckInstaller;

namespace ObsPortableStreamDeckInstaller;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}