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
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;

namespace EldenRingFPSUnlockAndMore
{
    public partial class MainWindow : Window
    {
        internal long _offset_framelock = 0x0;
        internal long _offset_hertzlock = 0x0;
        internal long _offset_resolution = 0x0;
        internal long _offset_resolution_scaling_fix = 0x0;
        internal long _offset_fovmultiplier = 0x0;
        internal long _offset_deathpenalty = 0x0;
        internal long _offset_timescale = 0x0;

        internal byte[] _patch_hertzlock_disable;
        internal byte[] _patch_resolution_enable;
        internal byte[] _patch_resolution_disable;
        internal byte[] _patch_deathpenalty_disable;

        internal bool _codeCave_fovmultiplier = false;
        internal const string _DATACAVE_FOV_MULTIPLIER = "dfovMultiplier";
        internal const string _CODECAVE_FOV_MULTIPLIER = "cfovMultiplier";

        internal static string _path_logs;
        internal Process _gameProc;
        internal IntPtr _gameHwnd = IntPtr.Zero;
        internal IntPtr _gameAccessHwnd = IntPtr.Zero;
        internal static IntPtr _gameAccessHwndStatic;
        internal static bool _startup = true;
        internal static bool _running = false;

        internal MemoryCaveGenerator _memoryCaveGenerator;
        internal readonly DispatcherTimer _dispatcherTimerGameCheck = new DispatcherTimer();
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
            _dispatcherTimerGameCheck.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            _dispatcherTimerGameCheck.Start();
        }

