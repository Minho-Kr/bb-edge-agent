using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using BBEdgeAgent.Models;
using BBEdgeAgent.Services;

namespace BBEdgeAgent.Forms
{
    public class frmManualCheckIn : Form
    {
        private readonly LocalDbService    _db;
        private readonly AttendanceService _attendance;

        private TextBox  _txtSearch   = null!;
        private ListBox  _lstStudents = null!;
        private Button   _btnConfirm  = null!;
        private Label    _lblStatus   = null!;

        private List<Student> _students = new();
        public CheckInOutcome? Result { get; private set; }

        public frmManualCheckIn(LocalDbService db, AttendanceService attendance)
        {
            _db         = db;
            _attendance = attendance;
            InitializeComponent();
            LoadStudents();
        }

        private void InitializeComponent()
        {
            UITheme.ApplyToForm(this);
            Text            = L.ManualTitle;
            Size            = new Size(560, 620);
            MinimumSize     = new Size(480, 520);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;

            var title = UITheme.MakeLabel(L.ManualTitle, UITheme.FontLarge, UITheme.AccentGold);
            title.Location = new Point(20, 20);

            var lblSub = UITheme.MakeLabel(L.SelectAndCheckIn, UITheme.FontSmall, UITheme.TextMuted);
            lblSub.Location = new Point(20, 54);

            _txtSearch = UITheme.MakeTextBox();
            _txtSearch.Location = new Point(20, 80);
            _txtSearch.Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _txtSearch.Width    = ClientSize.Width - 40;
            _txtSearch.Text = "";
            _txtSearch.TextChanged += (_, __) => FilterStudents();

            _lstStudents = new ListBox
            {
                Location    = new Point(20, 118),
                Size        = new Size(ClientSize.Width - 40, 300),
                Anchor      = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                BackColor   = UITheme.BgCard,
                ForeColor   = UITheme.TextPrimary,
                BorderStyle = BorderStyle.None,
                Font        = UITheme.FontBody,
                DrawMode    = DrawMode.OwnerDrawFixed,
                ItemHeight  = 36,
            };
            _lstStudents.DrawItem += DrawStudentItem;

            _lblStatus = UITheme.MakeLabel("", UITheme.FontSmall, UITheme.TextMuted);
            _lblStatus.Location = new Point(20, 408);
            _lblStatus.AutoSize = true;

            _btnConfirm = UITheme.MakeButton(L.ConfirmCheckIn);
            _btnConfirm.Location = new Point(300, 430);
            _btnConfirm.Width    = 140;
            _btnConfirm.Click   += async (_, __) => await Confirm();

            var btnCancel = new GhostBtn { Text = L.CancelBtn, Location = new Point(20, 432), Width = 100, Font = UITheme.FontBody };
            btnCancel.Click += (_, __) => { DialogResult = DialogResult.Cancel; Close(); };

            Controls.Add(title);
            Controls.Add(lblSub);
            Controls.Add(_txtSearch);
            Controls.Add(_lstStudents);
            Controls.Add(_lblStatus);
            Controls.Add(_btnConfirm);
            Controls.Add(btnCancel);
        }

        private void LoadStudents()
        {
            _students = _db.GetCachedStudents();
            FilterStudents();
        }

        private void FilterStudents()
        {
            var q = _txtSearch.Text.Trim().ToLower();
            _lstStudents.Items.Clear();
            foreach (var s in _students)
                if (string.IsNullOrEmpty(q) || s.FullName.ToLower().Contains(q))
                    _lstStudents.Items.Add(s);
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
                using var b = new SolidBrush(UITheme.TextMuted);
                e.Graphics.DrawString(stu.FullName, UITheme.FontBodyBold, n, e.Bounds.X + 10, e.Bounds.Y + 4);
                e.Graphics.DrawString(stu.BeltName,  UITheme.FontSmall,   b, e.Bounds.X + 10, e.Bounds.Y + 20);
            }
        }

        private async System.Threading.Tasks.Task Confirm()
        {
            if (_lstStudents.SelectedItem is not Student stu) { ShowStatus(L.SelectStudent, UITheme.Warning); return; }
            _btnConfirm.Enabled = false;
            Result = await _attendance.ManualCheckInAsync(stu);

            if (Result.Result == CheckInResult.Success)
            {
                ShowStatus(L.ManualOk, UITheme.Success);
                await System.Threading.Tasks.Task.Delay(600);
                DialogResult = DialogResult.OK;
                Close();
            }
            else if (Result.Result == CheckInResult.AlreadyCheckedIn)
            {
                ShowStatus($"{L.AlreadyCheckedIn} ({Result.MinutesAgo} {L.AlreadyTime})", UITheme.Warning);
                _btnConfirm.Enabled = true;
            }
        }

        private void ShowStatus(string msg, Color color)
        {
            _lblStatus.Text      = msg;
            _lblStatus.ForeColor = color;
        }
    }
}
