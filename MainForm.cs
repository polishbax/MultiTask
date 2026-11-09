using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace TinyTaskSafeClone;

public partial class MainForm : Form
{
    private readonly List<MacroEvent> events = new();
    private readonly object eventsLock = new();
    private bool ignoreInjectedInput;
    private long lastRecordedMoveTicks;
    private int lastRecordedMoveX;
    private int lastRecordedMoveY;
    private Stopwatch? stopwatch;
    private LowLevelHook? hook;
    private CancellationTokenSource? playbackCts;
    private bool recording;
    
    private Button recordButton = null!;
    private Button stopButton = null!;
    private Button playButton = null!;
    private Button saveButton = null!;
    private Button loadButton = null!;
    private Label statusLabel = null!;
    private NumericUpDown repeatBox = null!;

    public MainForm()
    {
        InitializeComponent();
        hook = new LowLevelHook(OnMouseEvent, OnKeyboardEvent);
        FormClosed += (_, _) => hook?.Dispose();
    }

    private TextBox diagnosticsBox = null!;
    private CheckBox refreshAfterClick = null!;
    private NumericUpDown timerBox = null!;
    private Panel diagnosticsPanel = null!;
    private Button diagnosticsToggle = null!;
    private CheckBox movementRefresh = null!;
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID_F8 = 0x5001;
    private const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RegisterHotKey(Handle, HOTKEY_ID_F8, MOD_NOREPEAT, (uint)Keys.F8);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        if (IsHandleCreated)
            UnregisterHotKey(Handle, HOTKEY_ID_F8);
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID_F8)
        {
            BeginInvoke(new Action(StopEverything));
            return;
        }
        base.WndProc(ref m);
    }

    private void InitializeComponent()
    {
        Text = "Multitask";
        ClientSize = new Size(680, 425);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Color.FromArgb(245, 247, 250);
        Font = new Font("Segoe UI", 10F);

        var title = new Label { Text = "TinyTask Safe Clone", Font = new Font("Segoe UI", 22F, FontStyle.Bold),
            Dock = DockStyle.Top, Height = 58, TextAlign = ContentAlignment.MiddleCenter };
        statusLabel = new Label { Text = "Gotowe — v7", Dock = DockStyle.Top, Height = 32,
            TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(90,98,110) };

        var panel = new Panel { Location = new Point(28,98), Size = new Size(624,225),
            BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };

        recordButton = new Button { Text="●  NAGRYWAJ", Size=new Size(175,50), Location=new Point(16,16),
            BackColor=Color.FromArgb(220,65,65), ForeColor=Color.White, FlatStyle=FlatStyle.Flat,
            Font=new Font("Segoe UI",10F,FontStyle.Bold) };
        recordButton.FlatAppearance.BorderSize=0;
        stopButton = new Button { Text="■  STOP", Size=new Size(175,50), Location=new Point(224,16),
            BackColor=Color.FromArgb(55,62,72), ForeColor=Color.White, FlatStyle=FlatStyle.Flat,
            Font=new Font("Segoe UI",10F,FontStyle.Bold), Enabled=false };
        stopButton.FlatAppearance.BorderSize=0;
        playButton = new Button { Text="▶  ODTWÓRZ", Size=new Size(175,50), Location=new Point(432,16),
            BackColor=Color.FromArgb(55,125,225), ForeColor=Color.White, FlatStyle=FlatStyle.Flat,
            Font=new Font("Segoe UI",10F,FontStyle.Bold), Enabled=false };
        playButton.FlatAppearance.BorderSize=0;

        var options = new Panel {
            Location=new Point(28,82), Size=new Size(564,150),
            BorderStyle=BorderStyle.FixedSingle
        };

        var repeatLabel = new Label { Text="Powtórzenia", AutoSize=true, Location=new Point(78,18) };
        repeatBox = new NumericUpDown {
            Minimum=1, Maximum=9999999999999999999999999999M, Value=1,
            Width=115, Location=new Point(158,14)
        };
        saveButton = new Button { Text="Zapisz", Size=new Size(88,32), Location=new Point(285,11), FlatStyle=FlatStyle.Flat };
        loadButton = new Button { Text="Wczytaj", Size=new Size(88,32), Location=new Point(382,11), FlatStyle=FlatStyle.Flat };

        var timerLabel = new Label { Text="Limit czasu", AutoSize=true, Location=new Point(78,61) };
        timerBox = new NumericUpDown {
            Minimum=0, Maximum=720, DecimalPlaces=2, Increment=0.25M,
            Value=0, Width=82, Location=new Point(158,57)
        };
        var timerUnit = new Label { Text="godz.", AutoSize=true, Location=new Point(245,61) };
        var timerHint = new Label {
            Text="0 = bez limitu", AutoSize=true, ForeColor=Color.DimGray, Location=new Point(295,61)
        };

        refreshAfterClick = new CheckBox {
            Text="Odśwież UI po kliknięciu (Buy)", AutoSize=true, Checked=true, Location=new Point(78,101)
        };
        movementRefresh = new CheckBox {
            Text="Roblox Movement", AutoSize=true, Checked=true, Location=new Point(345,101)
        };

        options.Controls.AddRange([
            repeatLabel,repeatBox,saveButton,loadButton,
            timerLabel,timerBox,timerUnit,timerHint,
            refreshAfterClick,movementRefresh
        ]);

        var info = new Label { Text="Mysz • kliknięcia • scroll • klawiatura  |  F8 = STOP",
            Size=new Size(580,24), Location=new Point(22,202), TextAlign=ContentAlignment.MiddleCenter,
            ForeColor=Color.FromArgb(105,112,124) };
        panel.Controls.AddRange([recordButton,stopButton,playButton,options,info]);

        diagnosticsToggle = new Button {
            Text="▼  Rejestry / diagnostyka", Size=new Size(624,36),
            Location=new Point(28,335), FlatStyle=FlatStyle.Flat,
            TextAlign=ContentAlignment.MiddleLeft
        };
        diagnosticsPanel = new Panel {
            Location=new Point(28,377), Size=new Size(624,120),
            BackColor=Color.White, BorderStyle=BorderStyle.FixedSingle
        };
        diagnosticsBox = new TextBox {
            Location=new Point(8,8), Size=new Size(606,103),
            Multiline=true, ReadOnly=true, ScrollBars=ScrollBars.Vertical,
            BackColor=Color.White, Font=new Font("Consolas",9.5F)
        };
        diagnosticsPanel.Controls.Add(diagnosticsBox);
        diagnosticsPanel.Visible = false;

        diagnosticsToggle.Click += (_,_) => {
            diagnosticsPanel.Visible=!diagnosticsPanel.Visible;
            diagnosticsToggle.Text=diagnosticsPanel.Visible
                ? "▼  Rejestry / diagnostyka"
                : "▶  Rejestry / diagnostyka";
            ClientSize=diagnosticsPanel.Visible ? new Size(680,555) : new Size(680,425);
        };
        var hint = new Label { Text="v7 • F8 STOP • SCAN CODE • zwijane rejestry",
            Dock=DockStyle.Bottom, Height=38, TextAlign=ContentAlignment.MiddleCenter,
            ForeColor=Color.FromArgb(110,116,126) };

        Controls.Add(hint); Controls.Add(diagnosticsPanel); Controls.Add(diagnosticsToggle); Controls.Add(panel);
        Controls.Add(statusLabel); Controls.Add(title);

        recordButton.Click += (_,_) => StartRecording();
        stopButton.Click += (_,_) => StopEverything();
        playButton.Click += async (_,_) => await PlayAsync();
        saveButton.Click += (_,_) => SaveMacro();
        loadButton.Click += (_,_) => LoadMacro();
    }

    private void StartRecording()
    {
        if (recording) return;
        playbackCts?.Cancel();

        events.Clear();
        hook?.ResetHeldKeys();
        stopwatch = Stopwatch.StartNew();
        lastRecordedMoveTicks = 0;
        lastRecordedMoveX = int.MinValue;
        lastRecordedMoveY = int.MinValue;
        recording = true;

        recordButton.Enabled = false;
        stopButton.Enabled = true;
        playButton.Enabled = false;
        statusLabel.Text = "NAGRYWANIE — F8 zatrzymuje";
        if (diagnosticsBox != null) diagnosticsBox.Text = "Nagrywanie w toku...";
    }

    private void UpdateDiagnostics(string extra = "")
    {
        List<MacroEvent> snapshot;
        lock (eventsLock) snapshot = events.ToList();

        int moves=snapshot.Count(e=>e.Type=="move");
        int down=snapshot.Count(e=>e.Type=="click" && e.Down);
        int up=snapshot.Count(e=>e.Type=="click" && !e.Down);
        int kd=snapshot.Count(e=>e.Type=="key_down");
        int ku=snapshot.Count(e=>e.Type=="key_up");
        int scroll=snapshot.Count(e=>e.Type=="scroll");

        var lines=new List<string>{
            $"Zdarzenia  : {snapshot.Count}",
            $"Ruch myszy : {moves}",
            $"Klik DOWN  : {down}",
            $"Klik UP    : {up}",
            $"Klawiatura : DOWN {kd} / UP {ku}",
            $"Scroll     : {scroll}"
        };
        if(snapshot.Count>0){
            lines.Add($"Czas       : {snapshot.Last().Time-snapshot.First().Time:0.000} s");
            lines.Add(""); lines.Add("Ostatnie kliknięcia:");
            foreach(var e in snapshot.Where(e=>e.Type=="click"&&e.Down).TakeLast(10))
                lines.Add($"  {e.Button,-6} X={e.X,5} Y={e.Y,5} t={e.Time,8:0.000}s");
        }
        if(!string.IsNullOrWhiteSpace(extra)){ lines.Add(""); lines.Add(extra); }
        if(diagnosticsBox!=null) diagnosticsBox.Text=string.Join(Environment.NewLine,lines);
    }

    private void StopEverything()
    {
        recording = false;
        stopwatch?.Stop();
        playbackCts?.Cancel();

        recordButton.Enabled = true;
        stopButton.Enabled = false;
        playButton.Enabled = events.Count > 0;
        lock (eventsLock)
            {
                int moves = events.Count(e => e.Type == "move");
                int clicks = events.Count(e => e.Type == "click" && e.Down);
                int keys = events.Count(e => e.Type.StartsWith("key_"));
                statusLabel.Text = $"Gotowe — {events.Count} zdarzeń | ruch: {moves} | klik: {clicks} | klawisze: {keys}";
            }
    }

    private void AddRecordedEvent(MacroEvent e)
    {
        if (!recording || stopwatch == null || ignoreInjectedInput) return;

        e.Time = stopwatch.Elapsed.TotalSeconds;

        lock (eventsLock)
        {
            // Mouse movement is sampled instead of storing every WM_MOUSEMOVE.
            // This keeps the recording manageable while preserving the real path.
            if (e.Type == "move")
            {
                long now = Stopwatch.GetTimestamp();

                if (lastRecordedMoveTicks != 0)
                {
                    double elapsedMs =
                        (now - lastRecordedMoveTicks) * 1000.0 / Stopwatch.Frequency;

                    int dx = e.X - lastRecordedMoveX;
                    int dy = e.Y - lastRecordedMoveY;
                    double distance = Math.Sqrt(dx * dx + dy * dy);

                    // Ignore extremely small duplicate/coalesced movement events.
                    // Always keep a movement if the cursor travelled far enough.
                    if (elapsedMs < 6 && distance < 3)
                        return;
                }

                lastRecordedMoveTicks = now;
                lastRecordedMoveX = e.X;
                lastRecordedMoveY = e.Y;
            }

            events.Add(e);
        }
    }

    private void OnMouseEvent(MacroEvent e) => AddRecordedEvent(e);

    private void OnKeyboardEvent(MacroEvent e)
    {
        if (e.Key == "F8" && e.Type == "key_down")
        {
            if (IsHandleCreated)
                BeginInvoke(StopEverything);
            return;
        }

        AddRecordedEvent(e);
    }

    private async Task PlayAsync()
    {
        if (recording) return;

        List<MacroEvent> snapshot;
        lock (eventsLock)
        {
            if (events.Count == 0) return;
            snapshot = events.Select(e => new MacroEvent {
                Time = e.Time, Type = e.Type, X = e.X, Y = e.Y,
                Button = e.Button, Down = e.Down, Key = e.Key, ScanCode = e.ScanCode, Extended = e.Extended, Delta = e.Delta
            }).ToList();
        }

        playbackCts?.Cancel();
        playbackCts = new CancellationTokenSource();
        var token = playbackCts.Token;

        recordButton.Enabled = false;
        stopButton.Enabled = true;
        playButton.Enabled = false;
        statusLabel.Text = "ODTWARZANIE — F8 zatrzymuje";

        try
        {
            decimal repeats = repeatBox.Value;
            DateTime deadline = timerBox.Value > 0
                ? DateTime.UtcNow.AddHours((double)timerBox.Value)
                : DateTime.MaxValue;

            ignoreInjectedInput = true;

            for (decimal r = 0; r < repeats; r++)
            {
                if (DateTime.UtcNow >= deadline)
                    break;

                double previous = 0;

                foreach (var e in snapshot)
                {
                    token.ThrowIfCancellationRequested();

                    double delay = Math.Max(0, e.Time - previous);
                    await DelayPrecisely(delay, token);
                    previous = e.Time;

                    if (e.Type == "move")
                    {
                        InputSender.Send(e);
                    }
                    else if (e.Type == "click" && e.Down)
                    {
                        // Put the cursor exactly on the recorded click point and
                        // give the target application a short moment to register it.
                        InputSender.SetCursor(e.X, e.Y);
                        await Task.Delay(70, token);
                        InputSender.Send(e);
                    }
                    else
                    {
                        InputSender.Send(e);

                        if (e.Type == "click" && !e.Down && refreshAfterClick.Checked)
                        {
                            await Task.Delay(80, token);
                            InputSender.RefreshMouseAt(e.X, e.Y);
                        }

                        if ((e.Type == "key_down" || e.Type == "key_up")
                            && movementRefresh.Checked)
                        {
                            await Task.Delay(2, token);
                            InputSender.RefreshMouseAt(e.X, e.Y);
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            ignoreInjectedInput = false;
            recordButton.Enabled = true;
            stopButton.Enabled = false;
            playButton.Enabled = snapshot.Count > 0;
            statusLabel.Text = "Gotowe";
            UpdateDiagnostics($"Odtwarzanie zakończone. Limit: {timerBox.Value:0.##} h | Powtórzenia ustawione: {repeatBox.Value:N0}");
        }
    }

    private static async Task DelayPrecisely(double seconds, CancellationToken token)
    {
        if (seconds <= 0) return;

        int milliseconds = (int)Math.Max(0, seconds * 1000 - 2);
        if (milliseconds > 0)
            await Task.Delay(milliseconds, token);

        long target = Stopwatch.GetTimestamp() +
            (long)((seconds - milliseconds / 1000.0) * Stopwatch.Frequency);

        while (Stopwatch.GetTimestamp() < target)
        {
            token.ThrowIfCancellationRequested();
            Thread.SpinWait(100);
        }
    }

    private void SaveMacro()
    {
        List<MacroEvent> snapshot;
        lock (eventsLock) snapshot = events.ToList();
        if (snapshot.Count == 0) return;
        using var dialog = new SaveFileDialog {
            Filter = "TinyTask Safe Macro (*.json)|*.json",
            DefaultExt = "json"
        };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            File.WriteAllText(dialog.FileName,
                JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
            statusLabel.Text = "Makro zapisane";
        }
    }

    private void LoadMacro()
    {
        using var dialog = new OpenFileDialog {
            Filter = "TinyTask Safe Macro (*.json)|*.json"
        };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            var loaded = JsonSerializer.Deserialize<List<MacroEvent>>(
                File.ReadAllText(dialog.FileName));
            lock (eventsLock)
            {
                events.Clear();
                if (loaded != null) events.AddRange(loaded.OrderBy(e => e.Time));
                playButton.Enabled = events.Count > 0;
            }
            statusLabel.Text = $"Wczytano — {events.Count} zdarzeń";
        }
    }
}

public sealed class MacroEvent
{
    public double Time { get; set; }
    public string Type { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public string Button { get; set; } = "";
    public bool Down { get; set; }
    public string Key { get; set; } = "";
    public int ScanCode { get; set; }
    public bool Extended { get; set; }
    public int Delta { get; set; }
}

internal sealed class LowLevelHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_MBUTTONUP = 0x0208;
    private const int WM_MOUSEWHEEL = 0x020A;

    private readonly LowLevelProc keyboardProc;
    private readonly LowLevelProc mouseProc;
    private readonly IntPtr keyboardHook;
    private readonly IntPtr mouseHook;
    private readonly Action<MacroEvent> mouseCallback;
    private readonly Action<MacroEvent> keyboardCallback;
    private readonly object keyStateLock = new();
    private readonly HashSet<int> heldKeys = new();

    public LowLevelHook(Action<MacroEvent> mouseCallback, Action<MacroEvent> keyboardCallback)
    {
        this.mouseCallback = mouseCallback;
        this.keyboardCallback = keyboardCallback;
        keyboardProc = KeyboardHook;
        mouseProc = MouseHook;

        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule!;
        keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, keyboardProc,
            GetModuleHandle(module.ModuleName!), 0);
        mouseHook = SetWindowsHookEx(WH_MOUSE_LL, mouseProc,
            GetModuleHandle(module.ModuleName!), 0);
    }

    private IntPtr KeyboardHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_KEYUP))
        {
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            int vk = (int)data.vkCode;
            int scan = (int)data.scanCode;
            bool extended = (data.flags & 0x01) != 0;
            string key = ((Keys)vk).ToString();
            bool down = wParam == (IntPtr)WM_KEYDOWN;

            lock (keyStateLock)
            {
                if (down)
                {
                    // Windows repeats WM_KEYDOWN while a key is held.
                    // Record only the real press, not the auto-repeat flood.
                    if (!heldKeys.Add(vk))
                        return CallNextHookEx(keyboardHook, nCode, wParam, lParam);
                }
                else
                {
                    heldKeys.Remove(vk);
                }
            }

            keyboardCallback(new MacroEvent {
                Type = down ? "key_down" : "key_up",
                Key = key,
                ScanCode = scan,
                Extended = extended
            });
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private IntPtr MouseHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            int msg = wParam.ToInt32();

            if (msg == WM_MOUSEMOVE)
            {
                mouseCallback(new MacroEvent {
                    Type = "move",
                    X = data.pt.x,
                    Y = data.pt.y
                });
            }
            else if (msg == WM_LBUTTONDOWN || msg == WM_LBUTTONUP ||
                msg == WM_RBUTTONDOWN || msg == WM_RBUTTONUP ||
                msg == WM_MBUTTONDOWN || msg == WM_MBUTTONUP)
            {
                string button = msg is WM_LBUTTONDOWN or WM_LBUTTONUP ? "Left" :
                                msg is WM_RBUTTONDOWN or WM_RBUTTONUP ? "Right" : "Middle";
                mouseCallback(new MacroEvent {
                    Type = "click", X = data.pt.x, Y = data.pt.y,
                    Button = button, Down = msg is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN
                });
            }
            else if (msg == WM_MOUSEWHEEL)
            {
                int delta = (short)((data.mouseData >> 16) & 0xffff);
                mouseCallback(new MacroEvent {
                    Type = "scroll", X = data.pt.x, Y = data.pt.y, Delta = delta
                });
            }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    public void ResetHeldKeys()
    {
        lock (keyStateLock)
            heldKeys.Clear();
    }

    public void Dispose()
    {
        if (keyboardHook != IntPtr.Zero) UnhookWindowsHookEx(keyboardHook);
        if (mouseHook != IntPtr.Zero) UnhookWindowsHookEx(mouseHook);
    }

    private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData, flags, time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn,
        IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
        IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}

