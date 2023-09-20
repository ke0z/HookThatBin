using Frida;
using HelloFrida;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace HookAMal
{
    /// <summary>
    /// Interaction logic for SpawnWindow.xaml
    /// </summary>
    public partial class SpawnWindow : System.Windows.Window
    {


        public SpawnWindow()
        {
            InitializeComponent();
        }


        private void btnSpawnProg_Click(object sender, RoutedEventArgs e)
        {
            foreach (System.Windows.Window window in Application.Current.Windows)
            {
                if (window.GetType() == typeof(MainWindow))
                {
                    var winWin = (window as MainWindow);
                    var device = winWin.deviceList.SelectedItem as Frida.Device;
                    try
                    {
                        //device.Spawn(txtBoxProgram.Text, new string[] { String.Format(txtBoxProgram.Text), txtBoxArgs.Text }, null, null, null);
                        winWin.pid = device.Spawn(txtBoxProgram.Text, new string[] { String.Format(txtBoxProgram.Text), txtBoxArgs.Text }, null, null, null);
                        winWin.session = device.Attach(winWin.pid);
                        //device.Resume(pid);
                        winWin.session.Detached += new Frida.SessionDetachedHandler(winWin.session_Detached);
                        winWin.debugConsole.Items.Add("Attached to " + winWin.session.Pid);
                        winWin.createScriptButton.IsEnabled = winWin.session != null && winWin.script == null;
                        winWin.loadScriptButton.IsEnabled = winWin.script != null && !winWin.scriptLoaded;
                        winWin.unloadScriptButton.IsEnabled = winWin.script != null;
                        winWin.resumeButton.IsEnabled = winWin.pid >= 1;
                        winWin.Processes.Clear();
                    }
                    catch (Exception ex)
                    {
                        winWin.debugConsole.Items.Add("Spawn failed: " + ex.Message);
                    }
                    
                }
            }
            this.Close();
        }

    }
}
