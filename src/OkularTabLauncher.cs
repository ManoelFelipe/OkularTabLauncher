// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace OkularTabLauncher
{
    internal static class Program
    {
        private const int ExitSuccess = 0;
        private const int ExitUnexpectedError = 1;
        private const int ExitInvalidInput = 2;
        private const int ExitMutexTimeout = 3;
        private const int ExitOkularNotFound = 4;
        private const int ExitAutomationFailed = 5;

        private const int SwRestore = 9;
        private const int GwOwner = 4;
        private const int IdOk = 1;
        private const int FileNameComboId = 1148;
        private const int MinimumOpenDialogScore = 16000;
        private const uint WmSetText = 0x000C;
        private const uint BmClick = 0x00F5;

        private const string MutexName = @"Local\OkularTabLauncherV2";
        private const string OkularOverrideVariable = "OKULAR_TAB_LAUNCHER_OKULAR_EXE";

        private static readonly string BaseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OkularTabLauncher");

        private static readonly string ErrorLogPath = Path.Combine(BaseDirectory, "last-error.txt");
        private static readonly string RunLogPath = Path.Combine(BaseDirectory, "last-run.txt");

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private delegate bool EnumChildProc(IntPtr window, IntPtr parameter);
        private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr window, int command);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr window, out Rect rect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDlgItem(IntPtr dialog, int controlId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumChildWindows(
            IntPtr parent,
            EnumChildProc callback,
            IntPtr parameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(
            IntPtr window,
            StringBuilder className,
            int maximumCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(
            IntPtr window,
            StringBuilder text,
            int maximumCount);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr window, int command);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(
            IntPtr window,
            uint message,
            IntPtr wordParameter,
            string longParameter);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(
            IntPtr window,
            uint message,
            IntPtr wordParameter,
            IntPtr longParameter);

        private static string GetClass(IntPtr window)
        {
            StringBuilder value = new StringBuilder(256);
            GetClassName(window, value, value.Capacity);
            return value.ToString();
        }

        private static string GetTitle(IntPtr window)
        {
            StringBuilder value = new StringBuilder(1024);
            GetWindowText(window, value, value.Capacity);
            return value.ToString();
        }

        private static void WriteError(string text)
        {
            try
            {
                Directory.CreateDirectory(BaseDirectory);
                File.WriteAllText(
                    ErrorLogPath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + text,
                    Encoding.UTF8);
            }
            catch
            {
                // Logging must never launch an error dialog from this WinExe.
            }
        }

        private static void WriteRun(string pdfPath)
        {
            File.WriteAllText(
                RunLogPath,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + pdfPath,
                Encoding.UTF8);
        }

        private static void ClearPreviousError()
        {
            if (File.Exists(ErrorLogPath))
            {
                File.Delete(ErrorLogPath);
            }
        }

        private static int Fail(int exitCode, string message)
        {
            WriteError(message);
            return exitCode;
        }

        private static bool TryResolvePdf(string[] arguments, out string pdfPath, out string error)
        {
            pdfPath = null;
            error = null;

            if (arguments == null || arguments.Length != 1 || string.IsNullOrWhiteSpace(arguments[0]))
            {
                error = "Informe exatamente um caminho absoluto para um arquivo PDF.";
                return false;
            }

            if (!Path.IsPathRooted(arguments[0]))
            {
                error = "O caminho do PDF precisa ser absoluto:" + Environment.NewLine + arguments[0];
                return false;
            }

            try
            {
                pdfPath = Path.GetFullPath(arguments[0]);
            }
            catch (Exception exception)
            {
                error = "Caminho de PDF inválido:" + Environment.NewLine + exception.Message;
                return false;
            }

            if (!string.Equals(Path.GetExtension(pdfPath), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                error = "Somente arquivos com extensão .pdf são aceitos:" + Environment.NewLine + pdfPath;
                return false;
            }

            if (!File.Exists(pdfPath))
            {
                error = "Arquivo não encontrado:" + Environment.NewLine + pdfPath;
                return false;
            }

            return true;
        }

        private static string FindOkularExecutable()
        {
            string configuredPath = Environment.GetEnvironmentVariable(OkularOverrideVariable);
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                try
                {
                    string fullConfiguredPath = Path.GetFullPath(configuredPath);
                    if (IsExecutableFile(fullConfiguredPath))
                    {
                        return fullConfiguredPath;
                    }
                }
                catch
                {
                    // Continue with the known installation locations.
                }
            }

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string scoop = Environment.GetEnvironmentVariable("SCOOP");
            string scoopGlobal = Environment.GetEnvironmentVariable("SCOOP_GLOBAL");
            List<string> candidates = new List<string>();

            if (!string.IsNullOrWhiteSpace(scoop))
            {
                candidates.Add(Path.Combine(scoop, "apps", "okular", "current", "bin", "okular.exe"));
            }

            candidates.AddRange(new[]
            {
                Path.Combine(userProfile, "scoop", "apps", "okular", "current", "bin", "okular.exe"),
                Path.Combine(programFiles, "Okular", "bin", "okular.exe"),
                Path.Combine(programFiles, "Okular", "okular.exe"),
                Path.Combine(programFilesX86, "Okular", "bin", "okular.exe"),
                Path.Combine(programFilesX86, "Okular", "okular.exe")
            });

            if (!string.IsNullOrWhiteSpace(scoopGlobal))
            {
                candidates.Add(Path.Combine(scoopGlobal, "apps", "okular", "current", "bin", "okular.exe"));
            }

            string pathVariable = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrWhiteSpace(pathVariable))
            {
                foreach (string directory in pathVariable.Split(Path.PathSeparator))
                {
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        candidates.Add(Path.Combine(directory.Trim(), "okular.exe"));
                    }
                }
            }

            foreach (string candidate in candidates)
            {
                if (IsExecutableFile(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool IsExecutableFile(string path)
        {
            return File.Exists(path) &&
                   string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase);
        }

        private static Process FindMainOkular()
        {
            Process[] processes = Process.GetProcessesByName("okular");
            Process bestProcess = null;
            long bestArea = -1;

            foreach (Process process in processes)
            {
                IntPtr window = process.MainWindowHandle;
                Rect rect;

                if (window == IntPtr.Zero || !IsWindowVisible(window) || !GetWindowRect(window, out rect))
                {
                    process.Dispose();
                    continue;
                }

                long width = Math.Max(0, rect.Right - rect.Left);
                long height = Math.Max(0, rect.Bottom - rect.Top);
                long area = width * height;

                if (area > bestArea)
                {
                    if (bestProcess != null)
                    {
                        bestProcess.Dispose();
                    }

                    bestArea = area;
                    bestProcess = process;
                }
                else
                {
                    process.Dispose();
                }
            }

            return bestProcess;
        }

        private static HashSet<IntPtr> SnapshotTopLevelWindows()
        {
            HashSet<IntPtr> windows = new HashSet<IntPtr>();
            EnumWindows(
                delegate (IntPtr window, IntPtr parameter)
                {
                    windows.Add(window);
                    return true;
                },
                IntPtr.Zero);
            return windows;
        }

        private static bool ActivateWindow(IntPtr window)
        {
            ShowWindow(window, SwRestore);

            for (int attempt = 0; attempt < 10; attempt++)
            {
                SetForegroundWindow(window);
                Thread.Sleep(50);

                if (GetForegroundWindow() == window)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TitleLooksLikeOpenDialog(string title)
        {
            return title.IndexOf("Abrir", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   title.IndexOf("Open", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int ScoreOpenDialog(
            IntPtr window,
            IntPtr okularWindow,
            uint okularProcessId,
            IntPtr foregroundWindow,
            HashSet<IntPtr> windowsBefore)
        {
            if (window == IntPtr.Zero ||
                window == okularWindow ||
                windowsBefore.Contains(window) ||
                !IsWindowVisible(window))
            {
                return -1;
            }

            string className = GetClass(window);
            string title = GetTitle(window);
            bool standardDialog = string.Equals(className, "#32770", StringComparison.Ordinal);
            bool matchingTitle = TitleLooksLikeOpenDialog(title);
            bool standardFileNameControl = GetDlgItem(window, FileNameComboId) != IntPtr.Zero;

            if (!standardDialog && !matchingTitle && !standardFileNameControl)
            {
                return -1;
            }

            int score = 10000;

            if (window == foregroundWindow)
            {
                score += 2000;
            }

            if (standardDialog)
            {
                score += 2000;
            }

            if (matchingTitle)
            {
                score += 4000;
            }

            if (standardFileNameControl)
            {
                score += 5000;
            }

            uint processId;
            GetWindowThreadProcessId(window, out processId);
            if (processId == okularProcessId)
            {
                score += 6000;
            }

            if (GetWindow(window, GwOwner) == okularWindow)
            {
                score += 6000;
            }

            return score;
        }

        private static IntPtr WaitForOpenDialog(
            IntPtr okularWindow,
            uint okularProcessId,
            HashSet<IntPtr> windowsBefore)
        {
            for (int attempt = 0; attempt < 60; attempt++)
            {
                Thread.Sleep(100);

                IntPtr foregroundWindow = GetForegroundWindow();
                IntPtr bestWindow = IntPtr.Zero;
                int bestScore = -1;

                EnumWindows(
                    delegate (IntPtr window, IntPtr parameter)
                    {
                        int score = ScoreOpenDialog(
                            window,
                            okularWindow,
                            okularProcessId,
                            foregroundWindow,
                            windowsBefore);

                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestWindow = window;
                        }

                        return true;
                    },
                    IntPtr.Zero);

                if (bestWindow != IntPtr.Zero && bestScore >= MinimumOpenDialogScore)
                {
                    return bestWindow;
                }
            }

            return IntPtr.Zero;
        }

        private static IntPtr FindEditInside(IntPtr parent)
        {
            IntPtr result = IntPtr.Zero;

            EnumChildWindows(
                parent,
                delegate (IntPtr window, IntPtr parameter)
                {
                    if (IsWindowVisible(window) &&
                        string.Equals(GetClass(window), "Edit", StringComparison.OrdinalIgnoreCase))
                    {
                        result = window;
                        return false;
                    }

                    return true;
                },
                IntPtr.Zero);

            return result;
        }

        private static IntPtr FindBottomEdit(IntPtr dialog)
        {
            IntPtr result = IntPtr.Zero;
            int bestTop = -1;

            EnumChildWindows(
                dialog,
                delegate (IntPtr window, IntPtr parameter)
                {
                    if (!IsWindowVisible(window) ||
                        !string.Equals(GetClass(window), "Edit", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    Rect rect;
                    if (GetWindowRect(window, out rect) && rect.Top > bestTop)
                    {
                        bestTop = rect.Top;
                        result = window;
                    }

                    return true;
                },
                IntPtr.Zero);

            return result;
        }

        private static IntPtr FindFileNameControl(IntPtr dialog)
        {
            IntPtr combo = GetDlgItem(dialog, FileNameComboId);
            if (combo != IntPtr.Zero)
            {
                IntPtr edit = FindEditInside(combo);
                return edit != IntPtr.Zero ? edit : combo;
            }

            return FindBottomEdit(dialog);
        }

        private static bool WaitForDialogToClose(IntPtr dialog)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                Thread.Sleep(100);
                if (!IsWindow(dialog) || !IsWindowVisible(dialog))
                {
                    return true;
                }
            }

            return false;
        }

        private static void StartOkular(string executablePath, string pdfPath)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = "\"" + pdfPath + "\"",
                WorkingDirectory = Path.GetDirectoryName(executablePath),
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    throw new InvalidOperationException("O Windows não iniciou o processo do Okular.");
                }
            }
        }

        [STAThread]
        private static int Main(string[] arguments)
        {
            Mutex mutex = null;
            bool ownsMutex = false;

            try
            {
                Directory.CreateDirectory(BaseDirectory);

                string pdfPath;
                string validationError;
                if (!TryResolvePdf(arguments, out pdfPath, out validationError))
                {
                    return Fail(ExitInvalidInput, validationError);
                }

                WriteRun(pdfPath);

                mutex = new Mutex(false, MutexName);

                try
                {
                    ownsMutex = mutex.WaitOne(10000);
                }
                catch (AbandonedMutexException)
                {
                    ownsMutex = true;
                }

                if (!ownsMutex)
                {
                    return Fail(ExitMutexTimeout, "Timeout aguardando outra execução do launcher.");
                }

                using (Process okular = FindMainOkular())
                {
                    if (okular == null)
                    {
                        string okularExecutable = FindOkularExecutable();
                        if (okularExecutable == null)
                        {
                            return Fail(
                                ExitOkularNotFound,
                                "Okular não encontrado. Instale-o ou configure a variável " +
                                OkularOverrideVariable + ".");
                        }

                        StartOkular(okularExecutable, pdfPath);
                        ClearPreviousError();
                        return ExitSuccess;
                    }

                    IntPtr mainWindow = okular.MainWindowHandle;
                    if (!ActivateWindow(mainWindow))
                    {
                        return Fail(
                            ExitAutomationFailed,
                            "Não foi possível ativar com segurança a janela principal do Okular.");
                    }

                    HashSet<IntPtr> windowsBefore = SnapshotTopLevelWindows();
                    SendKeys.SendWait("^o");

                    IntPtr dialog = WaitForOpenDialog(
                        mainWindow,
                        unchecked((uint)okular.Id),
                        windowsBefore);

                    if (dialog == IntPtr.Zero)
                    {
                        return Fail(
                            ExitAutomationFailed,
                            "Não foi possível localizar a nova janela Abrir do Okular.");
                    }

                    IntPtr fileNameControl = FindFileNameControl(dialog);
                    if (fileNameControl == IntPtr.Zero)
                    {
                        return Fail(
                            ExitAutomationFailed,
                            "Janela Abrir localizada, mas o campo de nome do arquivo não foi encontrado." +
                            Environment.NewLine + "Título: " + GetTitle(dialog) +
                            Environment.NewLine + "Classe: " + GetClass(dialog));
                    }

                    SendMessage(fileNameControl, WmSetText, IntPtr.Zero, pdfPath);
                    Thread.Sleep(200);

                    IntPtr openButton = GetDlgItem(dialog, IdOk);
                    if (openButton != IntPtr.Zero)
                    {
                        SendMessage(openButton, BmClick, IntPtr.Zero, IntPtr.Zero);
                    }
                    else
                    {
                        if (!ActivateWindow(dialog))
                        {
                            return Fail(
                                ExitAutomationFailed,
                                "O botão Abrir não foi encontrado e o diálogo não pôde ser ativado.");
                        }

                        SendKeys.SendWait("{ENTER}");
                    }

                    if (!WaitForDialogToClose(dialog))
                    {
                        return Fail(
                            ExitAutomationFailed,
                            "O diálogo Abrir permaneceu visível após a confirmação do PDF.");
                    }
                }

                ClearPreviousError();
                return ExitSuccess;
            }
            catch (Exception exception)
            {
                return Fail(ExitUnexpectedError, exception.ToString());
            }
            finally
            {
                if (mutex != null)
                {
                    if (ownsMutex)
                    {
                        try
                        {
                            mutex.ReleaseMutex();
                        }
                        catch (ApplicationException)
                        {
                            // The process is exiting and no longer owns the mutex.
                        }
                    }

                    mutex.Dispose();
                }
            }
        }
    }
}
