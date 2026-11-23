using System;
using System.Collections.Generic;
using System.Data.SQLite;
using BBEdgeAgent.Models;

namespace BBEdgeAgent.Services
{
    public class LocalDbService : IDisposable
    {
        private readonly string _connStr;
        private SQLiteConnection? _conn;

        public LocalDbService(string dbPath)
        {
            _connStr = $"Data Source={dbPath};Version=3;";
            Open();
            EnsureSchema();
        }

        private void Open()
        {
            _conn = new SQLiteConnection(_connStr);
            _conn.Open();
        }

        private void EnsureSchema()
        {
            Execute(@"
                CREATE TABLE IF NOT EXISTS attendance_queue (
                    id           TEXT PRIMARY KEY,
                    student_id   TEXT NOT NULL,
                    branch_id    TEXT NOT NULL,
                    attended_at  TEXT NOT NULL,
                    source       TEXT NOT NULL DEFAULT 'auto_rfid',
                    rfid_tag_id  TEXT,
                    rfid_tag_uid TEXT,
                    synced       INTEGER NOT NULL DEFAULT 0,
                    created_at   TEXT NOT NULL DEFAULT (datetime('now'))
                );
                CREATE TABLE IF NOT EXISTS cached_students (
                    id           TEXT PRIMARY KEY,
                    first_name   TEXT NOT NULL,
                    last_name    TEXT NOT NULL,
                    belt_name    TEXT,
                    belt_code    TEXT,
                    class_type   TEXT,
                    status       TEXT DEFAULT 'active',
                    last_sync    TEXT
                );
                CREATE TABLE IF NOT EXISTS rfid_cache (
                    tag_uid      TEXT PRIMARY KEY,
                    tag_id       TEXT,
                    student_id   TEXT NOT NULL,
                    student_name TEXT NOT NULL,
                    belt_name    TEXT,
                    is_active    INTEGER NOT NULL DEFAULT 1,
                    last_sync    TEXT
                );");
        }

        // ── Attendance Queue ─────────────────────────────────────

        public void EnqueueAttendance(AttendanceRecord rec)
        {
            Execute(@"INSERT OR REPLACE INTO attendance_queue
                      (id,student_id,branch_id,attended_at,source,rfid_tag_id,rfid_tag_uid,synced)
                      VALUES(@id,@sid,@bid,@at,@src,@tid,@tuid,0)",
                P("@id",   rec.Id),
                P("@sid",  rec.StudentId),
                P("@bid",  rec.BranchId),
                P("@at",   rec.AttendedAt.ToString("o")),
                P("@src",  rec.Source),
                P("@tid",  rec.RfidTagId   ?? (object)DBNull.Value),
                P("@tuid", rec.RfidTagUid  ?? (object)DBNull.Value));
        }

        public List<AttendanceRecord> GetUnsynced(int limit = 50)
        {
            var list = new List<AttendanceRecord>();
            using var cmd = new SQLiteCommand(
                "SELECT * FROM attendance_queue WHERE synced=0 ORDER BY attended_at LIMIT @lim", _conn);
            cmd.Parameters.AddWithValue("@lim", limit);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new AttendanceRecord
                {
                    Id         = r["id"].ToString()!,
                    StudentId  = r["student_id"].ToString()!,
                    BranchId   = r["branch_id"].ToString()!,
                    AttendedAt = DateTime.Parse(r["attended_at"].ToString()!).ToUniversalTime(),
                    Source     = r["source"].ToString()!,
                    RfidTagId  = r["rfid_tag_id"] as string,
                    RfidTagUid = r["rfid_tag_uid"] as string,
                });
            return list;
        }

        public void MarkSynced(string id) =>
            Execute("UPDATE attendance_queue SET synced=1 WHERE id=@id", P("@id", id));

