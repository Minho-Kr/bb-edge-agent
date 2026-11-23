using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using BBEdgeAgent.Models;
using BBEdgeAgent.Services;

namespace BBEdgeAgent.Forms
{
    public class frmMain : Form
    {
        private readonly AppConfig         _cfg;
        private readonly RfidService       _rfid;
        private readonly SupabaseService   _supabase;
        private readonly LocalDbService    _db;
        private readonly AttendanceService _attendance;

        // Status bar
        private Label _lblReaderStatus = null!;
        private Label _lblCloudStatus  = null!;
        private Label _lblTodayCount   = null!;
        private Label _lblQueueCount   = null!;
        private Label _lblTime         = null!;
        private Label _lblBranchName   = null!;
        private Button _btnLang        = null!;

        // Scan result panel
        private Panel  _pnlScan        = null!;
        private Label  _lblScanBig     = null!;  // "Please scan your RFID tag"
        private Label  _lblName        = null!;  // student name
        private Label  _lblBeltResult  = null!;  // belt level
        private Label  _lblResultSub   = null!;  // time / "already" message

        // Recent log
        private ListView _lvRecent     = null!;

        // Sidebar buttons
        private Button _btnScan        = null!;

        // State
        private bool    _scanning      = false;
        private int     _todayCount    = 0;
        private Color   _scanBg        = Color.FromArgb(30, 42, 62);
        private System.Windows.Forms.Timer _uiTimer           = null!;
        private System.Windows.Forms.Timer _syncTimer         = null!;
        private System.Windows.Forms.Timer _onlineTimer       = null!;
        private System.Windows.Forms.Timer _resetScanTimer    = null!;

        // Fonts for big scan display
        private static readonly Font FontScanPrompt  = new Font("Segoe UI", 28f, FontStyle.Bold);
        private static readonly Font FontScanName    = new Font("Segoe UI", 42f, FontStyle.Bold);
        private static readonly Font FontScanBelt    = new Font("Segoe UI", 18f);
        private static readonly Font FontScanSub     = new Font("Segoe UI", 13f);

        public frmMain(AppConfig cfg, RfidService rfid, SupabaseService supabase,
                       LocalDbService db, AttendanceService attendance)
        {
            _cfg        = cfg;
            _rfid       = rfid;
            _supabase   = supabase;
            _db         = db;
            _attendance = attendance;
            InitializeComponent();
            _ = InitAsync();
        }

        private void InitializeComponent()
        {
            UITheme.ApplyToForm(this);
            Text            = $"{L.AppTitle} — {_cfg.BranchName}";
            Size            = new Size(1280, 800);
            MinimumSize     = new Size(1024, 680);
            StartPosition   = FormStartPosition.CenterScreen;
            WindowState     = FormWindowState.Maximized;

            // ── TOP BAR ──────────────────────────────────────────
            var topBar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 58,
                BackColor = UITheme.BgCard,
            };

            var logo = new PictureBox
            {
                Size     = new Size(36, 36),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor= Color.Transparent,
                Location = new Point(14, 11),
            };
            TryLoadLogo(logo);

            _lblBranchName = UITheme.MakeLabel(_cfg.BranchName, UITheme.FontBodyBold, UITheme.AccentGold);
            _lblBranchName.Location = new Point(60, 19);

            // Right-side status indicators (laid out in Resize)
            _lblReaderStatus = MakeStatusChip("● Reader", UITheme.Error);
            _lblCloudStatus  = MakeStatusChip("● Cloud",  UITheme.Error);
            _lblTodayCount   = MakeStatusChip($"{L.TodayLabel}: 0", UITheme.TextSecond);
            _lblQueueCount   = MakeStatusChip($"{L.QueuedLabel}: 0", UITheme.TextMuted);
            _lblTime         = MakeStatusChip(DateTime.Now.ToString("HH:mm"), UITheme.TextPrimary);

            _btnLang = new GhostBtn
            {
                Text   = L.Current == "en" ? "한국어" : "English",
                Width  = 82, Height = 30, Font = UITheme.FontSmall,
            };
            _btnLang.Click += ToggleLang;

