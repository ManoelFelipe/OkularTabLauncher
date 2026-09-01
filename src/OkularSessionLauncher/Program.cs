// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;

namespace OkularSessionLauncher
{
    internal static class Program
    {
        private const int ExitSuccess = 0;
        private const int ExitUnexpectedError = 1;
        private const int ExitInvalidMode = 2;
        private const int ExitOkularAlreadyRunning = 3;
        private const int ExitOkularNotFound = 4;
        private const int ExitSessionUnavailable = 5;

        private const string MonitorMutexName = @"Local\OkularSessionLauncher.Monitor.v1";
        private const string OkularOverrideVariable = "OKULAR_SESSION_LAUNCHER_OKULAR_EXE";
        private const string SharedOkularOverrideVariable = "OKULAR_TAB_LAUNCHER_OKULAR_EXE";

        private static readonly string BaseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OkularSessionLauncher");

        private static readonly string SessionFile = Path.Combine(BaseDirectory, "last-session.txt");
        private static readonly string LogFile = Path.Combine(BaseDirectory, "session-log.txt");

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr window, out Rect rect);

        private static void Log(string text)
        {
            try
            {
                Directory.CreateDirectory(BaseDirectory);
                File.AppendAllText(
                    LogFile,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + text + Environment.NewLine,
                    Encoding.UTF8);
            }
            catch
            {
                // A background monitor must never display a logging failure.
            }
        }

