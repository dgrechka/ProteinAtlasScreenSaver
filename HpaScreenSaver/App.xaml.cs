using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

namespace HpaScreenSaver
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        MainWindow[] windows = new MainWindow[Screen.AllScreens.Length];

        private void ApplicationStartup(object sender, StartupEventArgs e)
        {
            if (e.Args.Length == 0 || e.Args[0].ToLower().StartsWith("/s"))
            {
                for (int i = 0; i < Screen.AllScreens.Length; i++)
                {
                    var s = Screen.AllScreens[i];
                    MainWindow window = new MainWindow();
                    window.Left = s.WorkingArea.Left;
                    window.Top = s.WorkingArea.Top;
                    window.Width = s.WorkingArea.Width;
                    window.Height = s.WorkingArea.Height;
                    window.Show();
                    windows[i] = window;
                    // System.Windows.MessageBox.Show("Activated at monitor "+i.ToString());
                }
                var initResult = HpaClient.initTask().Result;
                foreach (var window in windows)
                {
                    //window.Activate();
                    window.StartShow();
                }
            }
            else {
                Current.Shutdown();
            }
        }
    }
}