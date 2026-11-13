using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace MonitorSwitcher;

// ───────────────────────────── Entry point ─────────────────────────────
static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // --restore : put the saved layout back and exit (handy as a panic shortcut)
        if (args.Any(a => a.Equals("--restore", StringComparison.OrdinalIgnoreCase)))
        {
            var c = AppConfig.Load();
            if (c.Switched)
            {
                try { Display.Restore(c.SavedLayout); c.Switched = false; c.Save(); Log.Write("Restored via --restore"); }
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

// ───────────────────────────── Logging ─────────────────────────────
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

// ───────────────────────────── Config (config.json next to the exe) ─────────────────────────────
class MonInfo
{
    public string Key { get; set; } = "";      // stable hardware-based ID
    public string Name { get; set; } = "";
    public string Adapter { get; set; } = "";  // e.g. \\.\DISPLAY1
    public bool Attached { get; set; }
    public bool Primary { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int Freq { get; set; }
    public int Bits { get; set; }
    public int Orientation { get; set; }

    [JsonIgnore]
    public string HwCode
    {
        get
        {
            var parts = Key.Split('\\');
            return parts.Length > 1 && parts[0] == "MONITOR" ? parts[1] : "";
        }
    }

    [JsonIgnore]
    public string Label => HwCode.Length > 0 ? $"{Name} [{HwCode}]" : Name;
}

class AppConfig
{
    public string DefaultMonitorKey { get; set; } = "";
    public bool TurnOffOthers { get; set; } = false;
    public bool AutoApplyOnLaunch { get; set; } = false;
    public bool ConfirmChanges { get; set; } = true;

    // Crash-safe state: the layout from before we switched anything
    public bool Switched { get; set; } = false;
    public List<MonInfo> SavedLayout { get; set; } = new();

    public static readonly string Dir = ResolveDir();
    static string FilePath => Path.Combine(Dir, "config.json");

    static string ResolveDir()
    {
        string d = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        try
        {
            var t = Path.Combine(d, ".write_test");
            File.WriteAllText(t, "x");
            File.Delete(t);
            return d;
        }
        catch
        {
            var a = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MonitorSwitcher");
            Directory.CreateDirectory(a);
            return a;
        }
    }

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath)) ?? new AppConfig();
        }
        catch (Exception ex) { Log.Write("Config load failed: " + ex.Message); }
        return new AppConfig();
    }

    public void Save()
    {
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); }
        catch (Exception ex) { Log.Write("Config save failed: " + ex.Message); }
    }
}