            topBar.Controls.Add(logo);
            topBar.Controls.Add(_lblBranchName);
            topBar.Controls.Add(_lblReaderStatus);
            topBar.Controls.Add(_lblCloudStatus);
            topBar.Controls.Add(_lblTodayCount);
            topBar.Controls.Add(_lblQueueCount);
            topBar.Controls.Add(_lblTime);
            topBar.Controls.Add(_btnLang);

            topBar.Resize += (_, __) => PositionTopBarItems(topBar);
            Controls.Add(topBar);

            // ── SIDEBAR (right) ───────────────────────────────────
            var sidebar = new Panel
            {
                Dock      = DockStyle.Right,
                Width     = 200,
                BackColor = UITheme.BgCard,
                Padding   = new Padding(12, 16, 12, 12),
            };

            _btnScan = UITheme.MakeButton(L.StartScan);
            _btnScan.Dock   = DockStyle.Top;
            _btnScan.Height = 54;
            _btnScan.Font   = UITheme.FontBodyBold;
            _btnScan.Margin = new Padding(0, 0, 0, 6);
            _btnScan.Click += ToggleScan;

            void AddSideBtn(string text, EventHandler onClick)
            {
                var b = new GhostBtn { Text = text, Dock = DockStyle.Top, Height = 46, Font = UITheme.FontBody };
                b.Margin = new Padding(0, 0, 0, 4);
                b.Click += onClick;
                sidebar.Controls.Add(b);
            }

            var btnLogout = new GhostBtn { Text = L.Logout, Dock = DockStyle.Bottom, Height = 44, Font = UITheme.FontBody, ForeColor = UITheme.Error };
            btnLogout.FlatAppearance.BorderColor = UITheme.Error;
            btnLogout.Click += (_, __) => { _rfid.Disconnect(); Close(); };
            sidebar.Controls.Add(btnLogout);

            // Add in reverse (DockStyle.Top adds visually bottom-up)
            AddSideBtn(L.SettingsMenu,  ShowSettings);
            AddSideBtn(L.SyncNow,       async (_, __) => await SyncNow());
            AddSideBtn(L.TagMapping,    ShowTagMapping);
            AddSideBtn(L.ManualCheckIn, ShowManualCheckIn);
            sidebar.Controls.Add(_btnScan);
            Controls.Add(sidebar);

            // ── CONTENT ───────────────────────────────────────────
            var content = new Panel
            {
                Dock        = DockStyle.Fill,
                BackColor   = UITheme.BgDark,
                Padding     = new Padding(0),
            };

            // SCAN PANEL (big central display)
            _pnlScan = new DoubleBufferPanel
            {
                Dock      = DockStyle.Top,
                Height    = 420,
                BackColor = Color.FromArgb(30, 42, 62),
                Padding   = new Padding(40),
            };
            _pnlScan.Paint += (_, e) => e.Graphics.Clear(_scanBg);

            _lblScanBig = new Label
            {
                Text      = L.ScanPrompt,
                Font      = FontScanPrompt,
                ForeColor = UITheme.TextSecond,
                BackColor = Color.Transparent,
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock      = DockStyle.Fill,
            };

            _lblName = new Label
            {
                Text      = "",
                Font      = FontScanName,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                AutoSize  = false,
                TextAlign = ContentAlignment.BottomCenter,
                Visible   = false,
            };

            _lblBeltResult = new Label
            {
                Text      = "",
                Font      = FontScanBelt,
                ForeColor = UITheme.AccentGold,
                BackColor = Color.Transparent,
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Visible   = false,
            };

            _lblResultSub = new Label
            {
                Text      = L.ScanPromptSub,
                Font      = FontScanSub,
                ForeColor = UITheme.TextMuted,
                BackColor = Color.Transparent,
                AutoSize  = false,
                TextAlign = ContentAlignment.TopCenter,
            };

            _pnlScan.Controls.Add(_lblScanBig);
            _pnlScan.Controls.Add(_lblName);
            _pnlScan.Controls.Add(_lblBeltResult);
            _pnlScan.Controls.Add(_lblResultSub);

            _pnlScan.Resize += (_, __) => LayoutScanPanel();
            content.Resize  += (_, __) => _pnlScan.Height = Math.Max(320, content.Height * 54 / 100);

