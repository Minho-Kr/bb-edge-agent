using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using BBEdgeAgent.Services;

namespace BBEdgeAgent.Forms
{
    public class frmSettings : Form
    {
        private AppConfig        _cfg;
        private RfidService      _rfid;
        private SupabaseService  _supabase;

        // Reader
        private TextBox _txtIp   = null!;
        private TextBox _txtPort = null!;
        private Label   _lblReaderResult = null!;

        // Supabase
        private TextBox   _txtUrl       = null!;
        private TextBox   _txtKey       = null!;
        private TextBox   _txtEmail     = null!;
        private TextBox   _txtPass      = null!;
        private TextBox   _txtBranchId  = null!;
        private TextBox   _txtBranchName= null!;
        private ComboBox  _cboBranch    = null!;
        private Label     _lblSupResult = null!;

        // Attendance
        private NumericUpDown _nudDebounce = null!;
        private CheckBox      _chkAutoStart= null!;

        // Security
        private TextBox _txtNewPin  = null!;
        private TextBox _txtConfPin = null!;
        private Label   _lblPinMsg  = null!;

        public frmSettings(AppConfig cfg, RfidService rfid, SupabaseService supabase)
        {
            _cfg = cfg; _rfid = rfid; _supabase = supabase;
            InitializeComponent();
            LoadValues();
        }

        private void InitializeComponent()
        {
            UITheme.ApplyToForm(this);
            Text            = L.SettingsTitle;
            Size            = new Size(700, 640);
            MinimumSize     = new Size(600, 560);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;

            // Bottom buttons
            var btnBar = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = UITheme.BgCard };
            btnBar.Padding = new Padding(16, 10, 16, 10);

            var btnSave = UITheme.MakeButton(L.SaveSettings);
            btnSave.Width  = 110; btnSave.Height = 36;
            btnSave.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            btnSave.Location = new Point(btnBar.Width - 130, 10);
            btnSave.Click  += Save;

            var btnCancel = new GhostBtn { Text = L.CancelBtn, Width = 100, Height = 36, Font = UITheme.FontBody };
            btnCancel.Anchor   = AnchorStyles.Right | AnchorStyles.Top;
            btnCancel.Location = new Point(btnBar.Width - 250, 10);
            btnCancel.Click   += (_, __) => Close();

            btnBar.Controls.Add(btnSave);
            btnBar.Controls.Add(btnCancel);
            btnBar.Resize += (_, __) =>
            {
                btnSave.Location   = new Point(btnBar.Width - 130, 10);
                btnCancel.Location = new Point(btnBar.Width - 250, 10);
            };
            Controls.Add(btnBar);

            // Tab control
            var tabs = new TabControl
            {
                Dock       = DockStyle.Fill,
                DrawMode   = TabDrawMode.OwnerDrawFixed,
                ItemSize   = new Size(130, 32),
            };
            tabs.DrawItem += DrawTabItem;
            tabs.TabPages.Add(BuildReaderTab());
            tabs.TabPages.Add(BuildSupabaseTab());
            tabs.TabPages.Add(BuildAttendanceTab());
            tabs.TabPages.Add(BuildSecurityTab());
            Controls.Add(tabs);
        }

        // ── Tab: Reader ───────────────────────────────────────────

        private TabPage BuildReaderTab()
        {
            var page = PageOf(L.TabReader);
            var tbl  = FormTable(page, 28);

            _txtIp   = FillBox(); _txtPort = FillBox(); _txtPort.Width = 80;
            _lblReaderResult = MsgLabel();

            var btnTest = new GhostBtn { Text = L.TestConn, Width = 140, Height = 34, Font = UITheme.FontSmall };
            btnTest.Click += async (_, __) => await TestReader();

            Row(tbl, L.ReaderIpLabel,  _txtIp);
            Row(tbl, L.ReaderPortLabel, _txtPort);
            Row(tbl, "",               btnTest);
            Row(tbl, "",               _lblReaderResult);
            return page;
        }

        // ── Tab: Supabase ─────────────────────────────────────────

