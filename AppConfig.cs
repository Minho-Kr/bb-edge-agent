using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace BBEdgeAgent
{
    public class AppConfig
    {
        // RFID Reader
        public string ReaderIp   { get; set; } = "192.168.2.116";
        public int    ReaderPort { get; set; } = 49152;

        // Supabase
        public string SupabaseUrl    { get; set; } = "";
        public string SupabaseKey    { get; set; } = "";   // anon or service key
        public string EdgeEmail      { get; set; } = "";   // supabase auth email for edge device
        public string EdgePassword   { get; set; } = "";   // stored locally (plain); no card data here

        // Branch
        public string BranchId   { get; set; } = "";
        public string BranchName { get; set; } = "BB Taekwondo";

        // Attendance
        public int  DebounceMinutes   { get; set; } = 3;
        public bool AutoStartScanning { get; set; } = true;

        // Security
        public string OperatorPinHash { get; set; } = "";  // SHA-256 of PIN; empty = "1234"

        // UI
        public string Language { get; set; } = "en";       // "en" or "ko"

        // ── Paths ────────────────────────────────────────────────
        public static readonly string AppDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BBEdgeAgent");

        public static readonly string ConfigPath = Path.Combine(AppDataDir, "config.json");
        public static readonly string DbPath     = Path.Combine(AppDataDir, "attendance.db");

        // ── Load / Save ──────────────────────────────────────────
        public static AppConfig Load()
        {
            AppConfig cfg;
            try
            {
                if (File.Exists(ConfigPath))
                    cfg = JsonConvert.DeserializeObject<AppConfig>(File.ReadAllText(ConfigPath))
                          ?? new AppConfig();
                else
                    cfg = new AppConfig();
            }
            catch { cfg = new AppConfig(); }

            // If Supabase not configured yet, try reading from bb-ai-hub .env
            if (string.IsNullOrEmpty(cfg.SupabaseUrl) || string.IsNullOrEmpty(cfg.SupabaseKey))
                cfg.TryLoadFromDotEnv();

            return cfg;
        }

        // Reads VITE_SUPABASE_URL and VITE_SUPABASE_ANON_KEY from sibling bb-ai-hub/.env
        public void TryLoadFromDotEnv()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                // next to exe (deployed)
                Path.Combine(baseDir, ".env"),
                // dev: bin\Debug\net48\ → up 3 → bb-edge-agent → up 1 → mh_project → bb-ai-hub
                Path.Combine(baseDir, "..", "..", "..", "..", "bb-ai-hub", ".env"),
                Path.Combine(baseDir, "..", "..", "..", "bb-ai-hub", ".env"),
                // if run from project root
                Path.Combine(baseDir, "..", "bb-ai-hub", ".env"),
                Path.Combine(baseDir, "..", ".env"),
            };
            foreach (var p in candidates)
            {
                if (!File.Exists(p)) continue;
                foreach (var line in File.ReadAllLines(p))
                {
                    if (line.StartsWith("#") || !line.Contains("=")) continue;
                    var idx = line.IndexOf('=');
                    var key = line.Substring(0, idx).Trim();
                    var val = line.Substring(idx + 1).Trim();
                    if (key == "VITE_SUPABASE_URL"      && string.IsNullOrEmpty(SupabaseUrl))
                        SupabaseUrl = val;
                    if (key == "VITE_SUPABASE_ANON_KEY" && string.IsNullOrEmpty(SupabaseKey))
                        SupabaseKey = val;
                }
                if (!string.IsNullOrEmpty(SupabaseUrl)) break;
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(AppDataDir);
                File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch { }
        }

        // ── PIN ──────────────────────────────────────────────────
        public bool VerifyPin(string pin)
        {
            if (string.IsNullOrEmpty(OperatorPinHash))
                return pin == "1234";
            return HashPin(pin) == OperatorPinHash;
        }

        public void SetPin(string pin) => OperatorPinHash = HashPin(pin);

        private static string HashPin(string pin)
        {
            using var sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(pin)));
        }

        // ── Validation ───────────────────────────────────────────
        public bool IsSupabaseConfigured =>
            !string.IsNullOrEmpty(SupabaseUrl) &&
            !string.IsNullOrEmpty(SupabaseKey) &&
            !string.IsNullOrEmpty(BranchId);

        public bool IsReaderConfigured =>
            !string.IsNullOrEmpty(ReaderIp) && ReaderPort > 0;
    }
}
