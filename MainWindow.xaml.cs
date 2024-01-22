using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Collections.ObjectModel;
using Frida;
using System.IO;

namespace HelloFrida
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DeviceManager deviceManager;

        public ObservableCollection<Device> Devices { get; private set; }
        public ObservableCollection<Process> Processes { get; private set; }
        public Session session;
        public Script script;
        public bool scriptLoaded;
        public uint pid;
        private string name;

       

        public MainWindow()
        {
            InitializeComponent();
            Devices = new ObservableCollection<Frida.Device>();
            Processes = new ObservableCollection<Frida.Process>();
            DataContext = this;
            Loaded += new RoutedEventHandler(MainWindow_Loaded);
        }

        private void RefreshAllowedActions()
        {
            deviceList.IsEnabled = session == null;
            refreshButton.IsEnabled = session == null && deviceList.SelectedItem != null;

            processList.IsEnabled = session == null;
            spawnButton.IsEnabled = deviceList.SelectedItem != null;
            resumeButton.IsEnabled = processList.SelectedItem != null || pid > 1;
            attachButton.IsEnabled = processList.SelectedItem != null && session == null;
            detachButton.IsEnabled = session != null;

            scriptSource.IsEnabled = session != null && script == null;
            createScriptButton.IsEnabled = session != null && script == null;
            loadScriptButton.IsEnabled = script != null && !scriptLoaded;
            unloadScriptButton.IsEnabled = script != null;
            //clearResultsButton.IsEnabled = script != null && scriptLoaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            deviceManager = new Frida.DeviceManager(Dispatcher);
            deviceManager.Changed += new EventHandler(deviceManager_Changed);
            RefreshDeviceList();
            RefreshAllowedActions();
            saveScriptButton.IsEnabled = false;
            listAllScripts();
        }
        private void listAllScripts()
        {
            var current_dir = Directory.GetCurrentDirectory();
            var scripts = current_dir + "\\scripts";

            bool exists = Directory.Exists(scripts);

            try
            {

                if (!exists)
                {

                    Directory.CreateDirectory(scripts);
                    MessageBox.Show("Created " + scripts + " Directory");

                }

                else
                {
                    MessageBox.Show("Reading scripts from " + scripts);
                }

                int i;
                String[] files = Directory.GetFiles(scripts);
                for (i = 0; i < files.Length; i++)
                {
                    scriptsListBox.Items.Add(files[i]);
                }
            }

            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            
            }

            
        }
        private void RefreshDeviceList()
        {
            var devices = deviceManager.EnumerateDevices();
            debugConsole.Items.Add(String.Format("Got {0} devices", devices.Length));
            Array.Sort(devices, delegate(Device a, Device b)
            {
                var aHasIcon = a.Icon != null;
                var bHasIcon = b.Icon != null;
                if (aHasIcon == bHasIcon)
                    return a.Id.CompareTo(b.Id);
                else
                    return bHasIcon.CompareTo(aHasIcon);
            });
            Devices.Clear();
            foreach (var device in devices)
                Devices.Add(device);
        }

        private void RefreshProcessList()
        {
            var device = deviceList.SelectedItem as Frida.Device;
            if (device == null)
            {
                Processes.Clear();
                return;
            }

            try
            {
                var processes = device.EnumerateProcesses(Frida.Scope.Full);
                Array.Sort(processes, delegate(Frida.Process a, Frida.Process b) {
                    var aHasIcon = a.Icons.Length != 0;
                    var bHasIcon = b.Icons.Length != 0;
                    if (aHasIcon == bHasIcon)
                        return a.Name.CompareTo(b.Name);
                    else
                        return bHasIcon.CompareTo(aHasIcon);
                });
                Processes.Clear();
                foreach (var process in processes)
                    Processes.Add(process);
            }
            catch (Exception ex)
            {
                debugConsole.Items.Add("EnumerateProcesses failed: " + ex.Message);
                Processes.Clear();
            }
        }

        private void deviceManager_Changed(object sender, EventArgs e)
        {
            debugConsole.Items.Add("DeviceManager Changed");
            RefreshDeviceList();
        }

        private void deviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshAllowedActions();
            RefreshProcessList();
        }

        private void refreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshProcessList();
        }

        private void processList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshAllowedActions();
        }

        private void spawnButton_Click(object sender, RoutedEventArgs e)
        {
            HookAMal.SpawnWindow spawnWindow = new HookAMal.SpawnWindow();
            spawnWindow.Show();
            
        }

        private void resumeButton_Click(object sender, RoutedEventArgs e)
        {
            var device = deviceList.SelectedItem as Frida.Device;
            try
            {
                
                device.Resume(pid);

            }
            catch (Exception ex)
            {
                debugConsole.Items.Add("Resume failed: " + ex.Message);
            }
        }

        private void attachButton_Click(object sender, RoutedEventArgs e)
        {
            var device = deviceList.SelectedItem as Frida.Device;
            var process = processList.SelectedItem as Frida.Process;

            try
            {
                session = device.Attach(process.Pid);
            }
            catch (Exception ex)
            {
                debugConsole.Items.Add("Attach failed: " + ex.Message);
                return;
            }
            session.Detached += new Frida.SessionDetachedHandler(session_Detached);
            debugConsole.Items.Add("Attached to " + session.Pid);
            RefreshAllowedActions();
        }

        private void detachButton_Click(object sender, RoutedEventArgs e)
        {
            session.Detach();
            session = null;
            script = null;
            RefreshAllowedActions();
        }

        public void session_Detached(object sender, Frida.SessionDetachedEventArgs e)
        {
            if (sender == session)
            {
                debugConsole.Items.Add($"Detached from Session with PID {session.Pid} ({e.Reason})");
                session = null;
                script = null;
                RefreshAllowedActions();
            }
        }

        private void createScriptButton_Click(object sender, RoutedEventArgs e)
        {
            if (script != null)
            {
                try
                {
                    script.Unload();
                }
                catch (Exception ex)
                {
                    debugConsole.Items.Add("Failed to unload previous script: " + ex.Message);
                }
                script = null;
                scriptLoaded = false;
                RefreshAllowedActions();
                resumeButton.IsEnabled = true;
            }

            try
            {
                script = session.CreateScript(scriptSource.Text);
                scriptLoaded = false;
                RefreshAllowedActions();
            }
            catch (Exception ex)
            {
                debugConsole.Items.Add("CreateScript failed: " + ex.Message);
                return;
            }
            script.Message += new Frida.ScriptMessageHandler(script_Message);
        }

        private void loadScriptButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                script.Load();
                scriptLoaded = true;
                RefreshAllowedActions();
            }
            catch (Exception ex)
            {
                debugConsole.Items.Add("Load failed: " + ex.Message);
            }
        }

        private void unloadScriptButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                script.Unload();
            }
            catch (Exception ex)
            {
                debugConsole.Items.Add("Failed to unload script: " + ex.Message);
            }
            script = null;
            scriptLoaded = false;
            RefreshAllowedActions();
        }

        private void postToScriptButton_Click(object sender, RoutedEventArgs e)
        {
            resultBox.Items.Clear();
        }

        private void script_Message(object sender, Frida.ScriptMessageEventArgs e)
        {
            if (sender == script)
            {
                resultBox.Items.Add(String.Format("{0}", e.Message));
            }
        }


        private void editScriptButton_Click(object sender, RoutedEventArgs e)
        {
            if (scriptsListBox.SelectedItems.Count == 1)
            { 
                string selected_script = scriptsListBox.SelectedItem.ToString();
                scriptSource.Clear();
                scriptSource.Text = File.ReadAllText(selected_script);
                scriptSource.IsEnabled = true;
                saveScriptButton.IsEnabled = true;
            }
        }

        private void saveScriptButton_Click(object sender, RoutedEventArgs e)
        {
            if (scriptsListBox.SelectedItems.Count == 1)
            {
                var current_dir = Directory.GetCurrentDirectory();
                var scripts_path = current_dir + "\\scripts";
                //string selected_script = scriptsListBox.SelectedItem.ToString();
                var folderBrowserDialog = new CommonOpenFileDialog();
                folderBrowserDialog.Title = "Save Custom Scripts";
                folderBrowserDialog.IsFolderPicker = false;
                folderBrowserDialog.InitialDirectory = scripts_path;
                folderBrowserDialog.AddToMostRecentlyUsedList = false;
                folderBrowserDialog.AllowNonFileSystemItems = false;
                folderBrowserDialog.DefaultDirectory = scripts_path;
                folderBrowserDialog.EnsureFileExists = false;
                folderBrowserDialog.EnsurePathExists = true;
                folderBrowserDialog.EnsureReadOnly = false;
                folderBrowserDialog.EnsureValidNames = true;
                folderBrowserDialog.Multiselect = false;
                folderBrowserDialog.ShowPlacesList = true;

                if (folderBrowserDialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    TextWriter tw = new StreamWriter(scripts_path + "custom_script.js");
                    tw.Write(scriptSource.Text);
                    tw.Close();
                    System.Windows.MessageBox.Show("Saved custom script");
                }
            }
        }

        private void saveResultstButton_Click(object sender, RoutedEventArgs e)
        {

            var current_dir = Directory.GetCurrentDirectory();
            var results_path = current_dir + "\\results";

            DateTime now = DateTime.Now;

            if (spawnButton.IsEnabled == false)
            {
                
                string str;
                
                    str = processList.SelectedItem.ToString();
                    var badChars = new string[] { "\"", ":", "," };
                    foreach (var badChar in badChars)
                    {
                        str = str.Replace(badChar, string.Empty);
                    }
                    var dir_name = System.IO.Path.Combine(results_path, str.Replace(@"\", ""));

                    Directory.CreateDirectory(dir_name);
                    var result_filename = System.IO.Path.Combine(dir_name, now.ToLongTimeString().Replace(':', '-'));
                    StreamWriter SaveFile = new StreamWriter(result_filename);
                    foreach (var item in resultBox.Items)
                    {
                        SaveFile.WriteLine(item);
                    }

                    SaveFile.Close();
                
                

                System.Windows.MessageBox.Show("Saved Results");
            }

            else 
            {
                try
                {
                    name = "random_program";

                    

                        var dir_name = System.IO.Path.Combine(results_path, name, pid.ToString());
                        Directory.CreateDirectory(dir_name);
                        var result_filename = System.IO.Path.Combine(dir_name, now.ToLongTimeString().Replace(':', '-'));
                        StreamWriter SaveFile = new StreamWriter(result_filename);
                        foreach (var item in resultBox.Items)
                        {
                            SaveFile.WriteLine(item);
                        }

                        SaveFile.Close();
                    System.Windows.MessageBox.Show("Saved Results");

                }
                catch { }

            }
        }

        private void clearResultsButton_Click(object sender, RoutedEventArgs e)
        {
            resultBox.Items.Clear();
        }
    }
}
