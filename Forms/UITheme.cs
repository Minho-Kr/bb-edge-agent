using System.Drawing;
using System.Windows.Forms;

namespace BBEdgeAgent.Forms
{
    /// <summary>Modern dark navy theme — consistent with BB AI Hub web app.</summary>
    public static class UITheme
    {
        // ── Colors ───────────────────────────────────────────────
        public static readonly Color BgDark       = Color.FromArgb(18, 26, 42);    // main background
        public static readonly Color BgCard       = Color.FromArgb(30, 42, 62);    // card / panel surface
        public static readonly Color BgCardHover  = Color.FromArgb(38, 52, 75);
        public static readonly Color BgSidebar    = Color.FromArgb(24, 34, 52);
        public static readonly Color AccentGold   = Color.FromArgb(245, 158, 11);  // amber gold
        public static readonly Color AccentBlue   = Color.FromArgb(59, 130, 246);
        public static readonly Color TextPrimary  = Color.FromArgb(241, 245, 249);
        public static readonly Color TextSecond   = Color.FromArgb(148, 163, 184);
        public static readonly Color TextMuted    = Color.FromArgb(100, 116, 139);
        public static readonly Color Success      = Color.FromArgb(16, 185, 129);
        public static readonly Color Error        = Color.FromArgb(239, 68, 68);
        public static readonly Color Warning      = Color.FromArgb(245, 158, 11);
        public static readonly Color Border       = Color.FromArgb(51, 65, 85);

        // ── Fonts ────────────────────────────────────────────────
        public static readonly Font FontTitle     = new Font("Segoe UI", 22f, FontStyle.Bold);
        public static readonly Font FontLarge     = new Font("Segoe UI", 15f, FontStyle.Bold);
        public static readonly Font FontBody      = new Font("Segoe UI", 10f);
        public static readonly Font FontBodyBold  = new Font("Segoe UI", 10f, FontStyle.Bold);
        public static readonly Font FontSmall     = new Font("Segoe UI", 8.5f);
        public static readonly Font FontMono      = new Font("Consolas",  9f);

        // ── Apply to form ────────────────────────────────────────
        public static void ApplyToForm(Form form)
        {
            form.BackColor = BgDark;
            form.ForeColor = TextPrimary;
            form.Font      = FontBody;
        }

        // ── Control factories ────────────────────────────────────
        public static Label MakeLabel(string text, Font? font = null, Color? color = null)
        {
            return new Label
            {
                Text      = text,
                ForeColor = color ?? TextPrimary,
                BackColor = Color.Transparent,
                Font      = font ?? FontBody,
                AutoSize  = true,
            };
        }

        public static TextBox MakeTextBox(bool password = false)
        {
            var tb = new TextBox
            {
                BackColor      = BgCard,
                ForeColor      = TextPrimary,
                BorderStyle    = BorderStyle.FixedSingle,
                Font           = FontBody,
                Height         = 30,
            };
            if (password) tb.PasswordChar = '●';
            return tb;
        }

        public static Button MakeButton(string text, Color? bg = null, Color? fg = null)
        {
            return new FlatBtn
            {
                Text      = text,
                BackColor = bg ?? AccentGold,
                ForeColor = fg ?? Color.FromArgb(18, 26, 42),
                Font      = FontBodyBold,
                Height    = 38,
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand,
            };
        }

        public static Panel MakeCard(int padding = 16)
        {
            return new Panel
            {
                BackColor = BgCard,
                Padding   = new Padding(padding),
            };
        }

        public static Label MakeSeparator()
        {
            return new Label
            {
                Height    = 1,
                BackColor = Border,
                Dock      = DockStyle.Top,
            };
        }
    }

    /// <summary>Flat button with rounded feel via FlatAppearance.</summary>
    public class FlatBtn : Button
    {
        public FlatBtn()
        {
            FlatStyle                       = FlatStyle.Flat;
            FlatAppearance.BorderSize       = 0;
            FlatAppearance.MouseOverBackColor = Color.FromArgb(252, 172, 60);
            FlatAppearance.MouseDownBackColor = Color.FromArgb(220, 138, 5);
        }
    }

    /// <summary>Ghost button (outline style).</summary>
    public class GhostBtn : Button
    {
        public GhostBtn()
        {
            FlatStyle                       = FlatStyle.Flat;
            BackColor                       = Color.Transparent;
            ForeColor                       = UITheme.TextSecond;
            FlatAppearance.BorderColor      = UITheme.Border;
            FlatAppearance.BorderSize       = 1;
            FlatAppearance.MouseOverBackColor = UITheme.BgCardHover;
            FlatAppearance.MouseDownBackColor = UITheme.BgCard;
            Cursor                          = Cursors.Hand;
            Height                          = 38;
        }
    }
}
