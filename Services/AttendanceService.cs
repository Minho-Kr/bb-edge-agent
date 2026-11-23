using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BBEdgeAgent.Models;

namespace BBEdgeAgent.Services
{
    public enum CheckInResult { Success, AlreadyCheckedIn, TagNotFound, Error }

    public class CheckInOutcome
    {
        public CheckInResult    Result      { get; set; }
        public AttendanceRecord? Record     { get; set; }
        public string?          StudentName { get; set; }
        public string?          BeltName    { get; set; }
        public int              MinutesAgo  { get; set; }
    }

    public class AttendanceService
    {
        private readonly LocalDbService   _db;
        private readonly SupabaseService  _supabase;
        private readonly AppConfig        _cfg;

        // In-memory debounce: tagUid → last seen time
        private readonly Dictionary<string, DateTime> _tagDebounce = new();

        public AttendanceService(LocalDbService db, SupabaseService supabase, AppConfig cfg)
        {
            _db       = db;
            _supabase = supabase;
            _cfg      = cfg;
        }

        // ── Main check-in from RFID tag ──────────────────────────

        public async Task<CheckInOutcome> ProcessTagAsync(string tagUid)
        {
            // 1. Tag-level debounce (fast in-memory check)
            if (_tagDebounce.TryGetValue(tagUid, out var lastTag))
            {
                var elapsed = (int)(DateTime.UtcNow - lastTag).TotalMinutes;
                if (elapsed < _cfg.DebounceMinutes)
                {
                    _ = _supabase.LogRfidScanAsync(tagUid, "already_checked_in", null, null);
                    return new CheckInOutcome
                    {
                        Result     = CheckInResult.AlreadyCheckedIn,
                        MinutesAgo = elapsed
                    };
                }
            }

            // 2. Lookup student from local cache
            var (tagId, studentId, studentName, beltName) = _db.LookupTag(tagUid);
            if (studentId == null)
            {
                _ = _supabase.LogRfidScanAsync(tagUid, "tag_not_found", null, null);
                return new CheckInOutcome { Result = CheckInResult.TagNotFound };
            }

            // 3. Student-level debounce (check recent records in local DB)
            var lastStudentCheck = _db.GetLastAttendance(studentId);
            if (lastStudentCheck.HasValue)
            {
                var elapsed = (int)(DateTime.UtcNow - lastStudentCheck.Value).TotalMinutes;
                if (elapsed < _cfg.DebounceMinutes)
                {
                    _tagDebounce[tagUid] = lastStudentCheck.Value;
                    _ = _supabase.LogRfidScanAsync(tagUid, "already_checked_in", studentId, studentName);
                    return new CheckInOutcome
                    {
                        Result      = CheckInResult.AlreadyCheckedIn,
                        StudentName = studentName,
                        BeltName    = beltName,
                        MinutesAgo  = elapsed
                    };
                }
            }

            // 4. Record attendance
            var rec = new AttendanceRecord
            {
                StudentId   = studentId,
                BranchId    = _cfg.BranchId,
                AttendedAt  = DateTime.UtcNow,
                Source      = "auto_rfid",
                RfidTagId   = tagId,
                RfidTagUid  = tagUid,
                StudentName = studentName ?? "",
                BeltName    = beltName   ?? "",
            };

            // 5. Try Supabase first; fall back to local queue
            if (_supabase.IsOnline)
            {
                var ok = await _supabase.PostAttendanceAsync(rec);
                rec.Synced = ok;
                if (!ok) _db.EnqueueAttendance(rec);
            }
            else
            {
                _db.EnqueueAttendance(rec);
            }

            // 6. Update debounce
            _tagDebounce[tagUid] = rec.AttendedAt;

            _ = _supabase.LogRfidScanAsync(tagUid, "success", studentId, studentName);
            return new CheckInOutcome
            {
                Result      = CheckInResult.Success,
                Record      = rec,
                StudentName = studentName,
                BeltName    = beltName,
            };
        }

        // ── Manual check-in (no tag) ─────────────────────────────

        public async Task<CheckInOutcome> ManualCheckInAsync(Student student)
        {
            // Student-level debounce
            var lastCheck = _db.GetLastAttendance(student.Id);
            if (lastCheck.HasValue)
            {
                var elapsed = (int)(DateTime.UtcNow - lastCheck.Value).TotalMinutes;
                if (elapsed < _cfg.DebounceMinutes)
                    return new CheckInOutcome
                    {
                        Result      = CheckInResult.AlreadyCheckedIn,
                        StudentName = student.FullName,
                        BeltName    = student.BeltName,
                        MinutesAgo  = elapsed,
                    };
            }

            var rec = new AttendanceRecord
            {
                StudentId   = student.Id,
                BranchId    = _cfg.BranchId,
                AttendedAt  = DateTime.UtcNow,
                Source      = "manual",
                StudentName = student.FullName,
                BeltName    = student.BeltName,
            };

            if (_supabase.IsOnline)
            {
                var ok = await _supabase.PostAttendanceAsync(rec);
                rec.Synced = ok;
                if (!ok) _db.EnqueueAttendance(rec);
            }
            else
            {
                _db.EnqueueAttendance(rec);
            }

            return new CheckInOutcome
            {
                Result      = CheckInResult.Success,
                Record      = rec,
                StudentName = student.FullName,
                BeltName    = student.BeltName,
            };
        }

        // ── Sync queue ───────────────────────────────────────────

        public async Task<int> SyncQueueAsync()
        {
            if (!_supabase.IsOnline) return 0;
            var items = _db.GetUnsynced();
            int synced = 0;
            foreach (var rec in items)
            {
                var ok = await _supabase.PostAttendanceAsync(rec);
                if (ok) { _db.MarkSynced(rec.Id); synced++; }
            }
            return synced;
        }

        public int GetUnsyncedCount() => _db.GetUnsyncedCount();
    }
}
