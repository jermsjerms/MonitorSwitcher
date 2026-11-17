using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace MonitorSwitcher;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Any(a => a.Equals("--restore", StringComparison.OrdinalIgnoreCase)))
        {
            var c = AppConfig.Load();
            if (c.Switched)
            {
                try { Display.Restore(c.SavedLayout, c.SavedTopology); c.Switched = false; c.Save(); Log.Write("Restored via --restore"); }
                catch (Exception ex)
                {
                    Log.Write("--restore failed: " + ex);
                    MessageBox.Show("Could not restore the display layout:\n" + ex.Message, "Monitor Switcher",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }
            }
            return 0;
        }

        using var mutex = new Mutex(true, "MonitorSwitcher_SingleInstance", out bool first);
        if (!first)
        {
            MessageBox.Show("Monitor Switcher is already running (check the system tray).", "Monitor Switcher",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 1;
        }

        bool auto = args.Any(a => a.Equals("--auto", StringComparison.OrdinalIgnoreCase));
        Application.Run(new MainForm(AppConfig.Load(), auto));
        return 0;
    }
}

static class Log
{
    public static void Write(string msg)
    {
        try
        {
            var dir = Path.Combine(AppConfig.Dir, "logs");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, $"{DateTime.Now:yyyy-MM-dd}.log"),
                $"{DateTime.Now:HH:mm:ss}  {msg}{Environment.NewLine}");
        }
        catch { /* logging must never crash the app */ }
    }
}