using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using BBEdgeAgent.Models;
using BBEdgeAgent.Services;

namespace BBEdgeAgent.Forms
{
    public class frmTagMapping : Form
    {
        private readonly RfidService     _rfid;
        private readonly SupabaseService _supabase;
        private readonly LocalDbService  _db;
        private readonly AppConfig       _cfg;

        private ListBox  _lstStudents  = null!;
        private TextBox  _txtSearch    = null!;
        private ListView _lvTags       = null!;
        private Label    _lblScanState = null!;
        private Button   _btnScanMode  = null!;
        private Button   _btnRemove    = null!;
        private Label    _lblStatus    = null!;
        private Label    _lblStudent   = null!;

        private List<Student> _students = new();
        private bool _scanModeActive    = false;
        private string? _selectedStudentId;
        private string? _selectedStudentName;

        public frmTagMapping(RfidService rfid, SupabaseService supabase,
                             LocalDbService db, AppConfig cfg)
        {
            _rfid     = rfid;
            _supabase = supabase;
            _db       = db;
            _cfg      = cfg;
            InitializeComponent();
            _ = LoadStudentsAsync();
        }

        private void InitializeComponent()
        {
            UITheme.ApplyToForm(this);
            Text            = L.TagMappingTitle;
            Size            = new Size(1000, 700);
            MinimumSize     = new Size(800, 560);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox     = true;
            StartPosition   = FormStartPosition.CenterParent;

            // ── Title bar ─────────────────────────────────────────
            var titleBar = new Panel { Dock = DockStyle.Top, Height = 54, BackColor = UITheme.BgCard };
            titleBar.Padding = new Padding(16, 0, 16, 0);

            var lblTitle = UITheme.MakeLabel(L.TagMappingTitle, UITheme.FontLarge, UITheme.AccentGold);
            lblTitle.Location = new Point(16, 14);
            titleBar.Controls.Add(lblTitle);
            Controls.Add(titleBar);

            // ── Two-panel layout WITHOUT SplitContainer ───────────
            // Left panel fixed 300px, right fills the rest
            var leftPanel = new Panel
            {
                Dock      = DockStyle.Left,
                Width     = 300,
                BackColor = UITheme.BgCard,
                Padding   = new Padding(12),
            };
            Controls.Add(leftPanel);

            var splitterLine = new Panel { Dock = DockStyle.Left, Width = 4, BackColor = UITheme.Border };
            Controls.Add(splitterLine);

            var rightPanel = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = UITheme.BgDark,
                Padding   = new Padding(16),
            };
            Controls.Add(rightPanel);

            // ── LEFT: Student list ────────────────────────────────
            var lblLeft = UITheme.MakeLabel(L.StudentList, UITheme.FontBodyBold, UITheme.TextSecond);
            lblLeft.Dock = DockStyle.Top; lblLeft.Height = 28;

            _txtSearch = UITheme.MakeTextBox();
            _txtSearch.Dock   = DockStyle.Top;
            _txtSearch.Height = 32;
            _txtSearch.TextChanged += (_, __) => FilterStudents();

            _lstStudents = new ListBox
            {
                Dock        = DockStyle.Fill,
                BackColor   = UITheme.BgCard,
                ForeColor   = UITheme.TextPrimary,
                BorderStyle = BorderStyle.None,
                Font        = UITheme.FontBody,
                DrawMode    = DrawMode.OwnerDrawFixed,
                ItemHeight  = 38,
            };
            _lstStudents.DrawItem            += DrawStudentItem;
            _lstStudents.SelectedIndexChanged += OnStudentSelected;

            var bottomLeft = new Panel { Dock = DockStyle.Bottom, Height = 38, BackColor = UITheme.BgCard };
            var btnRefresh = new GhostBtn { Text = L.RefreshList, Dock = DockStyle.Fill, Font = UITheme.FontSmall };
            btnRefresh.Click += async (_, __) => await LoadStudentsAsync();
            bottomLeft.Controls.Add(btnRefresh);

            leftPanel.Controls.Add(_lstStudents);
            leftPanel.Controls.Add(_txtSearch);
            leftPanel.Controls.Add(lblLeft);
            leftPanel.Controls.Add(bottomLeft);

            // ── RIGHT: Tag details ────────────────────────────────
            var lblRight = UITheme.MakeLabel(L.TagsForStudent, UITheme.FontBodyBold, UITheme.TextSecond);
            lblRight.Dock = DockStyle.Top; lblRight.Height = 28;

            _lblStudent = UITheme.MakeLabel("—", UITheme.FontLarge, UITheme.TextPrimary);
            _lblStudent.Dock = DockStyle.Top; _lblStudent.Height = 36;

