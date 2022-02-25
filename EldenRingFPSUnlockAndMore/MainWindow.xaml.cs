using System;
using System.Windows;
using System.Threading;
using System.Windows.Media;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.ServiceProcess;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using System.ComponentModel;
using System.Management;
using System.Text.RegularExpressions;

namespace EldenRingFPSUnlockAndMore
{
    public partial class MainWindow : Window
    {
        internal long _offset_framelock = 0x0;

        internal static string _path_logs;
        internal Process _gameProc;
        internal IntPtr _gameHwnd = IntPtr.Zero;
        internal IntPtr _gameAccessHwnd = IntPtr.Zero;
        internal static IntPtr _gameAccessHwndStatic;
        internal static bool _startup = true;

        internal readonly DispatcherTimer _dispatcherTimerGameCheck = new DispatcherTimer();
        internal readonly DispatcherTimer _dispatcherTimerFreezeMem = new DispatcherTimer();
        internal readonly BackgroundWorker _bgwScanGame = new BackgroundWorker();

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
            _path_logs = Path.Combine(local, "EldenRingFPSUnlockAndMore", "logs.log");
            if (!Directory.Exists(Path.Combine(local, "EldenRingFPSUnlockAndMore")))
                Directory.CreateDirectory(Path.Combine(local, "EldenRingFPSUnlockAndMore"));

            var mutex = new Mutex(true, "ErFpsUnlockAndMore", out bool isNewInstance);
            if (!isNewInstance)
            {
                MessageBox.Show("Another instance is already running!", "Elden Ring FPS Unlocker and more", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                Environment.Exit(0);
            }
            GC.KeepAlive(mutex);

            _bgwScanGame.DoWork += new DoWorkEventHandler(ReadGame);
            _bgwScanGame.RunWorkerCompleted += new RunWorkerCompletedEventHandler(OnReadGameFinish);

            _dispatcherTimerGameCheck.Tick += new EventHandler(async (object s, EventArgs a) =>
            {
                if (_startup)
                {
                    _startup = false;
                    UpdateStatus("waiting for game...", Brushes.White);
                    bStart.IsEnabled = true;
                }
                bool result = await CheckGame();
                if (result)
                {
                    UpdateStatus("reading game...", Brushes.Orange);
                    _bgwScanGame.RunWorkerAsync();
                    _dispatcherTimerGameCheck.Stop();
                }
            });
            _dispatcherTimerGameCheck.Interval = new TimeSpan(0, 0, 0, 0, 2000);
            _dispatcherTimerGameCheck.Start();
        }