        private static bool IsExecutableFile(string? path)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                   File.Exists(path) &&
                   string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase);
        }

        private static string? NormalizeExecutable(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            try
            {
                string fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
                return IsExecutableFile(fullPath) ? fullPath : null;
            }
            catch
            {
                return null;
            }
        }

        private static string? FindOkularExecutable()
        {
            string? configured = NormalizeExecutable(
                Environment.GetEnvironmentVariable(OkularOverrideVariable));
            if (configured != null)
                return configured;

            configured = NormalizeExecutable(
                Environment.GetEnvironmentVariable(SharedOkularOverrideVariable));
            if (configured != null)
                return configured;

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string? scoop = Environment.GetEnvironmentVariable("SCOOP");
            string? scoopGlobal = Environment.GetEnvironmentVariable("SCOOP_GLOBAL");

            List<string?> candidates = new List<string?>
            {
                string.IsNullOrWhiteSpace(scoop) ? null : Path.Combine(scoop, "apps", "okular", "current", "bin", "okular.exe"),
                Path.Combine(userProfile, "scoop", "apps", "okular", "current", "bin", "okular.exe"),
                string.IsNullOrWhiteSpace(scoopGlobal) ? null : Path.Combine(scoopGlobal, "apps", "okular", "current", "bin", "okular.exe"),
                Path.Combine(programFiles, "Okular", "bin", "okular.exe"),
                Path.Combine(programFiles, "Okular", "okular.exe"),
                Path.Combine(programFilesX86, "Okular", "bin", "okular.exe"),
                Path.Combine(programFilesX86, "Okular", "okular.exe")
            };

            string? pathVariable = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrWhiteSpace(pathVariable))
            {
                foreach (string directory in pathVariable.Split(Path.PathSeparator))
                {
                    if (!string.IsNullOrWhiteSpace(directory))
                        candidates.Add(Path.Combine(directory.Trim(), "okular.exe"));
                }
            }

            foreach (string? candidate in candidates)
            {
                string? normalized = NormalizeExecutable(candidate);
                if (normalized != null)
                    return normalized;
            }

            return null;
        }

        private static Process? FindMainOkular()
        {
            Process? best = null;
            long bestArea = -1;

            foreach (Process process in Process.GetProcessesByName("okular"))
            {
                try
                {
                    IntPtr window = process.MainWindowHandle;
                    if (window == IntPtr.Zero || !GetWindowRect(window, out Rect rect))
                    {
                        process.Dispose();
                        continue;
                    }

                    long width = Math.Max(0, rect.Right - rect.Left);
                    long height = Math.Max(0, rect.Bottom - rect.Top);
                    long area = width * height;

                    if (area > bestArea)
                    {
                        best?.Dispose();
                        best = process;
                        bestArea = area;
                    }
                    else
                    {
                        process.Dispose();
                    }
                }
                catch
                {
                    process.Dispose();
                }
            }

            return best;
        }

        private static T RunStaWithTimeout<T>(
            Func<T> action,
            int timeoutMilliseconds,
            T fallback,
            string operation)
        {
            T result = fallback;
            Exception? failure = null;

            Thread worker = new Thread(() =>
            {
                try
                {
                    result = action();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });

            worker.IsBackground = true;
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();

            if (!worker.Join(timeoutMilliseconds))
            {
                Log("Timed out while attempting to " + operation + ".");
                return fallback;
            }

            if (failure != null)
            {
                Log("Failed to " + operation + ": " + failure.Message);
                return fallback;
            }

            return result;
        }

        private static List<AutomationElement> GetNamedTabs(Process okular)
        {
            List<AutomationElement> tabs = new List<AutomationElement>();
            AutomationElement root = AutomationElement.FromHandle(okular.MainWindowHandle);
            if (root == null)
                return tabs;

            Condition condition = new PropertyCondition(
                AutomationElement.ControlTypeProperty,
                ControlType.TabItem);

            AutomationElementCollection elements = root.FindAll(TreeScope.Descendants, condition);
            for (int index = 0; index < elements.Count; index++)
            {
                AutomationElement tab = elements[index];
                if (!string.IsNullOrWhiteSpace(tab.Current.Name))
                    tabs.Add(tab);
            }

            return tabs;
        }

        private static string GetTabFingerprint(Process okular)
        {
            return string.Join(
                "\u001F",
                GetNamedTabs(okular).Select(tab => tab.Current.Name ?? string.Empty));
        }

        private static List<string> CaptureSession(Process okular)
        {
            List<string> paths = new List<string>();
            List<AutomationElement> tabs = GetNamedTabs(okular);
            if (tabs.Count == 0)
                return paths;

            AutomationElement? originalTab = null;

            foreach (AutomationElement tab in tabs)
            {
                if (tab.TryGetCurrentPattern(SelectionItemPattern.Pattern, out object patternObject) &&
                    ((SelectionItemPattern)patternObject).Current.IsSelected)
                {
                    originalTab = tab;
                    break;
                }
            }

            foreach (AutomationElement tab in tabs)
            {
                try
                {
                    if (!tab.TryGetCurrentPattern(SelectionItemPattern.Pattern, out object patternObject))
                        continue;

                    ((SelectionItemPattern)patternObject).Select();
                    Thread.Sleep(130);
                    okular.Refresh();

                    string title = okular.MainWindowTitle ?? string.Empty;
                    const string suffix = " - Okular";
                    if (title.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                        title = title.Substring(0, title.Length - suffix.Length);

                    string candidate = title.Trim().Replace('/', '\\');
                    if (File.Exists(candidate))
                        paths.Add(Path.GetFullPath(candidate));
                }
                catch
                {
                    // A tab can disappear while the snapshot is being captured.
                }
            }

            if (originalTab != null)
            {
                try
                {
                    if (originalTab.TryGetCurrentPattern(SelectionItemPattern.Pattern, out object patternObject))
                        ((SelectionItemPattern)patternObject).Select();
                }
                catch
                {
                    // Restoring the selected tab is best effort only.
                }
            }

            return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static bool SaveSession(IReadOnlyCollection<string> paths)
        {
            if (paths.Count == 0)
                return false;

            Directory.CreateDirectory(BaseDirectory);
            string temporaryFile = SessionFile + ".tmp";
            File.WriteAllLines(temporaryFile, paths, new UTF8Encoding(false));
            File.Move(temporaryFile, SessionFile, true);
            Log("Session saved: " + paths.Count + " tab(s).");
            return true;
        }

        private static List<string> LoadSession()
        {
            if (!File.Exists(SessionFile))
                return new List<string>();

            return File.ReadAllLines(SessionFile, Encoding.UTF8)
                .Select(path => (path ?? string.Empty).Trim())
                .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static Process? StartOkular(IEnumerable<string> paths)
        {
            string? executable = FindOkularExecutable();
            if (executable == null)
            {
                Log("Okular was not found. Set " + OkularOverrideVariable + ".");
                return null;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(executable) ?? string.Empty
            };

            foreach (string path in paths)
                startInfo.ArgumentList.Add(path);

            return Process.Start(startInfo);
        }

        private static bool WaitForOkular(int timeoutMilliseconds)
        {
            int elapsed = 0;
            while (elapsed < timeoutMilliseconds)
            {
                using (Process? okular = FindMainOkular())
                {
                    if (okular != null)
                        return true;
                }

                Thread.Sleep(250);
                elapsed += 250;
            }

            return false;
        }

        private static int RestoreSession()
        {
            using (Process? running = FindMainOkular())
            {
                if (running != null)
                {
                    Log("Restore skipped because Okular is already running.");
                    return ExitOkularAlreadyRunning;
                }
            }

            List<string> paths = LoadSession();
            Process? started = StartOkular(paths);
            if (started == null)
                return ExitOkularNotFound;

            started.Dispose();
            Log(paths.Count == 0
                ? "No saved session; Okular started normally."
                : "Restoring " + paths.Count + " tab(s).");
            return ExitSuccess;
        }

        private static int SaveOnce()
        {
            using Process? okular = FindMainOkular();
            if (okular == null)
            {
                Log("Save skipped because Okular is not running.");
                return ExitSessionUnavailable;
            }

            List<string> session = RunStaWithTimeout(
                () => CaptureSession(okular),
                7000,
                new List<string>(),
                "capture the session");

            return SaveSession(session) ? ExitSuccess : ExitSessionUnavailable;
        }

        private static int ClearSession()
        {
            if (File.Exists(SessionFile))
                File.Delete(SessionFile);

            Log("Saved session cleared.");
            return ExitSuccess;
        }

        private static void RestoreAutomatically(Process okular)
        {
            List<string> saved = LoadSession();
            if (saved.Count == 0)
            {
                Log("Automatic restore skipped because no session is saved.");
                return;
            }

            List<string> current = RunStaWithTimeout(
                () => CaptureSession(okular),
                6000,
                new List<string>(),
                "read tabs for automatic restore");

            HashSet<string> currentSet = new HashSet<string>(current, StringComparer.OrdinalIgnoreCase);
            if (saved.All(currentSet.Contains))
            {
                Log("Automatic restore skipped because all saved tabs are already open.");
                return;
            }

            if (current.Count > 1)
            {
                Log("Automatic restore skipped to avoid closing a window that already has multiple tabs.");
                return;
            }

            List<string> combined = saved
                .Concat(current)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            Log("Restarting Okular to restore " + combined.Count + " tab(s).");

            if (!okular.CloseMainWindow() || !okular.WaitForExit(7000))
            {
                Log("Automatic restore cancelled because the initial Okular window did not close.");
                return;
            }

            using Process? started = StartOkular(combined);
            if (started != null)
                WaitForOkular(10000);
        }

        private static int Monitor()
        {
            bool createdNew;
            using Mutex mutex = new Mutex(true, MonitorMutexName, out createdNew);
            if (!createdNew)
            {
                Log("Monitor is already running.");
                return ExitSuccess;
            }

            Log("Monitor started.");

            IntPtr lastWindow = IntPtr.Zero;
            string lastFingerprint = string.Empty;
            bool initialized = false;
            bool wasRunning = false;

            while (true)
            {
                using Process? okular = FindMainOkular();

                if (!initialized)
                {
                    initialized = true;
                    wasRunning = okular != null;
                    if (wasRunning)
                        Log("Attached to an existing Okular window; automatic restore deferred.");
                }

                if (okular == null)
                {
                    if (wasRunning)
                        Log("Okular closed; last session preserved.");

                    wasRunning = false;
                    lastWindow = IntPtr.Zero;
                    lastFingerprint = string.Empty;
                    Thread.Sleep(900);
                    continue;
                }

                if (!wasRunning)
                {
                    wasRunning = true;
                    Log("New Okular start detected.");
                    RestoreAutomatically(okular);
                    Thread.Sleep(500);
                    continue;
                }

                string fingerprint = RunStaWithTimeout(
                    () => GetTabFingerprint(okular),
                    3500,
                    string.Empty,
                    "inspect the tab list");

                IntPtr currentWindow = okular.MainWindowHandle;
                if (currentWindow != lastWindow ||
                    (!string.IsNullOrWhiteSpace(fingerprint) &&
                     !string.Equals(fingerprint, lastFingerprint, StringComparison.Ordinal)))
                {
                    Thread.Sleep(350);
                    List<string> session = RunStaWithTimeout(
                        () => CaptureSession(okular),
                        7000,
                        new List<string>(),
                        "capture the session");

                    if (SaveSession(session))
                    {
                        lastWindow = currentWindow;
                        lastFingerprint = RunStaWithTimeout(
                            () => GetTabFingerprint(okular),
                            3500,
                            fingerprint,
                            "confirm the saved tab list");
                    }
                }

                Thread.Sleep(800);
            }
        }

        [STAThread]
        private static int Main(string[] arguments)
        {
            Directory.CreateDirectory(BaseDirectory);
            string mode = arguments.Length == 0
                ? "--restore"
                : arguments[0].Trim().ToLowerInvariant();

            try
            {
                switch (mode)
                {
                    case "--monitor":
                        return Monitor();
                    case "--save":
                        return SaveOnce();
                    case "--restore":
                        return RestoreSession();
                    case "--clear":
                        return ClearSession();
                    default:
                        Log("Unknown command-line mode: " + mode);
                        return ExitInvalidMode;
                }
            }
            catch (Exception exception)
            {
                Log("Fatal error: " + exception);
                return ExitUnexpectedError;
            }
        }
    }
}