        /// <summary>
        /// Check if game is running.
        /// </summary>
        private async Task<bool> CheckGame()
        {
            // check for game
            var procList = Process.GetProcessesByName(Properties.Settings.Default.GameName);
            if (!procList.Any() || procList[0].HasExited) 
                return false;

            // check if game is running without EAC
            //var procArgs = GetCommandLineOfProcess(procList[0]);
            var eacServices = ServiceController.GetServices().Where(service => service.ServiceName.Contains("EasyAntiCheat")).ToArray();
            var eacRunning = false;
            ServiceController sc = null; 
            foreach (var eacService in eacServices)
            {
                sc = new ServiceController(eacService.ServiceName);
                eacRunning = sc.Status == ServiceControllerStatus.Running ||
                             sc.Status == ServiceControllerStatus.ContinuePending ||
                             sc.Status == ServiceControllerStatus.StartPending;
                if (eacRunning)
                {
                    break;
                }
            }
                
            if (eacRunning || !File.Exists(Path.Combine(Path.GetDirectoryName(procList[0].MainModule.FileName), "steam_appid.txt")))
            {
                // if not prompt the user
                MessageBoxResult result = MessageBox.Show("Game is already running!\n\n" +
                                                          "Do you want to close and restart it in offline mode without EAC?\n\n", "Elden Ring FPS Unlocker and more", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                switch (result)
                {
                    case MessageBoxResult.Cancel:
                        return false;
                    case MessageBoxResult.No:
                        return await OpenGame();
                    case MessageBoxResult.Yes:
                    {
                        var filePath = Path.GetDirectoryName(procList[0].MainModule.FileName);
                        foreach (var proc in procList)
                        {
                            proc.Kill();
                            proc.WaitForExit(3000);
                            proc.Close();
                        }
                        await Task.Delay(2500);
                        if (sc != null && sc.Status != ServiceControllerStatus.Stopped && sc.Status != ServiceControllerStatus.StopPending)
                            sc.Stop();
                        await Task.Delay(2500);
                        await SafeStartGame(filePath);
                        return await OpenGame();
                    }
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
                if (procList[0].Responding && !procList[0].HasExited)
                    return await OpenGame();
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
            string gameExePath = OpenFile("Select eldenring.exe", "C:\\", new[] { "*.exe" }, new[] { "Elden Ring Executable" }, true);
            if (string.IsNullOrEmpty(gameExePath) || !File.Exists(gameExePath))
                Environment.Exit(0);
            var fileInfo = FileVersionInfo.GetVersionInfo(gameExePath);
            if (!fileInfo.FileDescription.ToLower().Contains(GameData.PROCESS_DESCRIPTION))
            {
                MessageBox.Show("Invalid game file!", "Elden Ring FPS Unlocker and more", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                Environment.Exit(0);
            }
            Properties.Settings.Default.GameName = Path.GetFileNameWithoutExtension(gameExePath);
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
                if (gamePath == null || (!File.Exists(Path.Combine(gamePath, $"{Properties.Settings.Default.GameName}.exe")) && !File.Exists(Path.Combine(gamePath, "GAME", $"{Properties.Settings.Default.GameName}.exe"))))
                    gameExePath = PromptForGamePath();
                else
                {
                    if (File.Exists(Path.Combine(gamePath, "GAME", $"{Properties.Settings.Default.GameName}.exe")))
                        gameExePath = Path.Combine(gamePath, "GAME", $"{Properties.Settings.Default.GameName}.exe");
                    else if (File.Exists(Path.Combine(gamePath, $"{Properties.Settings.Default.GameName}.exe")))
                        gameExePath = Path.Combine(gamePath, $"{Properties.Settings.Default.GameName}.exe");
                    else
                        gameExePath = PromptForGamePath();
                }
            }
            else
            {
                var fileInfo = FileVersionInfo.GetVersionInfo(gameExePath);
                if (!fileInfo.FileDescription.ToLower().Contains(GameData.PROCESS_DESCRIPTION))
                    gameExePath = PromptForGamePath();
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
                await Task.Delay(4000);
            }

            // start the game
            ProcessStartInfo siGame = new ProcessStartInfo
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                Verb = "runas",
                FileName = "cmd.exe",
                WorkingDirectory = gamePath,
                Arguments = $"/C \"{Properties.Settings.Default.GameName}.exe -noeac\""
            };
            Process procGameStarter = new Process
            {
                StartInfo = siGame
            };
            procGameStarter.Start();
            await WaitForProgram(Properties.Settings.Default.GameName, 10000);
            await Task.Delay(4000);
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
        private async Task<bool> OpenGame(bool retry = false)
        {
            UpdateStatus("accessing game...", Brushes.Orange);
            Process[] procList = Process.GetProcessesByName(Properties.Settings.Default.GameName);
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
                UpdateStatus("no game access...", Brushes.Red);
                LogToFile($"Process: {_gameProc.ProcessName} - {_gameProc.MainWindowTitle}");
                LogToFile($"hWnd: {_gameHwnd:X}");
                LogToFile($"Access hWnd: {_gameAccessHwnd}:X");
                LogToFile($"BaseAddress: {_gameProc.MainModule.BaseAddress:X}");
                if (!retry)
                {
                    await Task.Delay(5000);
                    return await OpenGame(true);
                }
                else
                {
                    MessageBox.Show("Couldn't gain access to game process!", "Elden Ring FPS Unlocker and more", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return false;
                }
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
            _memoryCaveGenerator = new MemoryCaveGenerator(_gameAccessHwnd, _gameProc.MainModule.BaseAddress.ToInt64());

            _offset_framelock = patternScan.FindPattern(GameData.PATTERN_FRAMELOCK) + GameData.PATTERN_FRAMELOCK_OFFSET;
            Debug.WriteLine($"fFrameTick found at: 0x{_offset_framelock:X}");
            if (!IsValidAddress(_offset_framelock))
            {
                _offset_framelock = patternScan.FindPattern(GameData.PATTERN_FRAMELOCK_FUZZY) + GameData.PATTERN_FRAMELOCK_OFFSET_FUZZY;
                if (!IsValidAddress(_offset_framelock))
                    _offset_framelock = 0x0;
            }

            _offset_hertzlock = patternScan.FindPattern(GameData.PATTERN_HERTZLOCK) + GameData.PATTERN_HERTZLOCK_OFFSET;
            Debug.WriteLine($"HertzLock found at: 0x{_offset_hertzlock:X}");
            if (!IsValidAddress(_offset_hertzlock))
                _offset_hertzlock = 0x0;
            else
            {
                _patch_hertzlock_disable = new byte[GameData.PATCH_HERTZLOCK_INSTRUCTION_LENGTH];
                if (!WinAPI.ReadProcessMemory(_gameAccessHwndStatic, _offset_hertzlock, _patch_hertzlock_disable, GameData.PATCH_HERTZLOCK_INSTRUCTION_LENGTH, out _))
                    _offset_hertzlock = 0x0;
            }

            Size nativeRes = GetDpiSafeResolution();
            if (nativeRes != Size.Empty && nativeRes.Width > 1)
            {
                Debug.WriteLine($"Native Res:{nativeRes.Width}x{nativeRes.Height}");
                _patch_resolution_enable = new byte[8];
                Buffer.BlockCopy(BitConverter.GetBytes((Int32)nativeRes.Width), 0, _patch_resolution_enable, 0, 4);
                Buffer.BlockCopy(BitConverter.GetBytes((Int32)nativeRes.Height), 0, _patch_resolution_enable, 4, 4);
                _offset_resolution = patternScan.FindPattern(nativeRes.Width < 1080 ? GameData.PATTERN_RESOLUTION_DEFAULT_720 : GameData.PATTERN_RESOLUTION_DEFAULT);
                Debug.WriteLine($"pResolution found at: 0x{_offset_resolution:X}");
                if (!IsValidAddress(_offset_resolution))
                    _offset_resolution = 0x0;
                else
                {
                    _offset_resolution_scaling_fix = patternScan.FindPattern(GameData.PATTERN_RESOLUTION_SCALING_FIX) + GameData.PATTERN_RESOLUTION_SCALING_FIX_OFFSET;
                    Debug.WriteLine($"resolution scaling found at: 0x{_offset_resolution_scaling_fix:X}");
                    if (!IsValidAddress(_offset_resolution_scaling_fix))
                        _offset_resolution = 0x0;
                    else
                    {
                        _patch_resolution_disable = new byte[8];
                        if (!WinAPI.ReadProcessMemory(_gameAccessHwndStatic, _offset_resolution, _patch_resolution_disable, 8, out _))
                            _offset_resolution = 0x0;
                    }
                }
            }

            _offset_fovmultiplier = patternScan.FindPattern(GameData.PATTERN_FOV_MULTIPLIER) + GameData.PATTERN_FOV_MULTIPLIER_OFFSET;
            Debug.WriteLine($"pFovMultiplier found at: 0x{_offset_fovmultiplier:X}");
            if (!IsValidAddress(_offset_fovmultiplier))
                _offset_fovmultiplier = 0x0;
            else
            {
                if (_memoryCaveGenerator.CreateNewCodeCave(_CODECAVE_FOV_MULTIPLIER, _offset_fovmultiplier, GameData.INJECT_FOV_MULTIPLIER_OVERWRITE_LENGTH, GameData.INJECT_FOV_MULTIPLIER_SHELLCODE, true))
                {
                    Debug.WriteLine($"pFovMultiplier code cave at: 0x{_memoryCaveGenerator.GetCodeCaveAddressByName(_CODECAVE_FOV_MULTIPLIER):X}");
                    if (_memoryCaveGenerator.CreateNewDataCave(_DATACAVE_FOV_MULTIPLIER, _memoryCaveGenerator.GetCodeCaveAddressByName(_CODECAVE_FOV_MULTIPLIER) + GameData.INJECT_FOV_MULTIPLIER_SHELLCODE_OFFSET, BitConverter.GetBytes(1.0f), PointerStyle.dwRelative))
                    {
                        Debug.WriteLine($"pFovMultiplier data cave at: 0x{_memoryCaveGenerator.GetDataCaveAddressByName(_DATACAVE_FOV_MULTIPLIER):X}");
                        if (_memoryCaveGenerator.ActivateDataCaveByName(_DATACAVE_FOV_MULTIPLIER))
                            _codeCave_fovmultiplier = true;
                    }
                }
            }

            long ref_lpTimeRelated = patternScan.FindPattern(GameData.PATTERN_TIMESCALE) + GameData.PATTERN_TIMESCALE_OFFSET;
            Debug.WriteLine($"ref_lpTimeRelated found at: 0x{ref_lpTimeRelated:X}");
            if (IsValidAddress(ref_lpTimeRelated))
            {
                long lpTimescaleManager = DereferenceRelativeOffset(ref_lpTimeRelated);
                Debug.WriteLine($"lpTimescaleManager found at: 0x{lpTimescaleManager:X}");
                if (IsValidAddress(lpTimescaleManager))
                {
                    _offset_timescale = Read<Int64>(lpTimescaleManager) + Read<Int32>(ref_lpTimeRelated + GameData.PATTERN_TIMESCALE_POINTER_OFFSET);
                    Debug.WriteLine($"fTimescale found at: 0x{_offset_timescale:X}");
                    if (!IsValidAddress(_offset_timescale))
                        _offset_timescale = 0x0;
                }
            }

            _offset_deathpenalty = patternScan.FindPattern(GameData.PATTERN_DEATHPENALTY) + GameData.PATTERN_DEATHPENALTY_OFFSET;
            Debug.WriteLine($"death penalty found at: 0x{_offset_deathpenalty:X}");
            if (!IsValidAddress(_offset_deathpenalty))
                _offset_fovmultiplier = 0x0;
            else 
            {
                _patch_deathpenalty_disable = new byte[GameData.PATCH_DEATHPENALTY_INSTRUCTION_LENGTH];
                if (!WinAPI.ReadProcessMemory(_gameAccessHwndStatic, _offset_deathpenalty, _patch_deathpenalty_disable, GameData.PATCH_DEATHPENALTY_INSTRUCTION_LENGTH, out _))
                    _offset_deathpenalty = 0x0;
            }

            patternScan.Dispose();
        }

        /// <summary>
        /// All game data has been read.
        /// </summary>
        private void OnReadGameFinish(object sender, RunWorkerCompletedEventArgs runWorkerCompletedEventArgs)
        {
            UpdateStatus("ready...", Brushes.Green);
            if (_offset_framelock == 0x0)
            {
                UpdateStatus("frame tick not found...", Brushes.Red);
                LogToFile("frame tick not found...");
                cbFramelock.IsEnabled = false;
            }

            if (_offset_hertzlock == 0x0)
            {
                UpdateStatus("hertz lock not found...", Brushes.Red);
                LogToFile("hertz lock not found...");
            }

            if (_offset_fovmultiplier == 0x0 || !_codeCave_fovmultiplier)
            {
                UpdateStatus("FOV multiplier not found...", Brushes.Red);
                LogToFile("FOV multiplier not found...");
                cbFov.IsEnabled = false;
            }

            if (_offset_resolution == 0x0)
            {
                UpdateStatus("resolution not found...", Brushes.Red);
                LogToFile("resolution not found...");
                cbAddWidescreen.IsEnabled = false;
            }

            if (_offset_deathpenalty == 0x0)
            {
                UpdateStatus("death penalty not found...", Brushes.Red);
                LogToFile("death penalty not found...");
                cbDeathPenalty.IsEnabled = false;
            }

            if (_offset_timescale == 0x0)
            {
                UpdateStatus("time scale not found...", Brushes.Red);
                LogToFile("time scale not found...");
                cbGameSpeed.IsEnabled = false;
            }

            bPatch.IsEnabled = true;
            _running = true;
            PatchGame();
        }

        /// <summary>
        /// Determines whether everything is ready for patching.
        /// </summary>
        /// <returns>True if we can patch game, false otherwise.</returns>
        private bool CanPatchGame()
        {
            if (!_running) 
                return false;

            Process[] processlist = Process.GetProcesses();
            if (!_gameProc.HasExited && processlist.Any(p => p.Id == _gameProc.Id)) 
                return true;

            ResetGame();
            return false;
        }

        /// <summary>
        /// Resets all game data.
        /// </summary>
        private void ResetGame()
        {
            _running = false;
            if (_gameAccessHwnd != IntPtr.Zero)
                WinAPI.CloseHandle(_gameAccessHwnd);
            _gameProc = null;
            _gameHwnd = IntPtr.Zero;
            _gameAccessHwnd = IntPtr.Zero;
            _gameAccessHwndStatic = IntPtr.Zero;
            _offset_framelock = 0x0;
            _offset_hertzlock = 0x0;
            _offset_fovmultiplier = 0x0;
            _offset_resolution = 0x0;
            _offset_resolution_scaling_fix = 0x0;
            _offset_deathpenalty = 0x0;
            _offset_timescale = 0x0;
            _startup = true;
            _patch_hertzlock_disable = null;
            _patch_deathpenalty_disable = null;
            _patch_resolution_enable = null;
            _codeCave_fovmultiplier = false;
            if (_memoryCaveGenerator != null)
                _memoryCaveGenerator.ClearCaves();
            _memoryCaveGenerator = null;
            cbFramelock.IsEnabled = true;
            cbFov.IsEnabled = true;
            cbAddWidescreen.IsEnabled = true;
            cbDeathPenalty.IsEnabled = true;
            cbGameSpeed.IsEnabled = true;
            bPatch.IsEnabled = false;
            _dispatcherTimerGameCheck.Start();
        }

        /// <summary>
        /// Patch game memory.
        /// </summary>
        private void PatchGame()
        {
            if (!CanPatchGame())
                return;
            PatchFramelock();
            PatchFov();
            PatchWidescreen();
            PatchDeathPenalty();
            PatchGameSpeed();
            UpdateStatus("game patched!", Brushes.Green);
        }

        /// <summary>
        /// Patch the game's frame rate lock.
        /// </summary>
        private bool PatchFramelock()
        {
            if (!cbFramelock.IsEnabled || _offset_framelock == 0x0 || !CanPatchGame()) return false;
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
                else if (fps > 360)
                {
                    tbFramelock.Text = "360";
                    fps = 360;
                }

                float deltaTime = (1000f / fps) / 1000f;
                WriteBytes(_offset_framelock, BitConverter.GetBytes(deltaTime));
                Int32 noHertzLock = 0x00000000;
                WriteBytes(_offset_hertzlock + GameData.PATTERN_HERTZLOCK_OFFSET_INTEGER1, BitConverter.GetBytes(noHertzLock));
                WriteBytes(_offset_hertzlock + GameData.PATTERN_HERTZLOCK_OFFSET_INTEGER2, BitConverter.GetBytes(noHertzLock));
            }
            else
            {
                float deltaTime = (1000f / 60) / 1000f;
                WriteBytes(_offset_framelock, BitConverter.GetBytes(deltaTime));
                WriteBytes(_offset_hertzlock, _patch_hertzlock_disable);
            }
            return true;
        }

        /// <summary>
        /// Patches the game's field of view.
        /// </summary>
        private bool PatchFov()
        {
            if (!cbFov.IsEnabled || _offset_fovmultiplier == 0x0 || !CanPatchGame()) return false;
            if (cbFov.IsChecked == true)
            {
                bool isNumber = Int32.TryParse(tbFov.Text, out int fovIncrease);
                if (fovIncrease < -95 || !isNumber)
                {
                    tbFov.Text = "-95";
                    fovIncrease = -95;
                }
                else if (fovIncrease > 95)
                {
                    tbFov.Text = "95";
                    fovIncrease = 95;
                }

                float fovValue = 1.0f + (fovIncrease / 100f);
                _memoryCaveGenerator.UpdateDataCaveValueByName(_DATACAVE_FOV_MULTIPLIER, BitConverter.GetBytes(fovValue));
                _memoryCaveGenerator.ActivateCodeCaveByName(_CODECAVE_FOV_MULTIPLIER);
            }
            else if (cbFov.IsChecked == false)
            {
                _memoryCaveGenerator.DeactivateCodeCaveByName(_CODECAVE_FOV_MULTIPLIER);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Patch games resolutions.
        /// </summary>
        /// <returns></returns>
        private bool PatchWidescreen()
        {
            if (!cbAddWidescreen.IsEnabled || _offset_resolution == 0x0 || !CanPatchGame()) return false;
            if (cbAddWidescreen.IsChecked == true)
            {
                WriteBytes(_offset_resolution, _patch_resolution_enable);
                WriteBytes(_offset_resolution_scaling_fix, GameData.PATCH_RESOLUTION_SCALING_FIX_ENABLE);
            }
            else if (cbAddWidescreen.IsChecked == false)
            {
                WriteBytes(_offset_resolution, _patch_resolution_disable);
                WriteBytes(_offset_resolution_scaling_fix, GameData.PATCH_RESOLUTION_SCALING_FIX_DISABLE);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Patches the game's death penalties.
        /// </summary>
        private bool PatchDeathPenalty()
        {
            if (!cbDeathPenalty.IsEnabled || _offset_deathpenalty == 0x0 || !CanPatchGame()) return false;
            if (cbDeathPenalty.IsChecked == true)
            {
                WriteBytes(_offset_deathpenalty, GameData.PATCH_DEATHPENALTY_ENABLE);
            }
            else if (cbDeathPenalty.IsChecked == false)
            {
                WriteBytes(_offset_deathpenalty, _patch_deathpenalty_disable);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Patches game's global speed.
        /// </summary>
        private bool PatchGameSpeed()
        {
            if (!cbGameSpeed.IsEnabled || _offset_timescale == 0x0 || !CanPatchGame()) return false;
            if (cbGameSpeed.IsChecked == true)
            {
                bool isNumber = Int32.TryParse(tbGameSpeed.Text, out int gameSpeed);
                if (gameSpeed < 0 || !isNumber)
                {
                    tbGameSpeed.Text = "100";
                    gameSpeed = 100;
                }
                else if (gameSpeed >= 999)
                {
                    tbGameSpeed.Text = "999";
                    gameSpeed = 1000;
                }
                float timeScale = gameSpeed / 100f;
                if (timeScale < 0.01f)
                    timeScale = 0.0001f;
                WriteBytes(_offset_timescale, BitConverter.GetBytes(timeScale));
            }
            else if (cbGameSpeed.IsChecked == false)
            {
                WriteBytes(_offset_timescale, BitConverter.GetBytes(1.0f));
                return false;
            }
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
        /// Check if installDir of an application is valid.
        /// </summary>
        /// <param name="displayName">The application install dir.</param>
        /// <param name="p_name">The apps name.</param>
        /// <param name="subkey">The registry key.</param>
        /// <param name="installDir"></param>
        /// <returns>True if valid.</returns>
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
        /// Gets DPI-clean resolution of the primary screen. 
        /// </summary>
        /// <returns></returns>
        private Size GetDpiSafeResolution()
        {
            IntPtr hwnd = WinAPI.GetDC(IntPtr.Zero);
            return new Size(WinAPI.GetDeviceCaps(hwnd, WinAPI.DeviceCap.VERTRES), WinAPI.GetDeviceCaps(hwnd, WinAPI.DeviceCap.HORZRES));

            // Requires dpiAware in manifest.
            //PresentationSource presentationSource = PresentationSource.FromVisual(this);
            //if (presentationSource == null)
            //    return new Size(SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
            //Matrix matrix = presentationSource.CompositionTarget.TransformToDevice;
            //return new Size(SystemParameters.PrimaryScreenWidth * matrix.M22, SystemParameters.PrimaryScreenHeight * matrix.M11);
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
        /// Reads a compile-time static, relative 4 bytes offset from an instruction and dereferences it.
        /// </summary>
        /// <param name="addressToRelativeOffset">The address the offset is located at.</param>
        /// <returns>The actual, non-relative address the offset points to.</returns>
        private static Int64 DereferenceRelativeOffset(Int64 addressToRelativeOffset)
        {
            return addressToRelativeOffset + Read<Int32>(addressToRelativeOffset) + 0x4;
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

            if (_gameAccessHwnd != IntPtr.Zero)
                WinAPI.CloseHandle(_gameAccessHwnd);
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
            {
                ResetGame();
                await SafeStartGame();
            }
            bStart.IsEnabled = true;
        }

        private void BFov0_Click(object sender, RoutedEventArgs e)
        {
            tbFov.Text = "0";
            if (cbFov.IsChecked == true) 
                PatchFov();
        }

        private void BFovLower_Click(object sender, RoutedEventArgs e)
        {
            if (Int32.TryParse(tbFov.Text, out int fov) && fov > -91)
            {
                tbFov.Text = (fov - 5).ToString();
                if (cbFov.IsChecked == true) 
                    PatchFov();
            }
        }

        private void BFovHigher_Click(object sender, RoutedEventArgs e)
        {
            if (Int32.TryParse(tbFov.Text, out int fov) && fov < 91)
            {
                tbFov.Text = (fov + 5).ToString();
                if (cbFov.IsChecked == true) 
                    PatchFov();
            }
        }

        private void BGsLower_Click(object sender, RoutedEventArgs e)
        {
            if (Int32.TryParse(tbGameSpeed.Text, out int gameSpeed) && gameSpeed > 4)
            {
                tbGameSpeed.Text = (gameSpeed - 5).ToString();
                if (cbGameSpeed.IsChecked == true) 
                    PatchGameSpeed();
            }
        }

        private void BGsHigher_Click(object sender, RoutedEventArgs e)
        {
            if (Int32.TryParse(tbGameSpeed.Text, out int gameSpeed) && gameSpeed < 995)
            {
                tbGameSpeed.Text = (gameSpeed + 5).ToString();
                if (cbGameSpeed.IsChecked == true) 
                    PatchGameSpeed();
            }
        }

        private void BGs100_Click(object sender, RoutedEventArgs e)
        {
            tbGameSpeed.Text = "100";
            if (cbGameSpeed.IsChecked == true) 
                PatchGameSpeed();
        }
    }
}