        /// <summary>
        /// Check if game is running.
        /// </summary>
        private async Task<bool> CheckGame()
        {
            // check for game
            Process[] procList = Process.GetProcessesByName(GameData.PROCESS_NAME);
            if (procList.Length > 0)
            {
                // check if game is running without EAC
                string procArgs = GetCommandLineOfProcess(procList[0]);
                ServiceController sc = new ServiceController("EasyAntiCheat");
                if (string.IsNullOrEmpty(procArgs) || !procArgs.Contains("-noeac") || 
                    sc.Status == ServiceControllerStatus.Running || sc.Status == ServiceControllerStatus.ContinuePending || sc.Status == ServiceControllerStatus.StartPending ||
                    !File.Exists(Path.Combine(Path.GetDirectoryName(procList[0].MainModule.FileName), "steam_appid.txt")))
                {
                    // if not prompt the user
                    MessageBoxResult result = MessageBox.Show("Game is already running!\n\n" +
                                                              "Do you want to close and restart it in offline mode without EAC?\n\n", "Elden Ring FPS Unlocker and more", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                    if (result == MessageBoxResult.Cancel)
                        return false;
                    if (result == MessageBoxResult.No)
                    {
                        return OpenGame();
                    }
                    else if (result == MessageBoxResult.Yes)
                    {
                        string filePath = Path.GetDirectoryName(procList[0].MainModule.FileName);
                        foreach (Process proc in procList)
                        {
                            proc.Kill();
                            proc.WaitForExit(3000);
                            proc.Close();
                        }
                        await Task.Delay(1000);
                        sc = new ServiceController("EasyAntiCheat");
                        if (sc.Status != ServiceControllerStatus.Stopped && sc.Status != ServiceControllerStatus.StopPending)
                            sc.Stop();
                        await Task.Delay(2000);
                        await SafeStartGame(filePath);
                        return OpenGame();
                    }
                }
                else
                {
                    if (_gameProc != null && _gameProc.Id == procList[0].Id)
                    {
                        MessageBox.Show("Game is already running without EAC!", "Elden Ring FPS Unlocker and more", MessageBoxButton.OK, MessageBoxImage.Information);
                        return true;
                    }
                    if (!procList[0].Responding)
                        await Task.Delay(5000);
                    if (procList[0].HasExited)
                    {
                        await Task.Delay(2500);
                        return false;
                    }
                    return OpenGame();
                }
            }
            return false;
        }

        /// <summary>
        /// Open a prompt to let user choose game installation path.
        /// </summary>
        /// <returns>The choosen file location.<returns>
        private string PromptForGamePath()
        {
            MessageBox.Show("Couldn't find game installation path!\n\n" +
                            "Please specify the installation path yourself...", "Elden Ring FPS Unlocker and more", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            string gameExePath = OpenFile("Select eldenring.exe", "C:\\", new[] { "eldenring.exe" }, new[] { "Elden Ring Executable" }, true);
            if (string.IsNullOrEmpty(gameExePath) || !File.Exists(gameExePath))
                Environment.Exit(0);
            return gameExePath;
        }

        /// <summary>
        /// Starts the game (and steam) in offline mode without EAC.
        /// </summary>
        private async Task SafeStartGame(string path = null)
        {
            UpdateStatus("starting game...", Brushes.Orange);

            // get game path
            string gameExePath = Properties.Settings.Default.GamePath;
            string gamePath;
            if (!File.Exists(gameExePath))
            {
                gamePath = path ?? GetApplicationPath("ELDEN RING");
                if (gamePath == null || (!File.Exists(Path.Combine(gamePath, "eldenring.exe")) && !File.Exists(Path.Combine(gamePath, "GAME", "eldenring.exe"))))
                    gameExePath = PromptForGamePath();
                else
                {
                    if (File.Exists(Path.Combine(gamePath, "GAME", "eldenring.exe")))
                        gameExePath = Path.Combine(gamePath, "GAME", "eldenring.exe");
                    else if (File.Exists(Path.Combine(gamePath, "eldenring.exe")))
                        gameExePath = Path.Combine(gamePath, "eldenring.exe");
                    else
                        gameExePath = PromptForGamePath();
                }
            }
            Properties.Settings.Default.GamePath = gameExePath;
            gamePath = Path.GetDirectoryName(gameExePath);

            // create steam_appid
            try
            {
                File.WriteAllText(Path.Combine(gamePath, "steam_appid.txt"), "1245620");
            }
            catch
            {
                MessageBox.Show("Couldn't write steam id file!", "Elden Ring FPS Unlocker and more", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                Environment.Exit(0);
            }

            // start steam
            Process[] procList = Process.GetProcessesByName("steam");
            if (procList.Length == 0)
            {
                ProcessStartInfo siSteam = new ProcessStartInfo
                {
                    WindowStyle = ProcessWindowStyle.Minimized,
                    Verb = "open",
                    FileName = "steam://open/console",
                };
                Process procSteam = new Process
                {
                    StartInfo = siSteam
                };
                procSteam.Start();
                await WaitForProgram("steam", 6000);
                await Task.Delay(2000);
            }

            // start the game
            ProcessStartInfo siGame = new ProcessStartInfo
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                Verb = "runas",
                FileName = "cmd.exe",
                WorkingDirectory = gamePath,
                Arguments = $"/C \"eldenring.exe -noeac\""
            };
            Process procGameStarter = new Process
            {
                StartInfo = siGame
            };
            procGameStarter.Start();
            await WaitForProgram("eldenring", 10000);
            await Task.Delay(2000);
            procGameStarter.Close();
        }

        /// <summary>
        /// Waits a set timeout for a process to appear.
        /// </summary>
        /// <param name="appName">The process to look for.</param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        private async Task<bool> WaitForProgram(string appName, int timeout = 5000)
        {
            int timePassed = 0;
            while (true)
            {
                Process[] procList = Process.GetProcessesByName(appName);
                foreach (Process proc in procList)
                {
                    if (proc.ProcessName == appName)
                        return true;
                }

                await Task.Delay(500);
                timePassed += 500;
                if (timePassed > timeout)
                    return false;
            }
        }

        /// <summary>
        /// Opens the game for full access.
        /// </summary>
        private bool OpenGame()
        {
            UpdateStatus("accessing game...", Brushes.Orange);
            Process[] procList = Process.GetProcessesByName(GameData.PROCESS_NAME);
            if (procList.Length != 1)
            {
                MessageBox.Show("Couldn't find the game! Start the game without EAC manually.", "Elden Ring FPS Unlocker and more", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return false;
            }
            _gameProc = procList[0];

            // open game
            _gameHwnd = _gameProc.MainWindowHandle;
            _gameAccessHwnd = WinAPI.OpenProcess(WinAPI.PROCESS_ALL_ACCESS, false, (uint)_gameProc.Id);
            _gameAccessHwndStatic = _gameAccessHwnd;
            if (_gameHwnd == IntPtr.Zero || _gameAccessHwnd == IntPtr.Zero || _gameProc.MainModule.BaseAddress == IntPtr.Zero)
            {
                MessageBox.Show("Couldn't gain access to game process!", "Elden Ring FPS Unlocker and more", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return false;
            }
            UpdateStatus("game init...", Brushes.Orange);
            return true;
        }

        /// <summary>
        /// Read all game offsets and pointer (external).
        /// </summary>
        private void ReadGame(object sender, DoWorkEventArgs doWorkEventArgs)
        {
            PatternScan patternScan = new PatternScan(_gameAccessHwnd, _gameProc.MainModule);

            _offset_framelock = patternScan.FindPattern(GameData.PATTERN_FRAMELOCK) + GameData.PATTERN_FRAMELOCK_OFFSET;
            Debug.WriteLine($"fFrameTick found at: 0x{_offset_framelock:X}");
            if (!IsValidAddress(_offset_framelock))
            {
                _offset_framelock = patternScan.FindPattern(GameData.PATTERN_FRAMELOCK_FUZZY) + GameData.PATTERN_FRAMELOCK_OFFSET_FUZZY;
                if (!IsValidAddress(_offset_framelock))
                    _offset_framelock = 0x0;
            }

            patternScan.Dispose();
        }

        /// <summary>
        /// All game data has been read.
        /// </summary>
        private void OnReadGameFinish(object sender, RunWorkerCompletedEventArgs runWorkerCompletedEventArgs)
        {
            if (_offset_framelock == 0x0)
            {
                UpdateStatus("frame tick not found...", Brushes.Red);
                LogToFile("frame tick not found...");
                cbFramelock.IsEnabled = false;
            }

            bPatch.IsEnabled = true;
            PatchGame();
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
            else
            {
                float deltaTime = (1000f / 60) / 1000f;
                WriteBytes(_offset_framelock, BitConverter.GetBytes(deltaTime));
            }

            UpdateStatus("game patched!", Brushes.Green);
            return true;
        }

        /// <summary>
        /// Gets the install path of an application.
        /// </summary>
        /// <param name="p_name">The full name of the application from control panel.</param>
        /// <returns>The folder the application is installed into.</returns>
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
                displayName = subkey?.GetValue("DisplayName") as string;

                if (displayName != null)
                {
                    if (InstallDirCheck(displayName, p_name, subkey, out installDir))
                        return installDir;
                }

            }

            // search in: LocalMachine_32
            key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            foreach (string keyName in key.GetSubKeyNames())
            {
                RegistryKey subkey = key.OpenSubKey(keyName);
                displayName = subkey?.GetValue("DisplayName") as string;

                if (displayName != null)
                {
                    if (InstallDirCheck(displayName, p_name, subkey, out installDir))
                        return installDir;
                }
            }

            // search in: LocalMachine_64
            key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
            foreach (string keyName in key.GetSubKeyNames())
            {
                RegistryKey subkey = key.OpenSubKey(keyName);
                displayName = subkey?.GetValue("DisplayName") as string;

                if (displayName != null)
                {
                    if (InstallDirCheck(displayName, p_name, subkey, out installDir))
                        return installDir;
                }
            }

            // NOT FOUND
            return null;
        }

        /// <summary>
        /// Check if installDir is valid
        /// </summary>
        /// <param name="displayName"></param>
        /// <param name="p_name"></param>
        /// <param name="subkey"></param>
        /// <param name="installDir"></param>
        /// <returns>True if valid</returns>
        private static bool InstallDirCheck(string displayName, string p_name, RegistryKey subkey, out string installDir)
        {
            const string RegexNotASCII = @"[^\x00-\x80]+";
            installDir = string.Empty;

            // Check for non-English characters in displayName (CN, KR, ...) 
            if (Regex.IsMatch(displayName, RegexNotASCII))
            {
                // check if InstallLocation path contains ELDEN RING sind displayName contains non-standard characters 
                installDir = subkey.GetValue("InstallLocation") as string;
                if (installDir != null && installDir.Contains(p_name))
                {
                    // Not needed but just an additional check to see if eldenring.exe is in the InstallLocation path
                    if (File.Exists(installDir + @"\Game\eldenring.exe"))
                        return true;
                }
            }
            else if (p_name.Equals(displayName, StringComparison.OrdinalIgnoreCase))
            {
                installDir = subkey.GetValue("InstallLocation") as string;
                if (!string.IsNullOrEmpty(installDir))
                    return true;
            }

            return false;
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
        /// Returns the command line arguments a process has been started with.
        /// </summary>
        /// <param name="proc"></param>
        /// <returns></returns>
        private static string GetCommandLineOfProcess(Process proc)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher($"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {proc.Id}"))
                using (var objects = searcher.Get())
                {
                    foreach (var obj in objects)
                        return obj?["CommandLine"]?.ToString() ?? "";
                }
            }
            catch
            {
                return string.Empty;
            }
            return string.Empty;
        }

