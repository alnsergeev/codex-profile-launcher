using System.Diagnostics;

namespace CodexProfileLauncher;

public sealed class CodexLauncherService
{
    private const string CodexAppShellTarget = @"shell:AppsFolder\OpenAI.Codex_2p2nqsd0c76g0!App";

    public bool IsCodexRunning()
    {
        return GetCodexProcesses().Count > 0;
    }

    public void StartCodex()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = CodexAppShellTarget,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Could not start Codex Desktop. Make sure it is installed from the Microsoft Store.", ex);
        }
    }

    public void RestartCodex()
    {
        CloseCodex();
        StartCodex();
    }

    public void CloseCodex()
    {
        var processes = GetCodexProcesses();
        if (processes.Count == 0)
        {
            return;
        }

        foreach (var process in processes)
        {
            TryCloseGracefully(process);
        }

        foreach (var process in processes)
        {
            TryKillIfStillRunning(process);
        }
    }

    private static IReadOnlyList<Process> GetCodexProcesses()
    {
        return Process.GetProcesses()
            .Where(IsCodexDesktopProcess)
            .ToList();
    }

    private static bool IsCodexDesktopProcess(Process process)
    {
        try
        {
            if (!process.ProcessName.Equals("Codex", StringComparison.OrdinalIgnoreCase)
                && !process.ProcessName.Equals("codex", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var path = process.MainModule?.FileName ?? string.Empty;
            return path.Contains(@"\OpenAI.Codex_", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(@"\Codex.exe", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(@"\codex.exe", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void TryCloseGracefully(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return;
            }

            if (process.MainWindowHandle != IntPtr.Zero)
            {
                process.CloseMainWindow();
                process.WaitForExit(5000);
            }
        }
        catch
        {
            // Fallback kill handles stubborn or inaccessible processes.
        }
    }

    private static void TryKillIfStillRunning(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return;
            }

            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
        }
        catch
        {
            // Leave the next start attempt to surface any remaining issue to the user.
        }
    }
}