            // Tags ListView
            _lvTags = new ListView
            {
                Dock          = DockStyle.Top,
                Height        = 200,
                View          = View.Details,
                FullRowSelect = true,
                GridLines     = false,
                BackColor     = UITheme.BgCard,
                ForeColor     = UITheme.TextPrimary,
                BorderStyle   = BorderStyle.None,
                Font          = UITheme.FontMono,
            };
            _lvTags.Columns.Add(L.TagUid,    300);
            _lvTags.Columns.Add(L.TagNote,   130);
            _lvTags.Columns.Add(L.AssignedAt, 120);

            // Action area
            var actionCard = UITheme.MakeCard(14);
            actionCard.Dock = DockStyle.Fill;

            var lblInstruction = UITheme.MakeLabel(L.ScanToAssign, UITheme.FontSmall, UITheme.TextMuted);
            lblInstruction.Location    = new Point(14, 12);
            lblInstruction.AutoSize    = false;
            lblInstruction.Width       = 560;
            lblInstruction.Height      = 36;

            _lblScanState = UITheme.MakeLabel("", UITheme.FontBody, UITheme.TextMuted);
            _lblScanState.Location = new Point(14, 52);
            _lblScanState.AutoSize = true;

            _btnScanMode = UITheme.MakeButton(L.ScanModeBtn);
            _btnScanMode.Location = new Point(14, 90);
            _btnScanMode.Width    = 180;
            _btnScanMode.Height   = 44;
            _btnScanMode.Click   += ToggleScanMode;

            _btnRemove = new GhostBtn
            {
                Text     = L.RemoveTagBtn,
                Location = new Point(14, 148),
                Width    = 220,
                Font     = UITheme.FontSmall,
            };
            _btnRemove.Click += async (_, __) => await RemoveSelectedTag();

            _lblStatus = UITheme.MakeLabel("", UITheme.FontBody, UITheme.Success);
            _lblStatus.Location = new Point(14, 205);
            _lblStatus.AutoSize = true;
            _lblStatus.MaximumSize = new Size(560, 0);

            var btnClose = new GhostBtn { Text = L.CloseBtn, Width = 110, Font = UITheme.FontSmall };
            btnClose.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.Location = new Point(actionCard.Width - 130, actionCard.Height - 50);
            btnClose.Click   += (_, __) => Close();

            actionCard.Controls.Add(lblInstruction);
            actionCard.Controls.Add(_lblScanState);
            actionCard.Controls.Add(_btnScanMode);
            actionCard.Controls.Add(_btnRemove);
            actionCard.Controls.Add(_lblStatus);
            actionCard.Controls.Add(btnClose);

            rightPanel.Controls.Add(actionCard);
            rightPanel.Controls.Add(_lvTags);
            rightPanel.Controls.Add(_lblStudent);
            rightPanel.Controls.Add(lblRight);

            // Subscribe to RFID events
            _rfid.TagRead += OnTagRead;
            FormClosed    += (_, __) => { _rfid.TagRead -= OnTagRead; ExitScanMode(); };
        }

        // ── Load students ─────────────────────────────────────────

        private async Task LoadStudentsAsync()
        {
            if (InvokeRequired) { Invoke(new Action(async () => await LoadStudentsAsync())); return; }
            _lstStudents.Items.Clear();
            _lstStudents.Items.Add(L.LoadingStudents);
            var online = await _supabase.CheckOnlineAsync();
            if (online)
            {
                var students = await _supabase.GetStudentsAsync();
                _db.CacheStudents(students);
                _students = students;
            }
            else
            {
                _students = _db.GetCachedStudents();
            }
            FilterStudents();
        }

        private void FilterStudents()
        {
            if (InvokeRequired) { Invoke(new Action(FilterStudents)); return; }
            var q = _txtSearch.Text.Trim().ToLower();
            _lstStudents.Items.Clear();
            var filtered = _students.Where(s =>
                string.IsNullOrEmpty(q) ||
                s.FullName.ToLower().Contains(q) ||
                s.BeltName.ToLower().Contains(q)).ToList();
            foreach (var s in filtered)
                _lstStudents.Items.Add(s);
            if (!filtered.Any())
                _lstStudents.Items.Add(_students.Any() ? "No match" : L.NoStudents);
        }

        private void DrawStudentItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using var bg = new SolidBrush(sel ? UITheme.AccentBlue : UITheme.BgCard);
            e.Graphics.FillRectangle(bg, e.Bounds);