        private TabPage BuildSupabaseTab()
        {
            var page = PageOf(L.TabSupabase);
            var tbl  = FormTable(page, 28);

            _txtUrl        = FillBox();
            _txtKey        = FillBox();
            _txtEmail      = FillBox(); _txtEmail.Width = 280;
            _txtPass       = FillBox(password: true); _txtPass.Width = 280;
            _txtBranchId   = FillBox(); _txtBranchId.Width = 300;
            _txtBranchName = FillBox(); _txtBranchName.Width = 280;
            _lblSupResult  = MsgLabel();

            // Branch dropdown (populated after Test connection)
            _cboBranch = new ComboBox
            {
                Width        = 420,
                Height       = 28,
                DropDownStyle= ComboBoxStyle.DropDownList,
                BackColor    = UITheme.BgCard,
                ForeColor    = UITheme.TextPrimary,
                Font         = UITheme.FontBody,
            };
            _cboBranch.SelectedIndexChanged += (_, __) =>
            {
                if (_cboBranch.SelectedItem is BranchItem b)
                {
                    _txtBranchId.Text   = b.Id;
                    _txtBranchName.Text = b.Name;
                }
            };

            var btnRow = new FlowLayoutPanel
            {
                AutoSize      = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor     = Color.Transparent,
            };
            var btnTest = new GhostBtn { Text = L.TestSupabase, Width = 150, Height = 34, Font = UITheme.FontSmall };
            btnTest.Click += async (_, __) => await TestSupabase();
            var btnLoad = new GhostBtn { Text = "Load Branches", Width = 140, Height = 34, Font = UITheme.FontSmall };
            btnLoad.Click += async (_, __) => await LoadBranches();
            btnRow.Controls.Add(btnTest);
            btnRow.Controls.Add(btnLoad);

            Row(tbl, L.SupabaseUrlLabel, _txtUrl);
            Row(tbl, L.AnonKeyLabel,     _txtKey);
            Row(tbl, L.EdgeEmailLabel,   _txtEmail);
            Row(tbl, L.EdgePassLabel,    _txtPass);
            Row(tbl, "",                 btnRow);
            Row(tbl, "",                 _lblSupResult);
            Row(tbl, "Branch",           _cboBranch);
            Row(tbl, L.BranchIdLabel,    _txtBranchId);
            Row(tbl, L.BranchNameLabel,  _txtBranchName);

            var note = UITheme.MakeLabel(
                "ℹ  Use the Supabase service role key to bypass RLS on this edge device.",
                UITheme.FontSmall, UITheme.TextMuted);
            note.MaximumSize = new Size(600, 0);
            note.AutoSize    = true;
            note.Dock        = DockStyle.Bottom;
            page.Controls.Add(note);
            return page;
        }

        // ── Tab: Attendance ───────────────────────────────────────

        private TabPage BuildAttendanceTab()
        {
            var page = PageOf(L.TabAttendance);
            var tbl  = FormTable(page, 28);

            _nudDebounce = new NumericUpDown
            {
                Minimum = 1, Maximum = 30, Value = 3, Width = 80, Height = 30,
                BackColor = UITheme.BgCard, ForeColor = UITheme.TextPrimary, Font = UITheme.FontBody,
            };
            _chkAutoStart = new CheckBox
            {
                Text = L.AutoStartLabel, ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent, Font = UITheme.FontBody, AutoSize = true,
            };

            Row(tbl, L.DebounceLabel, _nudDebounce);
            Row(tbl, "",              _chkAutoStart);

            var hint = UITheme.MakeLabel(
                "Debounce prevents the same tag from creating duplicate attendance entries within the time window.",
                UITheme.FontSmall, UITheme.TextMuted);
            hint.MaximumSize = new Size(580, 0);
            hint.AutoSize    = true;
            hint.Dock        = DockStyle.Bottom;
            page.Controls.Add(hint);
            return page;
        }

        // ── Tab: Security ─────────────────────────────────────────

        private TabPage BuildSecurityTab()
        {
            var page = PageOf(L.TabSecurity);
            var tbl  = FormTable(page, 28);

            _txtNewPin  = FillBox(password: true); _txtNewPin.Width = 120; _txtNewPin.MaxLength = 4;
            _txtConfPin = FillBox(password: true); _txtConfPin.Width = 120; _txtConfPin.MaxLength = 4;
            _lblPinMsg  = MsgLabel();

            var btnChange = new GhostBtn { Text = L.ChangePin, Width = 140, Height = 34, Font = UITheme.FontSmall };
            btnChange.Click += ChangePin;

            Row(tbl, L.NewPinLabel,     _txtNewPin);
            Row(tbl, L.ConfirmPinLabel, _txtConfPin);
            Row(tbl, "",                btnChange);
            Row(tbl, "",                _lblPinMsg);
            return page;
        }