        /// <summary>
        /// Opens file dialog.
        /// </summary>
        /// <param name="title">The title to sho in the file selection window.</param>
        /// <param name="defaultDir">The default directory to start up to.</param>
        /// <param name="defaultExt">A list of default extensions in ".extension" format.</param>
        /// <param name="filter">A list of names of a file with this extension ("Extension File").</param>
        /// <returns>The path to the selected file.</returns>
        private static string OpenFile(string title, string defaultDir, string[] defaultExt, string[] filter, bool explicitFilter = false)
        {
            if (defaultExt.Length != filter.Length)
                throw new ArgumentOutOfRangeException("defaultExt must be the same length as filter!");
            string fullFilter = "";
            if (explicitFilter)
            {
                fullFilter = filter[0] + "|" + defaultExt[0];
            }
            else
            {
                for (int i = 0; i < defaultExt.Length; i++)
                {
                    if (i > 0)
                        fullFilter += "|";
                    fullFilter += filter[i] + " (*" + defaultExt[i] + ")|*" + defaultExt[i];
                }
            }

            OpenFileDialog dlg = new OpenFileDialog
            {
                Title = title,
                InitialDirectory = defaultDir,
                //DefaultExt = defaultExt,
                Filter = fullFilter,
                FilterIndex = 0,
            };
            bool? result = dlg.ShowDialog();
            if (result != true)
                return null;
            return File.Exists(dlg.FileName) ? dlg.FileName : null;
        }

        /// <summary>
        /// On window closing.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, CancelEventArgs e)
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
            tbStatus.Background = color;
            tbStatus.Text = $"{DateTime.Now.ToString("HH:mm:ss")} {text}";
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

        private async void bStart_Click(object sender, RoutedEventArgs e)
        {
            bStart.IsEnabled = false;
            bool res = await CheckGame();
            if (!res)
                await SafeStartGame();
            bStart.IsEnabled = true;
        }
    }
}