        public int GetUnsyncedCount()
        {
            using var cmd = new SQLiteCommand(
                "SELECT COUNT(*) FROM attendance_queue WHERE synced=0", _conn);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        // ── Student Cache ────────────────────────────────────────

        public void CacheStudents(IEnumerable<Student> students)
        {
            Execute("DELETE FROM cached_students");
            foreach (var s in students)
                Execute(@"INSERT INTO cached_students (id,first_name,last_name,belt_name,belt_code,class_type,status,last_sync)
                          VALUES(@id,@fn,@ln,@bn,@bc,@ct,@st,@ls)",
                    P("@id",  s.Id),
                    P("@fn",  s.FirstName),
                    P("@ln",  s.LastName),
                    P("@bn",  s.BeltName),
                    P("@bc",  s.BeltCode),
                    P("@ct",  s.ClassType),
                    P("@st",  s.Status),
                    P("@ls",  DateTime.UtcNow.ToString("o")));
        }

        public List<Student> GetCachedStudents(string? filter = null)
        {
            var list = new List<Student>();
            var sql = "SELECT * FROM cached_students WHERE status='active'";
            if (!string.IsNullOrEmpty(filter))
                sql += " AND (first_name LIKE @f OR last_name LIKE @f)";
            sql += " ORDER BY last_name, first_name";
            using var cmd = new SQLiteCommand(sql, _conn);
            if (!string.IsNullOrEmpty(filter))
                cmd.Parameters.AddWithValue("@f", $"%{filter}%");
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new Student
                {
                    Id        = r["id"].ToString()!,
                    FirstName = r["first_name"].ToString()!,
                    LastName  = r["last_name"].ToString()!,
                    BeltName  = r["belt_name"]?.ToString() ?? "",
                    BeltCode  = r["belt_code"]?.ToString() ?? "",
                    ClassType = r["class_type"]?.ToString() ?? "",
                    Status    = r["status"].ToString()!,
                });
            return list;
        }

        // ── RFID Cache ───────────────────────────────────────────

        public void CacheRfidTags(IEnumerable<(string tagId, string tagUid, string studentId, string studentName, string beltName)> tags)
        {
            Execute("DELETE FROM rfid_cache");
            foreach (var t in tags)
                Execute(@"INSERT INTO rfid_cache (tag_uid,tag_id,student_id,student_name,belt_name,is_active,last_sync)
                          VALUES(@uid,@tid,@sid,@sn,@bn,1,@ls)",
                    P("@uid", t.tagUid),
                    P("@tid", t.tagId),
                    P("@sid", t.studentId),
                    P("@sn",  t.studentName),
                    P("@bn",  t.beltName),
                    P("@ls",  DateTime.UtcNow.ToString("o")));
        }

        public (string? tagId, string? studentId, string? studentName, string? beltName) LookupTag(string tagUid)
        {
            using var cmd = new SQLiteCommand(
                "SELECT tag_id,student_id,student_name,belt_name FROM rfid_cache WHERE tag_uid=@uid AND is_active=1",
                _conn);
            cmd.Parameters.AddWithValue("@uid", tagUid.ToUpperInvariant());
            using var r = cmd.ExecuteReader();
            if (r.Read())
                return (r["tag_id"] as string, r["student_id"] as string,
                        r["student_name"] as string, r["belt_name"] as string);
            return (null, null, null, null);
        }

        // ── Recent Check ─────────────────────────────────────────

        public DateTime? GetLastAttendance(string studentId)
        {
            using var cmd = new SQLiteCommand(
                "SELECT attended_at FROM attendance_queue WHERE student_id=@sid ORDER BY attended_at DESC LIMIT 1",
                _conn);
            cmd.Parameters.AddWithValue("@sid", studentId);
            var val = cmd.ExecuteScalar();
            if (val == null || val == DBNull.Value) return null;
            return DateTime.Parse(val.ToString()!).ToUniversalTime();
        }

        // ── Helpers ──────────────────────────────────────────────

        private void Execute(string sql, params SQLiteParameter[] parms)
        {
            using var cmd = new SQLiteCommand(sql, _conn);
            cmd.Parameters.AddRange(parms);
            cmd.ExecuteNonQuery();
        }

        private static SQLiteParameter P(string name, object? value) =>
            new SQLiteParameter(name, value ?? DBNull.Value);

        public void Dispose() => _conn?.Dispose();
    }
}