            if (_lstStudents.Items[e.Index] is Student stu)
            {
                using var n = new SolidBrush(UITheme.TextPrimary);
                using var b = new SolidBrush(UITheme.AccentGold);
                e.Graphics.DrawString(stu.FullName, UITheme.FontBodyBold, n,
                    new PointF(e.Bounds.X + 10, e.Bounds.Y + 5));
                e.Graphics.DrawString(stu.BeltName, UITheme.FontSmall, b,
                    new PointF(e.Bounds.X + 10, e.Bounds.Y + 21));
            }
            else
            {
                using var br = new SolidBrush(UITheme.TextMuted);
                e.Graphics.DrawString(_lstStudents.Items[e.Index].ToString(), UITheme.FontBody, br,
                    new PointF(e.Bounds.X + 10, e.Bounds.Y + 9));
            }
        }

        private async void OnStudentSelected(object? sender, EventArgs e)
        {
            ExitScanMode();
            _lblStatus.Text = "";
            if (_lstStudents.SelectedItem is Student stu)
            {
                _selectedStudentId   = stu.Id;
                _selectedStudentName = stu.FullName;
                _lblStudent.Text     = stu.FullName;
                await LoadTagsForStudent(stu.Id);
            }
            else
            {
                _lblStudent.Text = "—";
            }
        }

        private async Task LoadTagsForStudent(string studentId)
        {
            _lvTags.Items.Clear();
            var tags = await _supabase.GetTagsForStudentAsync(studentId);
            foreach (var t in tags)
            {
                var item = new ListViewItem(t.tagUid);
                item.SubItems.Add(t.notes);
                item.SubItems.Add(t.assignedAt.ToLocalTime().ToString("yyyy-MM-dd"));
                item.Tag = t.tagId;
                _lvTags.Items.Add(item);
            }
        }

        // ── Scan mode ─────────────────────────────────────────────

        private void ToggleScanMode(object? sender, EventArgs e)
        {
            if (!_scanModeActive) EnterScanMode(); else ExitScanMode();
        }

        private void EnterScanMode()
        {
            if (_selectedStudentId == null) { ShowStatus(L.SelectStudent, UITheme.Warning); return; }
            _scanModeActive        = true;
            _btnScanMode.Text      = L.StopScan;
            _btnScanMode.BackColor = UITheme.Error;
            _lblScanState.Text     = L.ScanWaiting;
            _lblScanState.ForeColor= UITheme.AccentGold;
            _rfid.StartScan();
        }

        private void ExitScanMode()
        {
            if (!_scanModeActive) return;
            _scanModeActive        = false;
            _btnScanMode.Text      = L.ScanModeBtn;
            _btnScanMode.BackColor = UITheme.AccentGold;
            _lblScanState.Text     = "";
            _rfid.StopScan();
        }

        private async void OnTagRead(object? sender, TagReadEventArgs e)
        {
            if (!_scanModeActive || _selectedStudentId == null) return;
            ExitScanMode();
            if (InvokeRequired) { Invoke(new Action(async () => await DoAssign(e.Epc))); return; }
            await DoAssign(e.Epc);
        }

        private async Task DoAssign(string epc)
        {
            ShowStatus($"Assigning {epc}...", UITheme.TextMuted);
            var ok = await _supabase.AssignTagAsync(epc, _selectedStudentId!);
            if (ok)
            {
                ShowStatus($"✓ {L.TagAssignOk}   {epc}", UITheme.Success);
                await LoadTagsForStudent(_selectedStudentId!);
                var allTags = await _supabase.GetRfidTagsAsync();
                _db.CacheRfidTags(allTags);
            }
            else
            {
                ShowStatus($"✗ {L.ConnFail}", UITheme.Error);
            }
        }

        private async Task RemoveSelectedTag()
        {
            if (_lvTags.SelectedItems.Count == 0) { ShowStatus(L.SelectTag, UITheme.Warning); return; }
            var tagId = _lvTags.SelectedItems[0].Tag as string;
            if (tagId == null) return;
            var ok = await _supabase.DeactivateTagAsync(tagId);
            if (ok)
            {
                ShowStatus($"✓ {L.TagRemoveOk}", UITheme.Success);
                if (_selectedStudentId != null) await LoadTagsForStudent(_selectedStudentId);
                var allTags = await _supabase.GetRfidTagsAsync();
                _db.CacheRfidTags(allTags);
            }
            else
            {
                ShowStatus($"✗ {L.ConnFail}", UITheme.Error);
            }
        }

        private void ShowStatus(string msg, Color color)
        {
            if (InvokeRequired) { Invoke(new Action(() => ShowStatus(msg, color))); return; }
            _lblStatus.Text      = msg;
            _lblStatus.ForeColor = color;
        }
    }
}
