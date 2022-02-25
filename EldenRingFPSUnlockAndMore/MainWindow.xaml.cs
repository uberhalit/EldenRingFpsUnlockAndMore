using System;
using System.Windows;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Media;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.ServiceProcess;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace EldenRingFPSUnlockAndMore
{
    public partial class MainWindow : Window
    {
        internal static string _path_logs;
        internal Process _gameProc;
        internal IntPtr _gameHwnd = IntPtr.Zero;
        internal IntPtr _gameAccessHwnd = IntPtr.Zero;
        internal static IntPtr _gameAccessHwndStatic;
        internal long _offset_framelock = 0x0;

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// On window loaded.
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _path_logs = Path.Combine(local, "Elden Ring FPS Unlocker", "logs.log");

            // Properties.Settings.Default.FrameLock = "144";

            var mutex = new Mutex(true, "ErFpsUnlockAndMore", out bool isNewInstance);
            if (!isNewInstance)
            {
                MessageBox.Show("Another instance is already running!", "Elden Ring FPS Unlocker and more", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                Environment.Exit(0);
            }
            GC.KeepAlive(mutex);

            CheckGame();
        }

        private async void CheckGame(bool startUp = true)
        {
            // check for game
            Process[] procList = Process.GetProcessesByName(GameData.PROCESS_NAME);
            if (procList.Length > 0)
            {
                MessageBoxResult result = MessageBox.Show("Game is already running!\n\n" +
                                                          "Do you want to close and restart it in offline mode without EAC?", "Elden Ring FPS Unlocker and more", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Cancel)
                    Environment.Exit(0);
                if (result == MessageBoxResult.No)
                {
                    bStart.IsEnabled = false;
                    OpenGame();
                    return;
                }
                else if (result == MessageBoxResult.Yes)
                {
                    foreach (Process proc in procList)
                    {
                        proc.Kill();
                        proc.WaitForExit(3000);
                        proc.Close();
                    }
                    await Task.Delay(2000);
                    ServiceController sc = new ServiceController("EasyAntiCheat");
                    if (sc.Status != ServiceControllerStatus.Stopped && sc.Status != ServiceControllerStatus.StopPending)
                        sc.Stop();
                    await Task.Delay(2000);
                    SafeStartGame();
                    return;
                }
            }
            if (!startUp)
                SafeStartGame();
        }

        /// <summary>
        /// Kill any running game instance and restart the game in offline mode without EAC.
        /// </summary>
        private async void SafeStartGame()
        {
            UpdateStatus("Starting game...", Brushes.Orange);

            // get game path
            string gamePath = GetApplicationPath("ELDEN RING");
            if (gamePath == null || !File.Exists(Path.Combine(gamePath, "GAME", "eldenring.exe")))
            {
                MessageBox.Show("Couldn't find game installation path!", "Elden Ring FPS Unlocker and more", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                Environment.Exit(0);
            }

            // create steam_appid
            try
            {
                File.WriteAllText(Path.Combine(gamePath, "GAME", "steam_appid.txt"), "1245620");
            }
            catch
            {
                MessageBox.Show("Couldn't write steam id file!", "Elden Ring FPS Unlocker and more", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                Environment.Exit(0);
            }

            Process[] procList = Process.GetProcessesByName("steam");
            if (procList.Length == 0)
            {
                ProcessStartInfo startInfo2 = new ProcessStartInfo
                {
                    WindowStyle = ProcessWindowStyle.Minimized,
                    Verb = "open",
                    FileName = "steam://open/console",
                };
                Process process2 = new Process
                {
                    StartInfo = startInfo2
                };
                process2.Start();
                await Task.Delay(6000);
            }

            // start the game
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                Verb = "runas",
                FileName = "cmd.exe",
                WorkingDirectory = Path.Combine(gamePath, "GAME"),
                Arguments = $"/C \"eldenring.exe -noeac\""
            };
            Process process = new Process
            {
                StartInfo = startInfo
            };
            process.Start();
            await Task.Delay(5000);

            OpenGame();
        }

        private void OpenGame()
        {
            UpdateStatus("Accessing game...", Brushes.Orange);
            Process[] procList = Process.GetProcessesByName(GameData.PROCESS_NAME);
            if (procList.Length != 1)
            {
                MessageBox.Show("Couldn't start game correctly!", "Elden Ring FPS Unlocker and more", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                Environment.Exit(0);
            }
            _gameProc = procList[0];

            // open game
            _gameHwnd = _gameProc.MainWindowHandle;
            _gameAccessHwnd = WinAPI.OpenProcess(WinAPI.PROCESS_ALL_ACCESS, false, (uint)_gameProc.Id);
            _gameAccessHwndStatic = _gameAccessHwnd;
            if (_gameHwnd == IntPtr.Zero || _gameAccessHwnd == IntPtr.Zero || _gameProc.MainModule.BaseAddress == IntPtr.Zero)
            {
                MessageBox.Show("Couldn't gain access to game process!", "Elden Ring FPS Unlocker and more", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                Environment.Exit(0);
            }
            UpdateStatus("Game init...", Brushes.Orange);
            bPatch.IsEnabled = true;

            PatternScan patternScan = new PatternScan(_gameAccessHwnd, _gameProc.MainModule);

            _offset_framelock = patternScan.FindPattern(GameData.PATTERN_FRAMELOCK) + GameData.PATTERN_FRAMELOCK_OFFSET;
            Debug.WriteLine("fFrameTick found at: 0x" + _offset_framelock.ToString("X"));
            if (!IsValidAddress(_offset_framelock))
                _offset_framelock = 0x0;
            else
                cbFramelock.IsEnabled = true;

            UpdateStatus("ready...", Brushes.Green);
        }

        /// <summary>
        /// Patch game memory.
        /// </summary>
        private void PatchGame()
        {
            PatchFramelock();
        }

        /// <summary>
        /// Patch the game's frame rate lock.
        /// </summary>
        private bool PatchFramelock()
        {
            if (!cbFramelock.IsEnabled || _offset_framelock == 0x0) return false;
            if (cbFramelock.IsChecked == true)
            {
                int fps = -1;
                bool isNumber = Int32.TryParse(tbFramelock.Text, out fps);
                if (fps < 1 || !isNumber)
                {
                    tbFramelock.Text = "60";
                    fps = 60;
                }
                else if (fps > 1 && fps < 30)
                {
                    tbFramelock.Text = "30";
                    fps = 30;
                }
                else if (fps > 300)
                {
                    tbFramelock.Text = "300";
                    fps = 300;
                }

                float deltaTime = (1000f / fps) / 1000f;
                WriteBytes(_offset_framelock, BitConverter.GetBytes(deltaTime));
            }
            else if (cbFramelock.IsChecked == false)
            {
                float deltaTime = (1000f / 60) / 1000f;
                WriteBytes(_offset_framelock, BitConverter.GetBytes(deltaTime));
            }

            UpdateStatus("Game patched!", Brushes.Green);
            return true;
        }

        /// <summary>
        /// Gets the install path of an application.
        /// </summary>
        /// <param name="p_name">The full name of the application from control panel.</param>
        /// <returns></returns>
        public static string GetApplicationPath(string p_name)
        {
            string displayName;
            string installDir;
            RegistryKey key;

            // search in: CurrentUser
            key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            foreach (string keyName in key.GetSubKeyNames())
            {
                RegistryKey subkey = key.OpenSubKey(keyName);
                displayName = subkey.GetValue("DisplayName") as string;
                if (p_name.Equals(displayName, StringComparison.OrdinalIgnoreCase) == true)
                {
                    installDir = subkey.GetValue("InstallLocation") as string;
                    if (!string.IsNullOrEmpty(installDir))
                        return installDir;
                }
            }

            // search in: LocalMachine_32
            key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            foreach (string keyName in key.GetSubKeyNames())
            {
                RegistryKey subkey = key.OpenSubKey(keyName);
                displayName = subkey.GetValue("DisplayName") as string;
                if (p_name.Equals(displayName, StringComparison.OrdinalIgnoreCase) == true)
                {
                    installDir = subkey.GetValue("InstallLocation") as string;
                    if (!string.IsNullOrEmpty(installDir))
                        return installDir;
                }
            }

            // search in: LocalMachine_64
            key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
            foreach (string keyName in key.GetSubKeyNames())
            {
                RegistryKey subkey = key.OpenSubKey(keyName);
                displayName = subkey.GetValue("DisplayName") as string;
                if (p_name.Equals(displayName, StringComparison.OrdinalIgnoreCase) == true)
                {
                    installDir = subkey.GetValue("InstallLocation") as string;
                    if (!string.IsNullOrEmpty(installDir))
                        return installDir;
                }
            }

            // NOT FOUND
            return null;
        }

        /// <summary>
        /// Checks if an address is valid.
        /// </summary>
        /// <param name="address">The address (the pointer points to).</param>
        /// <returns>True if (pointer points to a) valid address.</returns>
        private static bool IsValidAddress(Int64 address)
        {
            return (address >= 0x10000 && address < 0x000F000000000000);
        }

        /// <summary>
        /// Reads a given type from processes memory using a generic method.
        /// </summary>
        /// <typeparam name="T">The base type to read.</typeparam>
        /// <param name="lpBaseAddress">The address to read from.</param>
        /// <returns>The given base type read from memory.</returns>
        /// <remarks>GCHandle and Marshal are costy.</remarks>
        private static T Read<T>(Int64 lpBaseAddress)
        {
            byte[] lpBuffer = new byte[Marshal.SizeOf(typeof(T))];
            WinAPI.ReadProcessMemory(_gameAccessHwndStatic, lpBaseAddress, lpBuffer, (ulong)lpBuffer.Length, out _);
            GCHandle gcHandle = GCHandle.Alloc(lpBuffer, GCHandleType.Pinned);
            T structure = (T)Marshal.PtrToStructure(gcHandle.AddrOfPinnedObject(), typeof(T));
            gcHandle.Free();
            return structure;
        }

        /// <summary>
        /// Writes a given type and value to processes memory using a generic method.
        /// </summary>
        /// <param name="lpBaseAddress">The address to write from.</param>
        /// <param name="bytes">The byte array to write.</param>
        /// <returns>True if successful, false otherwise.</returns>
        private static bool WriteBytes(Int64 lpBaseAddress, byte[] bytes)
        {
            return WinAPI.WriteProcessMemory(_gameAccessHwndStatic, lpBaseAddress, bytes, (ulong)bytes.Length, out _);
        }

        /// <summary>
        /// On window closing.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // save all settings
            Properties.Settings.Default.Save();
        }

        /// <summary>
        /// Logs messages to log file.
        /// </summary>
        /// <param name="msg">The message to write to file.</param>
        internal static void LogToFile(string msg)
        {
            string timedMsg = "[" + DateTime.Now + "] " + msg;
            Debug.WriteLine(timedMsg);
            try
            {
                using (StreamWriter writer = new StreamWriter(_path_logs, true))
                {
                    writer.WriteLine(timedMsg);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Writing to log file failed: " + ex.Message, "Sekiro Fps Unlock And More");
            }
        }

        /// <summary>
        /// Write a status to the status bar.
        /// </summary>
        /// <param name="text">The text to write.</param>
        /// <param name="color">The color to use.</param>
        private void UpdateStatus(string text, Brush color)
        {
            this.tbStatus.Background = color;
            this.tbStatus.Text = $"{DateTime.Now.ToString("HH:mm:ss")} {text}";
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void bPatch_Click(object sender, RoutedEventArgs e)
        {
            PatchGame();
        }

        private void bStart_Click(object sender, RoutedEventArgs e)
        {
            bStart.IsEnabled = false;
            CheckGame(false);
        }
    }
}