        // ── Load / Save ───────────────────────────────────────────

        private void LoadValues()
        {
            _txtIp.Text          = _cfg.ReaderIp;
            _txtPort.Text        = _cfg.ReaderPort.ToString();
            _txtUrl.Text         = _cfg.SupabaseUrl;
            _txtKey.Text         = _cfg.SupabaseKey;
            _txtEmail.Text       = _cfg.EdgeEmail;
            _txtPass.Text        = _cfg.EdgePassword;
            _txtBranchId.Text    = _cfg.BranchId;
            _txtBranchName.Text  = _cfg.BranchName;
            _nudDebounce.Value   = Math.Max(1, Math.Min(30, _cfg.DebounceMinutes));
            _chkAutoStart.Checked= _cfg.AutoStartScanning;
        }

        private void Save(object? sender, EventArgs e)
        {
            _cfg.ReaderIp         = _txtIp.Text.Trim();
            _cfg.ReaderPort       = int.TryParse(_txtPort.Text, out var p) ? p : 49152;
            _cfg.SupabaseUrl      = _txtUrl.Text.TrimEnd('/');
            _cfg.SupabaseKey      = _txtKey.Text.Trim();
            _cfg.EdgeEmail        = _txtEmail.Text.Trim();
            _cfg.EdgePassword     = _txtPass.Text;
            _cfg.BranchId         = _txtBranchId.Text.Trim();
            _cfg.BranchName       = _txtBranchName.Text.Trim();
            _cfg.DebounceMinutes  = (int)_nudDebounce.Value;
            _cfg.AutoStartScanning= _chkAutoStart.Checked;
            _cfg.Save();
            _rfid.UpdateConfig(_cfg);
            _supabase.UpdateConfig(_cfg);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void ChangePin(object? sender, EventArgs e)
        {
            if (_txtNewPin.Text.Length != 4) { ShowPinMsg("PIN must be exactly 4 digits.", UITheme.Error); return; }
            if (_txtNewPin.Text != _txtConfPin.Text) { ShowPinMsg(L.PinMismatch, UITheme.Error); return; }
            _cfg.SetPin(_txtNewPin.Text);
            _cfg.Save();
            ShowPinMsg(L.PinChanged, UITheme.Success);
            _txtNewPin.Clear(); _txtConfPin.Clear();
        }

        private void ShowPinMsg(string msg, Color c) { _lblPinMsg.Text = msg; _lblPinMsg.ForeColor = c; }

        // ── Connection tests ──────────────────────────────────────

        private async Task TestReader()
        {
            _lblReaderResult.ForeColor = UITheme.TextMuted;
            _lblReaderResult.Text      = L.Connecting;
            // Use current textbox values (not yet saved)
            _rfid.UpdateConfig(new AppConfig
            {
                ReaderIp   = _txtIp.Text.Trim(),
                ReaderPort = int.TryParse(_txtPort.Text, out var p) ? p : 49152,
            });
            _rfid.Connect();
            await Task.Delay(2500);
            bool ok = _rfid.IsConnected;
            _lblReaderResult.ForeColor = ok ? UITheme.Success : UITheme.Error;
            _lblReaderResult.Text      = ok ? L.ConnOk : L.ConnFail;
        }

        private async Task TestSupabase()
        {
            _lblSupResult.ForeColor = UITheme.TextMuted;
            _lblSupResult.Text      = L.Connecting;
            _supabase.UpdateConfig(new AppConfig
            {
                SupabaseUrl  = _txtUrl.Text.TrimEnd('/'),
                SupabaseKey  = _txtKey.Text.Trim(),
                EdgeEmail    = _txtEmail.Text.Trim(),
                EdgePassword = _txtPass.Text,
                BranchId     = _txtBranchId.Text.Trim(),
            });
            var ok = await _supabase.SignInAsync();
            _lblSupResult.ForeColor = ok ? UITheme.Success : UITheme.Error;
            _lblSupResult.Text      = ok ? "✓  " + L.ConnOk : "✗  " + L.ConnFail;
        }

        private async Task LoadBranches()
        {
            _lblSupResult.ForeColor = UITheme.TextMuted;
            _lblSupResult.Text      = "Loading branches...";
            _supabase.UpdateConfig(new AppConfig
            {
                SupabaseUrl  = _txtUrl.Text.TrimEnd('/'),
                SupabaseKey  = _txtKey.Text.Trim(),
                EdgeEmail    = _txtEmail.Text.Trim(),
                EdgePassword = _txtPass.Text,
                BranchId     = "00000000-0000-0000-0000-000000000000",
            });
            await _supabase.SignInAsync();
            var branches = await _supabase.GetBranchesAsync();
            _cboBranch.Items.Clear();
            foreach (var (id, name) in branches)
                _cboBranch.Items.Add(new BranchItem(id, name));
            if (branches.Count > 0)
            {
                _cboBranch.SelectedIndex = 0;
                _lblSupResult.ForeColor  = UITheme.Success;
                _lblSupResult.Text       = $"✓  {branches.Count} branch(es) found";
            }
            else
            {
                _lblSupResult.ForeColor = UITheme.Warning;
                _lblSupResult.Text      = "No branches found — check URL and Key.";
            }
        }

        // ── Helpers ──────────────────────────────────────────────

        private static TabPage PageOf(string title)
        {
            return new TabPage(title) { BackColor = UITheme.BgDark, Padding = new Padding(20) };
        }

        private static TableLayoutPanel FormTable(TabPage page, int rowH)
        {
            var tbl = new TableLayoutPanel
            {
                AutoSize        = true,
                ColumnCount     = 2,
                BackColor       = Color.Transparent,
                Location        = new Point(20, 16),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            page.Controls.Add(tbl);
            return tbl;
        }

        private static void Row(TableLayoutPanel tbl, string label, Control ctrl)
        {
            int row = tbl.RowCount;
            tbl.RowCount = row + 1;
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            if (!string.IsNullOrEmpty(label))
            {
                var lbl = UITheme.MakeLabel(label, UITheme.FontSmall, UITheme.TextSecond);
                lbl.Anchor = AnchorStyles.Left | AnchorStyles.Top;
                lbl.Margin = new Padding(0, 14, 0, 0);
                tbl.Controls.Add(lbl, 0, row);
            }
            ctrl.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            ctrl.Margin = new Padding(0, 8, 0, 0);
            tbl.Controls.Add(ctrl, 1, row);
        }

        private TextBox FillBox(bool password = false)
        {
            var tb = UITheme.MakeTextBox(password);
            tb.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            tb.Width  = 420;
            tb.Height = 28;
            return tb;
        }

        private Label MsgLabel()
        {
            return new Label
            {
                Text      = "",
                ForeColor = UITheme.TextMuted,
                BackColor = Color.Transparent,
                Font      = UITheme.FontSmall,
                AutoSize  = true,
                Margin    = new Padding(0, 10, 0, 0),
            };
        }

        private class BranchItem
        {
            public string Id   { get; }
            public string Name { get; }
            public BranchItem(string id, string name) { Id = id; Name = name; }
            public override string ToString() => Name.Length > 0 ? $"{Name}  ({Id.Substring(0, Math.Min(8, Id.Length))}…)" : Id;
        }

        private void DrawTabItem(object? sender, DrawItemEventArgs e)
        {
            var tab    = (TabControl)sender!;
            var page   = tab.TabPages[e.Index];
            bool sel   = (e.State & DrawItemState.Selected) != 0;
            using var bg = new SolidBrush(sel ? UITheme.BgCard : UITheme.BgDark);
            e.Graphics.FillRectangle(bg, e.Bounds);
            using var fg = new SolidBrush(sel ? UITheme.AccentGold : UITheme.TextSecond);
            var sf = new System.Drawing.StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            e.Graphics.DrawString(page.Text, UITheme.FontSmall, fg, e.Bounds, sf);
            if (sel)
            {
                using var pen = new Pen(UITheme.AccentGold, 2);
                e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }
        }
    }
}
