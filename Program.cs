using System;
using System.IO;
using System.Windows.Forms;
using BBEdgeAgent.Forms;
using BBEdgeAgent.Services;

namespace BBEdgeAgent
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Ensure app data dir exists
            Directory.CreateDirectory(AppConfig.AppDataDir);

            // Load config
            var cfg = AppConfig.Load();
            L.Current = cfg.Language;

            // Show login
            var login = new frmLogin(cfg);
            if (login.ShowDialog() != DialogResult.OK) return;

            // Create services
            var db         = new LocalDbService(AppConfig.DbPath);
            var supabase   = new SupabaseService(cfg);
            var rfid       = new RfidService(cfg);
            var attendance = new AttendanceService(db, supabase, cfg);

            // Show main form
            Application.Run(new frmMain(cfg, rfid, supabase, db, attendance));

            // Cleanup
            rfid.Dispose();
            db.Dispose();
        }
    }
}