            // RECENT LOG
            var recentHdr = UITheme.MakeLabel(L.RecentTitle, UITheme.FontBodyBold, UITheme.TextSecond);
            recentHdr.Dock = DockStyle.Top; recentHdr.Height = 32; recentHdr.Padding = new Padding(12, 8, 0, 0);

            _lvRecent = new ListView
            {
                Dock          = DockStyle.Fill,
                View          = View.Details,
                FullRowSelect = true,
                GridLines     = false,
                BackColor     = UITheme.BgCard,
                ForeColor     = UITheme.TextPrimary,
                BorderStyle   = BorderStyle.None,
                Font          = UITheme.FontBody,
                HeaderStyle   = ColumnHeaderStyle.Nonclickable,
            };
            _lvRecent.Columns.Add("Time",    100);
            _lvRecent.Columns.Add("Name",    220);
            _lvRecent.Columns.Add("Belt",    160);
            _lvRecent.Columns.Add("Source",   90);

            content.Controls.Add(_lvRecent);
            content.Controls.Add(recentHdr);
            content.Controls.Add(_pnlScan);
            Controls.Add(content);

            // ── TIMERS ──────────────────────────────────────────
            _uiTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _uiTimer.Tick += (_, __) => RefreshUiTick();
            _uiTimer.Start();

            _syncTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
            _syncTimer.Tick += async (_, __) => await SyncNow();
            _syncTimer.Start();

            _onlineTimer = new System.Windows.Forms.Timer { Interval = 15_000 };
            _onlineTimer.Tick += async (_, __) => await CheckOnline();
            _onlineTimer.Start();

            _resetScanTimer = new System.Windows.Forms.Timer { Interval = 4000 };
            _resetScanTimer.Tick += (_, __) => { _resetScanTimer.Stop(); ResetScanPanel(); };

            _rfid.TagRead       += OnTagRead;
            _rfid.StatusChanged += OnReaderStatus;
            Load               += (_, __) => LayoutScanPanel();
            FormClosed         += (_, __) =>
            {
                _rfid.TagRead       -= OnTagRead;
                _rfid.StatusChanged -= OnReaderStatus;
                _uiTimer.Stop(); _syncTimer.Stop(); _onlineTimer.Stop();
            };
        }

        // ── Initialization ────────────────────────────────────────

        private async Task InitAsync()
        {
            var online = await _supabase.SignInAsync();
            UpdateCloudStatus();
            if (online)
            {
                var students = await _supabase.GetStudentsAsync();
                _db.CacheStudents(students);
                var tags = await _supabase.GetRfidTagsAsync();
                _db.CacheRfidTags(tags);
            }
            if (_cfg.IsReaderConfigured) _rfid.Connect();
            if (_cfg.AutoStartScanning) { await Task.Delay(2000); StartScan(); }
        }

        // ── RFID ─────────────────────────────────────────────────

        private void StartScan()
        {
            if (!_rfid.IsConnected) _rfid.Connect();
            _scanning          = true;
            _btnScan.Text      = L.StopScan;
            _btnScan.BackColor = UITheme.Error;
            _rfid.StartScan();
        }

        private void StopScan()
        {
            _scanning          = false;
            _btnScan.Text      = L.StartScan;
            _btnScan.BackColor = UITheme.AccentGold;
            _rfid.StopScan();
        }

        private void ToggleScan(object? sender, EventArgs e)
        {
            if (_scanning) StopScan(); else StartScan();
        }

        private async void OnTagRead(object? sender, TagReadEventArgs e)
        {
            if (InvokeRequired) { Invoke(new Action(async () => await HandleTag(e))); return; }
            await HandleTag(e);
        }

        private async Task HandleTag(TagReadEventArgs e)
        {
            var result = await _attendance.ProcessTagAsync(e.Epc);
            ShowScanResult(result);
        }

        private void OnReaderStatus(object? sender, string status)
        {
            if (InvokeRequired) { Invoke(new Action(() => OnReaderStatus(sender, status))); return; }
            bool ok = status.StartsWith("connected");
            _lblReaderStatus.Text      = ok ? $"● {L.StatusReader}" : $"● {L.StatusReader}";
            _lblReaderStatus.ForeColor = ok ? UITheme.Success : UITheme.Error;
            if (!ok) { _scanning = false; _btnScan.Text = L.StartScan; _btnScan.BackColor = UITheme.AccentGold; }
        }