// ───────────────────────────── Windows display API wrapper ─────────────────────────────
static class Display
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public ushort dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public uint dmFields;
        public int dmPositionX, dmPositionY;
        public uint dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency,
                    dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2,
                    dmPanningWidth, dmPanningHeight;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool EnumDisplayDevices(string device, uint index, ref DISPLAY_DEVICE dd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool EnumDisplaySettings(string device, int mode, ref DEVMODE dm);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int ChangeDisplaySettingsEx(string device, ref DEVMODE dm, IntPtr hwnd, uint flags, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int ChangeDisplaySettingsEx(string device, IntPtr dm, IntPtr hwnd, uint flags, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern int SetDisplayConfig(uint numPaths, IntPtr paths, uint numModes, IntPtr modes, uint flags);
    const uint SDC_TOPOLOGY_EXTEND = 0x4, SDC_APPLY = 0x80, SDC_ALLOW_CHANGES = 0x400;

    const int ENUM_CURRENT_SETTINGS = -1, ENUM_REGISTRY_SETTINGS = -2;
    const int DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x1, DISPLAY_DEVICE_PRIMARY_DEVICE = 0x4, DISPLAY_DEVICE_MIRRORING_DRIVER = 0x8;
    const uint DM_POSITION = 0x20, DM_DISPLAYORIENTATION = 0x80, DM_BITSPERPEL = 0x40000,
               DM_PELSWIDTH = 0x80000, DM_PELSHEIGHT = 0x100000, DM_DISPLAYFREQUENCY = 0x400000;
    const uint CDS_UPDATEREGISTRY = 0x1, CDS_SET_PRIMARY = 0x10, CDS_NORESET = 0x10000000;

    static DISPLAY_DEVICE NewDev() => new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
    static DEVMODE NewMode() => new DEVMODE { dmSize = (ushort)Marshal.SizeOf<DEVMODE>() };

    public static List<MonInfo> Enumerate()
    {
        var list = new List<MonInfo>();
        for (uint i = 0; ; i++)
        {
            var a = NewDev();
            if (!EnumDisplayDevices(null, i, ref a, 0)) break;
            if ((a.StateFlags & DISPLAY_DEVICE_MIRRORING_DRIVER) != 0) continue;

            bool attached = (a.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0;
            var m = NewDev();
            bool hasMonitor = EnumDisplayDevices(a.DeviceName, 0, ref m, 0);
            if (!attached && !hasMonitor) continue;

            var dm = NewMode();
            if (!EnumDisplaySettings(a.DeviceName, attached ? ENUM_CURRENT_SETTINGS : ENUM_REGISTRY_SETTINGS, ref dm)) continue;
            if (!attached && dm.dmPelsWidth == 0) continue;

            list.Add(new MonInfo
            {
                Key = hasMonitor ? m.DeviceID : a.DeviceName,
                Name = hasMonitor ? m.DeviceString : a.DeviceString,
                Adapter = a.DeviceName,
                Attached = attached,
                Primary = (a.StateFlags & DISPLAY_DEVICE_PRIMARY_DEVICE) != 0,
                X = dm.dmPositionX,
                Y = dm.dmPositionY,
                Width = (int)dm.dmPelsWidth,
                Height = (int)dm.dmPelsHeight,
                Freq = (int)dm.dmDisplayFrequency,
                Bits = (int)dm.dmBitsPerPel,
                Orientation = (int)dm.dmDisplayOrientation
            });
        }

        // Identical monitors can share the same hardware ID: make keys unique (#2, #3 ...)
        foreach (var g in list.GroupBy(x => x.Key).Where(g => g.Count() > 1))
        {
            int n = 1;
            foreach (var mon in g) { if (n > 1) mon.Key += "#" + n; n++; }
        }
        return list;
    }

    static DEVMODE Build(MonInfo m, int x, int y)
    {
        var dm = NewMode();
        dm.dmFields = DM_POSITION | DM_PELSWIDTH | DM_PELSHEIGHT | DM_BITSPERPEL | DM_DISPLAYFREQUENCY | DM_DISPLAYORIENTATION;
        dm.dmPositionX = x;
        dm.dmPositionY = y;
        dm.dmPelsWidth = (uint)m.Width;
        dm.dmPelsHeight = (uint)m.Height;
        dm.dmBitsPerPel = (uint)(m.Bits > 0 ? m.Bits : 32);
        dm.dmDisplayFrequency = (uint)m.Freq;
        dm.dmDisplayOrientation = (uint)m.Orientation;
        return dm;
    }

    static string Describe(int r) => r switch
    {
        1 => "A restart is required for the change to take effect",
        -1 => "The display driver failed the request",
        -2 => "The requested mode is not supported",
        -3 => "Settings could not be written to the registry",
        -4 => "Bad flags",
        -5 => "Bad parameter",
        _ => $"Error code {r}"
    };

    static void Commit(List<(string adapter, DEVMODE dm, uint flags)> plan)
    {
        foreach (var p in plan)
        {
            var dm = p.dm;
            int r = ChangeDisplaySettingsEx(p.adapter, ref dm, IntPtr.Zero, p.flags, IntPtr.Zero);
            if (r == -2 && dm.dmPelsWidth != 0)   // BADMODE: retry with only position + resolution
            {
                Log.Write($"{p.adapter}: full mode rejected, retrying with position + resolution only");
                dm.dmFields &= ~(DM_BITSPERPEL | DM_DISPLAYFREQUENCY | DM_DISPLAYORIENTATION);
                r = ChangeDisplaySettingsEx(p.adapter, ref dm, IntPtr.Zero, p.flags, IntPtr.Zero);
            }
            if (r != 0) throw new InvalidOperationException($"{p.adapter}: {Describe(r)}");
        }
        int final = ChangeDisplaySettingsEx(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
        if (final != 0) throw new InvalidOperationException(Describe(final));
    }

    /// <summary>Make <paramref name="target"/> the primary display (re-positioning the others around it).</summary>
    public static void SetPrimary(MonInfo target, List<MonInfo> current, bool disableOthers)
    {
        int ox = target.X, oy = target.Y;
        const uint baseFlags = CDS_UPDATEREGISTRY | CDS_NORESET;
        var plan = new List<(string, DEVMODE, uint)>
        {
            (target.Adapter, Build(target, 0, 0), baseFlags | CDS_SET_PRIMARY)
        };

        foreach (var o in current.Where(o => o.Attached && o.Adapter != target.Adapter))
        {
            if (disableOthers)
            {
                var off = NewMode();
                off.dmFields = DM_POSITION | DM_PELSWIDTH | DM_PELSHEIGHT;   // 0x0 size = detach
                plan.Add((o.Adapter, off, baseFlags));
            }
            else
            {
                plan.Add((o.Adapter, Build(o, o.X - ox, o.Y - oy), baseFlags));
            }
        }
        Commit(plan);
    }

    /// <summary>Put a previously saved layout back (re-enabling monitors that were switched off).</summary>
    public static void Restore(List<MonInfo> layout)
    {
        try
        {
            RestoreCore(layout, false);
        }
        catch (Exception first)
        {
            Log.Write("Restore attempt 1 failed: " + first.Message + "  -> trying fallback (re-enable all monitors, then reposition)");
            int r = SetDisplayConfig(0, IntPtr.Zero, 0, IntPtr.Zero, SDC_APPLY | SDC_TOPOLOGY_EXTEND | SDC_ALLOW_CHANGES);
            Log.Write("SetDisplayConfig(extend) returned " + r);
            Thread.Sleep(2000);
            try
            {
                RestoreCore(layout, true);
            }
            catch (Exception second)
            {
                throw new InvalidOperationException(first.Message + "  |  fallback: " + second.Message);
            }
        }
    }

    // useCurrentModes: keep whatever resolution/refresh each monitor is running right now and only
    // put the saved positions + primary flag back (monitors matched by hardware key).
    static void RestoreCore(List<MonInfo> layout, bool useCurrentModes)
    {
        const uint baseFlags = CDS_UPDATEREGISTRY | CDS_NORESET;
        var attached = layout.Where(m => m.Attached).ToList();
        var primary = attached.FirstOrDefault(m => m.Primary) ?? attached.FirstOrDefault();
        if (primary == null) return;

        var current = useCurrentModes ? Enumerate() : null;
        (string, DEVMODE, uint) Entry(MonInfo m, uint flags)
        {
            var src = m;
            if (current != null)
            {
                var cur = current.FirstOrDefault(c => c.Key == m.Key && c.Attached);
                if (cur != null) src = cur;
            }
            var dm = Build(src, m.X, m.Y);
            return (src.Adapter, dm, flags);
        }

        var plan = new List<(string, DEVMODE, uint)> { Entry(primary, baseFlags | CDS_SET_PRIMARY) };
        foreach (var m in attached.Where(m => m != primary))
            plan.Add(Entry(m, baseFlags));
        Commit(plan);
    }
}

// ───────────────────────────── "Keep these settings?" dialog ─────────────────────────────
class ConfirmForm : Form
{
    int left;
    readonly Label lbl = new();
    readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };

    public ConfirmForm(int seconds)
    {
        left = seconds;
        Text = "Keep display settings?";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        ControlBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(380, 120);

        lbl.SetBounds(15, 15, 350, 40);
        UpdateText();

        var keep = new Button { Text = "Keep", DialogResult = DialogResult.Yes, Left = 100, Top = 70, Width = 80 };
        var revert = new Button { Text = "Revert", DialogResult = DialogResult.No, Left = 200, Top = 70, Width = 80 };
        Controls.AddRange(new Control[] { lbl, keep, revert });
        AcceptButton = keep;

        timer.Tick += (_, _) =>
        {
            left--;
            if (left <= 0) { timer.Stop(); DialogResult = DialogResult.No; }
            else UpdateText();
        };
        Shown += (_, _) => timer.Start();
        FormClosed += (_, _) => timer.Dispose();
    }

    void UpdateText() => lbl.Text = $"Your display layout was changed.\nReverting automatically in {left} seconds unless you keep it.";
}

// ───────────────────────────── Main window ─────────────────────────────
class MainForm : Form
{
    const int WM_HOTKEY = 0x0312, HOTKEY_ID = 1;
    const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2;
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    readonly AppConfig cfg;
    readonly bool autoArg;
    List<MonInfo> monitors = new();

    readonly ListView lv = new();
    readonly CheckBox chkOff = new() { Text = "Turn off other monitors while active", AutoSize = true };
    readonly CheckBox chkAuto = new() { Text = "Apply default monitor automatically when app opens", AutoSize = true };
    readonly CheckBox chkConfirm = new() { Text = "Ask me to confirm changes (auto-reverts in 15 s)", AutoSize = true };
    readonly Label lblStatus = new() { AutoSize = true, Padding = new Padding(0, 8, 0, 0) };
    readonly NotifyIcon tray = new();
    bool hotkeyOk;
    string lastRestoreError = "";

    public MainForm(AppConfig c, bool auto)
    {
        cfg = c;
        autoArg = auto;

        Text = "Monitor Switcher";
        ClientSize = new Size(860, 480);
        MinimumSize = new Size(780, 420);
        StartPosition = FormStartPosition.CenterScreen;

        lv.Dock = DockStyle.Fill;
        lv.View = View.Details;
        lv.FullRowSelect = true;
        lv.MultiSelect = false;
        lv.HideSelection = false;
        lv.GridLines = true;
        lv.Columns.Add("Monitor", 270);
        lv.Columns.Add("Display", 80);
        lv.Columns.Add("Resolution", 100);
        lv.Columns.Add("Refresh", 70);
        lv.Columns.Add("Position", 90);
        lv.Columns.Add("Status", 160);

        chkOff.Checked = cfg.TurnOffOthers;
        chkAuto.Checked = cfg.AutoApplyOnLaunch;
        chkConfirm.Checked = cfg.ConfirmChanges;
        chkOff.CheckedChanged += (_, _) => { cfg.TurnOffOthers = chkOff.Checked; cfg.Save(); };
        chkAuto.CheckedChanged += (_, _) => { cfg.AutoApplyOnLaunch = chkAuto.Checked; cfg.Save(); };
        chkConfirm.CheckedChanged += (_, _) => { cfg.ConfirmChanges = chkConfirm.Checked; cfg.Save(); };

        var btnApply = new Button { Text = "Switch to selected monitor", AutoSize = true };
        var btnDefault = new Button { Text = "Set selected as default", AutoSize = true };
        var btnRestore = new Button { Text = "Restore previous setup", AutoSize = true };
        var btnRefresh = new Button { Text = "Refresh", AutoSize = true };
        btnApply.Click += (_, _) => OnApplyClicked();
        btnDefault.Click += (_, _) => OnSetDefault();
        btnRestore.Click += (_, _) => { if (RestoreLayout(false)) RefreshList(); };
        btnRefresh.Click += (_, _) => RefreshList();

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        buttons.Controls.AddRange(new Control[] { btnApply, btnDefault, btnRestore, btnRefresh });

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(10)
        };
        bottom.Controls.AddRange(new Control[] { chkOff, chkAuto, chkConfirm, buttons, lblStatus });

        Controls.Add(lv);
        Controls.Add(bottom);
        lv.BringToFront();

        // Tray icon
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show window", null, (_, _) => ShowFromTray());
        menu.Items.Add("Switch to default monitor", null, (_, _) => ApplyDefault());
        menu.Items.Add("Restore previous setup  (Ctrl+Alt+M)", null, (_, _) => HotkeyRestore());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit (restores previous setup)", null, (_, _) => Close());
        tray.ContextMenuStrip = menu;
        tray.Icon = SystemIcons.Application;
        tray.Text = "Monitor Switcher";
        tray.DoubleClick += (_, _) => ShowFromTray();
        tray.Visible = true;

        Resize += (_, _) => { if (WindowState == FormWindowState.Minimized) Hide(); };
        Shown += (_, _) => OnStartup();
        SystemEvents.SessionEnding += (_, _) => RestoreLayout(true);
    }

    // ── Startup ──
    void OnStartup()
    {
        RefreshList();

        if (cfg.Switched)
        {
            var r = MessageBox.Show(this,
                "Last time, Monitor Switcher did not get to restore your original display layout.\n\nRestore it now?",
                "Monitor Switcher", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r == DialogResult.Yes) { RestoreLayout(false); RefreshList(); }
        }

        if (autoArg || cfg.AutoApplyOnLaunch)
        {
            if (ApplyDefault()) HideToTray("Switched. Close this app (or press Ctrl+Alt+M) to restore your setup.");
        }
    }

    // ── Actions ──
    MonInfo SelectedOrDefault() =>
        lv.SelectedItems.Count > 0
            ? (MonInfo)lv.SelectedItems[0].Tag
            : monitors.FirstOrDefault(m => m.Key == cfg.DefaultMonitorKey);

    void OnApplyClicked()
    {
        var t = SelectedOrDefault();
        if (t == null) { MessageBox.Show(this, "Select a monitor in the list first.", "Monitor Switcher"); return; }
        ApplySwitch(t);
    }

    void OnSetDefault()
    {
        if (lv.SelectedItems.Count == 0) { MessageBox.Show(this, "Select a monitor in the list first.", "Monitor Switcher"); return; }
        cfg.DefaultMonitorKey = ((MonInfo)lv.SelectedItems[0].Tag).Key;
        cfg.Save();
        RefreshList();
    }

    bool ApplyDefault()
    {
        RefreshListKeepSelection();
        var t = monitors.FirstOrDefault(m => m.Key == cfg.DefaultMonitorKey);
        if (t == null)
        {
            Log.Write("Default monitor not found/connected");
            tray.ShowBalloonTip(4000, "Monitor Switcher", "Default monitor is not set or not connected.", ToolTipIcon.Warning);
            return false;
        }
        return ApplySwitch(t);
    }

    bool ApplySwitch(MonInfo target)
    {
        bool firstSwitch = !cfg.Switched;
        try
        {
            var current = Display.Enumerate();
            if (firstSwitch)
            {
                cfg.SavedLayout = current;   // snapshot ONLY the first time, so we always return to the true original
                cfg.Switched = true;
                cfg.Save();
            }
            Display.SetPrimary(target, current, cfg.TurnOffOthers);
            Log.Write($"Primary set to {target.Label} ({target.Adapter}); turnOffOthers={cfg.TurnOffOthers}");
        }
        catch (Exception ex)
        {
            Log.Write("Apply failed: " + ex);
            if (firstSwitch)
            {
                try { Display.Restore(cfg.SavedLayout); } catch (Exception ex2) { Log.Write("Rollback failed: " + ex2.Message); }
                cfg.Switched = false;
                cfg.Save();
            }
            RefreshList();
            MessageBox.Show(this, "Could not change the primary monitor:\n" + ex.Message +
                "\n\nTip: check the log file in the 'logs' folder.", "Monitor Switcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        RefreshList();

        if (cfg.ConfirmChanges)
        {
            using var dlg = new ConfirmForm(15);
            if (dlg.ShowDialog() != DialogResult.Yes)
            {
                RestoreLayout(true);
                RefreshList();
                return false;
            }
        }
        return true;
    }

    bool RestoreLayout(bool quiet)
    {
        if (!cfg.Switched || cfg.SavedLayout.Count == 0)
        {
            if (!quiet) MessageBox.Show(this, "Nothing to restore: the layout hasn't been changed.", "Monitor Switcher");
            return true;
        }
        try
        {
            Display.Restore(cfg.SavedLayout);
            cfg.Switched = false;
            cfg.Save();
            Log.Write("Original layout restored");
            return true;
        }
        catch (Exception ex)
        {
            Log.Write("Restore failed: " + ex);
            lastRestoreError = ex.Message;
            if (!quiet)
                MessageBox.Show(this, "Could not restore the layout:\n" + ex.Message, "Monitor Switcher",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    void HotkeyRestore()
    {
        if (!cfg.Switched)
        {
            tray.ShowBalloonTip(2500, "Monitor Switcher", "Nothing to restore.", ToolTipIcon.Info);
            return;
        }
        bool ok = RestoreLayout(true);
        RefreshList();
        tray.ShowBalloonTip(3000, "Monitor Switcher",
            ok ? "Previous display setup restored." : "Restore failed. See the log file.",
            ok ? ToolTipIcon.Info : ToolTipIcon.Error);
    }

    // ── UI helpers ──
    void RefreshListKeepSelection() => monitors = Display.Enumerate();

    void RefreshList()
    {
        monitors = Display.Enumerate();
        lv.Items.Clear();
        foreach (var m in monitors)
        {
            var it = new ListViewItem(m.Label) { Tag = m };
            it.SubItems.Add(m.Adapter.Replace(@"\\.\", ""));
            it.SubItems.Add(m.Attached ? $"{m.Width} x {m.Height}" : "Off");
            it.SubItems.Add(m.Attached ? $"{m.Freq} Hz" : "-");
            it.SubItems.Add($"{m.X}, {m.Y}");
            var tags = new List<string>();
            if (m.Primary) tags.Add("Primary");
            if (m.Key == cfg.DefaultMonitorKey) tags.Add("★ Default");
            if (!m.Attached) tags.Add("Disabled");
            it.SubItems.Add(string.Join(", ", tags));
            lv.Items.Add(it);
        }
        lblStatus.Text = cfg.Switched
            ? "ACTIVE: display layout is switched. Close this app (or press Ctrl+Alt+M) to restore your original setup."
            : "Idle: your layout is unchanged.";
        if (!hotkeyOk) lblStatus.Text += "  (Hotkey Ctrl+Alt+M is unavailable: another app uses it.)";
    }

    void HideToTray(string message)
    {
        Hide();
        tray.ShowBalloonTip(4000, "Monitor Switcher", message, ToolTipIcon.Info);
    }

    void ShowFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        RefreshList();
    }

    // ── Hotkey (Ctrl+Alt+M) ──
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        hotkeyOk = RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL | MOD_ALT, (uint)Keys.M);
        if (!hotkeyOk) Log.Write("Could not register Ctrl+Alt+M");
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        UnregisterHotKey(Handle, HOTKEY_ID);
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            BeginInvoke(new Action(HotkeyRestore));
        base.WndProc(ref m);
    }

    // ── Closing = restore previous setup ──
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (cfg.Switched && !RestoreLayout(true))
        {
            bool shuttingDown = e.CloseReason == CloseReason.WindowsShutDown;
            if (!shuttingDown)
            {
                var r = MessageBox.Show(this,
                    "Your original layout could not be restored.\n\nReason: " + lastRestoreError +
                    "\n\nExit anyway? (You can restore later by running MonitorSwitcher.exe --restore, or on the next launch.)",
                    "Monitor Switcher", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r == DialogResult.No) { e.Cancel = true; return; }
            }
        }
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        tray.Visible = false;
        tray.Dispose();
        base.OnFormClosed(e);
    }
}
