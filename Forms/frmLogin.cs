using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace BBEdgeAgent.Forms
{
    public class frmLogin : Form
    {
        private AppConfig _cfg;
        private TextBox   _txtPin   = null!;
        private Label     _lblError = null!;

        public frmLogin(AppConfig cfg)
        {
            _cfg = cfg;
            L.Current = cfg.Language;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            UITheme.ApplyToForm(this);
            Text            = "BB Edge Agent";
            Size            = new Size(560, 700);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            StartPosition   = FormStartPosition.CenterScreen;

            // ── Full background panel ─────────────────────────────
            var bg = new Panel { Dock = DockStyle.Fill, BackColor = UITheme.BgDark };
            Controls.Add(bg);

            // ── Logo ─────────────────────────────────────────────
            var logo = new PictureBox
            {
                Size      = new Size(80, 80),
                SizeMode  = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
            };
            TryLoadLogo(logo);
            logo.Location = new Point((560 - 80) / 2, 60);

            // ── Title ─────────────────────────────────────────────
            var lblApp = new Label
            {
                Text      = "BB TAEKWONDO",
                Font      = new Font("Segoe UI", 24f, FontStyle.Bold),
                ForeColor = UITheme.AccentGold,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize  = false,
                Width     = 520,
                Height    = 40,
                Location  = new Point(20, 158),
            };

            var lblSub = new Label
            {
                Text      = "Edge Agent",
                Font      = new Font("Segoe UI", 12f),
                ForeColor = UITheme.TextMuted,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize  = false,
                Width     = 520,
                Height    = 26,
                Location  = new Point(20, 200),
            };

            // ── Login card ────────────────────────────────────────
            var card = new Panel
            {
                BackColor = UITheme.BgCard,
                Width     = 380,
                Height    = 300,
                Location  = new Point((560 - 380) / 2, 248),
            };

            var lblTitle = new Label
            {
                Text      = L.LoginTitle,
                Font      = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                AutoSize  = false,
                Width     = 340,
                Height    = 30,
                Location  = new Point(20, 20),
            };

            var lblPin = new Label
            {
                Text      = L.PinLabel,
                Font      = UITheme.FontSmall,
                ForeColor = UITheme.TextSecond,
                BackColor = Color.Transparent,
                AutoSize  = false,
                Width     = 340,
                Height    = 20,
                Location  = new Point(20, 60),
            };

            _txtPin = new TextBox
            {
                Font         = new Font("Consolas", 28f, FontStyle.Bold),
                PasswordChar = '●',
                MaxLength    = 4,
                Width        = 340,
                Height       = 60,
                Location     = new Point(20, 82),
                BackColor    = Color.FromArgb(18, 26, 42),
                ForeColor    = UITheme.TextPrimary,
                BorderStyle  = BorderStyle.FixedSingle,
                TextAlign    = HorizontalAlignment.Center,
            };
            _txtPin.KeyPress += (_, e) =>
            {
                if (!char.IsDigit(e.KeyChar) && e.KeyChar != (char)Keys.Back) e.Handled = true;
            };
            _txtPin.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) TryLogin(); };

            _lblError = new Label
            {
                Text      = "",
                Font      = UITheme.FontSmall,
                ForeColor = UITheme.Error,
                BackColor = Color.Transparent,
                AutoSize  = false,
                Width     = 340,
                Height    = 22,
                Location  = new Point(20, 152),
            };

            var btnLogin = UITheme.MakeButton(L.LoginBtn);
            btnLogin.Width    = 340;
            btnLogin.Height   = 48;
            btnLogin.Location = new Point(20, 176);
            btnLogin.Font     = new Font("Segoe UI", 12f, FontStyle.Bold);
            btnLogin.Click   += (_, __) => TryLogin();

            var lblHint = new Label
            {
                Text      = L.DefaultPinHint,
                Font      = UITheme.FontSmall,
                ForeColor = UITheme.TextMuted,
                BackColor = Color.Transparent,
                AutoSize  = false,
                Width     = 340,
                Height    = 20,
                Location  = new Point(20, 234),
                TextAlign = ContentAlignment.MiddleCenter,
            };

            card.Controls.Add(lblTitle);
            card.Controls.Add(lblPin);
            card.Controls.Add(_txtPin);
            card.Controls.Add(_lblError);
            card.Controls.Add(btnLogin);
            card.Controls.Add(lblHint);

            // ── Language button ───────────────────────────────────
            var btnLang = new GhostBtn
            {
                Text     = L.Current == "en" ? "한국어" : "English",
                Width    = 90,
                Height   = 30,
                Font     = UITheme.FontSmall,
                Location = new Point(560 - 110, 700 - 60),
            };
            btnLang.Click += (_, __) =>
            {
                L.Current = L.Current == "en" ? "ko" : "en";
                _cfg.Language = L.Current;
                _cfg.Save();
                Application.Restart();
            };

            bg.Controls.Add(logo);
            bg.Controls.Add(lblApp);
            bg.Controls.Add(lblSub);
            bg.Controls.Add(card);
            bg.Controls.Add(btnLang);

            ActiveControl = _txtPin;
        }

        private void TryLogin()
        {
            _lblError.Text = "";
            if (_cfg.VerifyPin(_txtPin.Text.Trim()))
            {
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                _lblError.Text = L.InvalidPin;
                _txtPin.Clear();
                _txtPin.Focus();
            }
        }

        private static void TryLoadLogo(PictureBox pb)
        {
            var candidates = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bb.png"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "bb.png"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "bb.png"),
            };
            foreach (var p in candidates)
                if (File.Exists(p)) { pb.Image = Image.FromFile(p); return; }
        }
    }
}