        // ── Scan display ──────────────────────────────────────────

        private void ShowScanResult(CheckInOutcome result)
        {
            _resetScanTimer.Stop();

            switch (result.Result)
            {
                case CheckInResult.Success:
                    _scanBg = Color.FromArgb(10, 72, 50);
                    SetScanDisplay(
                        title:    "✓  " + L.CheckedIn,
                        titleClr: UITheme.Success,
                        name:     result.StudentName ?? "",
                        belt:     result.BeltName    ?? "",
                        sub:      DateTime.Now.ToString("HH:mm:ss"));
                    _todayCount++;
                    if (result.Record != null) AddRecentLog(result.Record);
                    break;

                case CheckInResult.AlreadyCheckedIn:
                    _scanBg = Color.FromArgb(60, 50, 10);
                    SetScanDisplay(
                        title:    L.AlreadyCheckedIn,
                        titleClr: UITheme.Warning,
                        name:     result.StudentName ?? "",
                        belt:     result.BeltName    ?? "",
                        sub:      $"{result.MinutesAgo} {L.AlreadyTime}");
                    break;

                case CheckInResult.TagNotFound:
                    _scanBg = Color.FromArgb(70, 15, 15);
                    SetScanDisplay(
                        title:    L.TagNotFound,
                        titleClr: UITheme.Error,
                        name:     "", belt: "", sub: "");
                    break;
            }
            _pnlScan.Invalidate();
            LayoutScanPanel();
            _resetScanTimer.Start();
        }

        private void SetScanDisplay(string title, Color titleClr, string name, string belt, string sub)
        {
            _lblScanBig.Text     = title;
            _lblScanBig.ForeColor= titleClr;
            _lblName.Text        = name;
            _lblBeltResult.Text  = belt;
            _lblResultSub.Text   = sub;
        }

        private void ResetScanPanel()
        {
            if (InvokeRequired) { Invoke(new Action(ResetScanPanel)); return; }
            _scanBg = Color.FromArgb(30, 42, 62);
            SetScanDisplay(L.ScanPrompt, UITheme.TextSecond, "", "", L.ScanPromptSub);
            _lblScanBig.Font = FontScanPrompt;
            _pnlScan.Invalidate();
            LayoutScanPanel();
        }

        private void LayoutScanPanel()
        {
            if (_pnlScan == null) return;
            int w  = _pnlScan.ClientSize.Width;
            int h  = _pnlScan.ClientSize.Height;
            bool hasName = !string.IsNullOrEmpty(_lblName.Text);

            if (hasName)
            {
                // Title (success/already/error) at top third
                _lblScanBig.Font   = new Font("Segoe UI", 18f, FontStyle.Bold);
                _lblScanBig.Bounds = new Rectangle(0, h / 2 - 120, w, 36);

                _lblName.Visible   = true;
                _lblName.Bounds    = new Rectangle(0, h / 2 - 80, w, 80);

                _lblBeltResult.Visible = !string.IsNullOrEmpty(_lblBeltResult.Text);
                _lblBeltResult.Bounds  = new Rectangle(0, h / 2 + 10, w, 36);

                _lblResultSub.Bounds   = new Rectangle(0, h / 2 + 60, w, 36);
            }
            else
            {
                _lblScanBig.Font   = FontScanPrompt;
                _lblScanBig.Bounds = new Rectangle(0, h / 2 - 60, w, 68);
                _lblName.Visible   = false;
                _lblBeltResult.Visible = false;
                _lblResultSub.Bounds   = new Rectangle(0, h / 2 + 20, w, 36);
            }
        }

        // ── Recent log ────────────────────────────────────────────

        private void AddRecentLog(AttendanceRecord rec)
        {
            var item = new ListViewItem(rec.AttendedAt.ToLocalTime().ToString("HH:mm:ss"));
            item.SubItems.Add(rec.StudentName);
            item.SubItems.Add(rec.BeltName);
            item.SubItems.Add(rec.Source == "auto_rfid" ? "RFID" : "Manual");
            _lvRecent.Items.Insert(0, item);
            if (_lvRecent.Items.Count > 30) _lvRecent.Items.RemoveAt(_lvRecent.Items.Count - 1);
        }