internal static class InputSender
{
    private const uint INPUT_MOUSE = 0;
    private const uint INPUT_KEYBOARD = 1;
    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_SCANCODE = 0x0008;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

    public static void SetCursor(int x, int y)
    {
        SetCursorPos(x, y);
    }

    public static void RefreshMouseAt(int x, int y)
    {
        SetCursor(x, y);
        SendRelative(1, 0);
        SendRelative(-1, 0);
        SetCursor(x, y);
    }

    private static void SendRelative(int dx, int dy)
    {
        SendInput(1, new INPUT {
            type = INPUT_MOUSE,
            U = new INPUTUNION {
                mi = new MOUSEINPUT {
                    dx = dx, dy = dy, dwFlags = MOUSEEVENTF_MOVE
                }
            }
        }, Marshal.SizeOf<INPUT>());
    }

    public static void Send(MacroEvent e)
    {
        if (e.Type == "move")
        {
            // Reproduce the recorded cursor path point-by-point.
            SetCursor(e.X, e.Y);
        }
        else if (e.Type == "click")
        {
            uint flags = e.Button switch
            {
                "Left" => e.Down ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP,
                "Right" => e.Down ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP,
                _ => e.Down ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP
            };
            Cursor.Position = new Point(e.X, e.Y);
            SendInput(1, new INPUT {
                type = INPUT_MOUSE,
                U = new INPUTUNION { mi = new MOUSEINPUT { dwFlags = flags } }
            }, Marshal.SizeOf<INPUT>());
        }
        else if (e.Type == "scroll")
        {
            Cursor.Position = new Point(e.X, e.Y);
            SendInput(1, new INPUT {
                type = INPUT_MOUSE,
                U = new INPUTUNION { mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_WHEEL, mouseData = unchecked((uint)e.Delta) } }
            }, Marshal.SizeOf<INPUT>());
        }
        else if (e.Type.StartsWith("key_"))
        {
            ushort scan = (ushort)(e.ScanCode > 0
                ? e.ScanCode
                : (Enum.TryParse<Keys>(e.Key, out var fallback) ? (ushort)fallback : 0));

            if (scan == 0) return;

            uint flags = KEYEVENTF_SCANCODE;
            if (e.Extended) flags |= KEYEVENTF_EXTENDEDKEY;
            if (e.Type == "key_up") flags |= KEYEVENTF_KEYUP;

            SendInput(1, new INPUT {
                type = INPUT_KEYBOARD,
                U = new INPUTUNION {
                    ki = new KEYBDINPUT {
                        wVk = 0,
                        wScan = scan,
                        dwFlags = flags
                    }
                }
            }, Marshal.SizeOf<INPUT>());
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUTUNION U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    private static uint SendInput(uint nInputs, INPUT pInput, int cbSize)
    {
        return SendInput(nInputs, new[] { pInput }, cbSize);
    }
}