        // ListView uses default rendering (no OwnerDraw) to avoid white-bar artifacts

        // ── Sync / Online ─────────────────────────────────────────

        private async Task SyncNow()
        {
            if (!_supabase.IsOnline) return;
            await _attendance.SyncQueueAsync();
            if (InvokeRequired) Invoke(new Action(UpdateQueueCount)); else UpdateQueueCount();
        }

        private async Task CheckOnline()
        {
            await _supabase.CheckOnlineAsync();
            if (InvokeRequired) Invoke(new Action(UpdateCloudStatus)); else UpdateCloudStatus();
        }

        // ── UI refresh ────────────────────────────────────────────

        private void RefreshUiTick()
        {
            _lblTime.Text = DateTime.Now.ToString("HH:mm");
            UpdateCloudStatus();
            UpdateQueueCount();
        }

        private void UpdateCloudStatus()
        {
            _lblCloudStatus.Text      = $"● {L.StatusSupabase}";
            _lblCloudStatus.ForeColor = _supabase.IsOnline ? UITheme.Success : UITheme.Error;
        }

        private void UpdateQueueCount()
        {
            int q = _db.GetUnsyncedCount();
            _lblQueueCount.Text      = $"{L.QueuedLabel}: {q}";
            _lblQueueCount.ForeColor = q > 0 ? UITheme.Warning : UITheme.TextMuted;
            _lblTodayCount.Text      = $"{L.TodayLabel}: {_todayCount}";
        }

        // ── Position top bar items ─────────────────────────────────

        private void PositionTopBarItems(Panel bar)
        {
            int right = bar.Width - 12;
            _btnLang.Location         = new Point(right - (_btnLang.Width + 6), 14);
            right -= (_btnLang.Width + 14);
            _lblTime.Location         = new Point(right - 60, 19); right -= 74;
            _lblQueueCount.Location   = new Point(right - 90, 19); right -= 104;
            _lblTodayCount.Location   = new Point(right - 80, 19); right -= 94;
            _lblCloudStatus.Location  = new Point(right - 70, 19); right -= 84;
            _lblReaderStatus.Location = new Point(right - 70, 19);
        }

        // ── Sub-dialogs ───────────────────────────────────────────

        private void ShowSettings(object? sender, EventArgs e)
        {
            bool was = _scanning; StopScan();
            using var dlg = new frmSettings(_cfg, _rfid, _supabase);
            dlg.ShowDialog(this);
            Text = $"{L.AppTitle} — {_cfg.BranchName}";
            _lblBranchName.Text = _cfg.BranchName;
            if (was) StartScan();
        }

        private void ShowTagMapping(object? sender, EventArgs e)
        {
            bool was = _scanning; StopScan();
            using var dlg = new frmTagMapping(_rfid, _supabase, _db, _cfg);
            dlg.ShowDialog(this);
            if (was) StartScan();
        }

        private void ShowManualCheckIn(object? sender, EventArgs e)
        {
            using var dlg = new frmManualCheckIn(_db, _attendance);
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Result != null)
                ShowScanResult(dlg.Result);
        }

        private void ToggleLang(object? sender, EventArgs e)
        {
            L.Current     = L.Current == "en" ? "ko" : "en";
            _cfg.Language = L.Current;
            _cfg.Save();
            _rfid.StopScan();
            Application.Restart();
        }

        // ── Helpers ──────────────────────────────────────────────

        private static Label MakeStatusChip(string text, Color color)
        {
            return new Label
            {
                Text      = text,
                ForeColor = color,
                BackColor = Color.Transparent,
                Font      = UITheme.FontSmall,
                AutoSize  = true,
            };
        }

        private static void TryLoadLogo(PictureBox pb)
        {
            var candidates = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bb.png"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "bb.png"),
            };
            foreach (var p in candidates)
                if (File.Exists(p)) { pb.Image = Image.FromFile(p); return; }
        }
    }

    /// <summary>Panel with DoubleBuffered = true to prevent flicker.</summary>
    internal class DoubleBufferPanel : Panel
    {
        public DoubleBufferPanel() { DoubleBuffered = true; }
    }
}
