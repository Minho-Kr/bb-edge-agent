using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using BBEdgeAgent.Models;

namespace BBEdgeAgent.Services
{
    public class SupabaseService
    {
        private readonly HttpClient _http = new HttpClient();
        private AppConfig _cfg;
        private string? _jwt;

        public bool IsOnline { get; private set; }

        public SupabaseService(AppConfig cfg)
        {
            _cfg = cfg;
            _http.Timeout = TimeSpan.FromSeconds(10);
        }

        public void UpdateConfig(AppConfig cfg)
        {
            _cfg = cfg;
            _jwt = null;
            IsOnline = false;
        }

        // ── Auth ─────────────────────────────────────────────────

        public async Task<bool> SignInAsync()
        {
            if (string.IsNullOrEmpty(_cfg.SupabaseUrl) || string.IsNullOrEmpty(_cfg.SupabaseKey))
            {
                IsOnline = false;
                return false;
            }

            // If no edge email provided, use the API key directly as JWT (service role key mode)
            if (string.IsNullOrEmpty(_cfg.EdgeEmail))
            {
                _jwt     = _cfg.SupabaseKey;
                IsOnline = await PingAsync();
                return IsOnline;
            }

            // Email/password auth
            try
            {
                var body = JsonConvert.SerializeObject(new
                {
                    email    = _cfg.EdgeEmail,
                    password = _cfg.EdgePassword
                });
                var req = new HttpRequestMessage(HttpMethod.Post,
                    $"{_cfg.SupabaseUrl}/auth/v1/token?grant_type=password");
                req.Headers.Add("apikey", _cfg.SupabaseKey);
                req.Content = new StringContent(body, Encoding.UTF8, "application/json");

                var res = await _http.SendAsync(req);
                if (!res.IsSuccessStatusCode) { IsOnline = false; return false; }

                var json = await res.Content.ReadAsStringAsync();
                var obj  = JObject.Parse(json);
                _jwt     = obj["access_token"]?.ToString();
                IsOnline = _jwt != null;
                return IsOnline;
            }
            catch { IsOnline = false; return false; }
        }

        private async Task<bool> PingAsync()
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get,
                    $"{_cfg.SupabaseUrl}/rest/v1/branches?select=id&limit=1");
                AddHeaders(req);
                var res = await _http.SendAsync(req);
                // 200 OK or 406 (no rows) both mean connection is working
                return res.IsSuccessStatusCode ||
                       (int)res.StatusCode == 406 ||
                       (int)res.StatusCode == 400;
            }
            catch { return false; }
        }

        public async Task<bool> CheckOnlineAsync()
        {
            if (string.IsNullOrEmpty(_jwt))
                return await SignInAsync();
            try
            {
                var req = BuildGet($"branches?id=eq.{_cfg.BranchId}&select=id&limit=1");
                var res = await _http.SendAsync(req);
                IsOnline = res.IsSuccessStatusCode;
                if (!IsOnline) _jwt = null;
                return IsOnline;
            }
            catch { IsOnline = false; return false; }
        }

        // ── Branches ─────────────────────────────────────────────

        public async Task<List<(string id, string name)>> GetBranchesAsync()
        {
            var list = new List<(string, string)>();
            try
            {
                var req = BuildGet("branches?select=id,name&is_active=eq.true&order=name");
                var res = await _http.SendAsync(req);
                if (!res.IsSuccessStatusCode) return list;
                var json = await res.Content.ReadAsStringAsync();
                var arr  = JArray.Parse(json);
                foreach (var item in arr)
                    list.Add((item["id"]?.ToString() ?? "", item["name"]?.ToString() ?? ""));
            }
            catch { }
            return list;
        }

        // ── Students ─────────────────────────────────────────────

        public async Task<List<Student>> GetStudentsAsync()
        {
            var students = new List<Student>();
            try
            {
                // Get active students with belt grades
                var url = $"students?branch_id=eq.{_cfg.BranchId}" +
                          "&status=eq.active" +
                          "&select=id,first_name,last_name,class_type,class_days,status,belt_grade_id,belt_grades(name,short_code)";
                var req = BuildGet(url);
                var res = await _http.SendAsync(req);
                if (!res.IsSuccessStatusCode) return students;

                var json = await res.Content.ReadAsStringAsync();
                var arr  = JArray.Parse(json);
                foreach (var item in arr)
                {
                    var bg   = item["belt_grades"];
                    var days = item["class_days"];
                    students.Add(new Student
                    {
                        Id        = item["id"]?.ToString()        ?? "",
                        FirstName = item["first_name"]?.ToString() ?? "",
                        LastName  = item["last_name"]?.ToString()  ?? "",
                        ClassType = item["class_type"]?.ToString() ?? "",
                        Status    = item["status"]?.ToString()     ?? "active",
                        BeltName  = bg?["name"]?.ToString()       ?? "",
                        BeltCode  = bg?["short_code"]?.ToString() ?? "",
                        ClassDays = days?.Type == JTokenType.Array
                                    ? days.ToObject<List<int>>() ?? new System.Collections.Generic.List<int>()
                                    : new System.Collections.Generic.List<int>(),
                    });
                }
            }
            catch { }
            return students;
        }

        // ── RFID Tags ────────────────────────────────────────────

        public async Task<List<(string tagId, string tagUid, string studentId, string studentName, string beltName)>>
            GetRfidTagsAsync()
        {
            var result = new List<(string, string, string, string, string)>();
            try
            {
                var url = $"rfid_tags?branch_id=eq.{_cfg.BranchId}&is_active=eq.true" +
                          "&select=id,tag_uid,student_id,students(first_name,last_name,belt_grades(name))";
                var req = BuildGet(url);
                var res = await _http.SendAsync(req);
                if (!res.IsSuccessStatusCode) return result;

                var json = await res.Content.ReadAsStringAsync();
                var arr  = JArray.Parse(json);
                foreach (var item in arr)
                {
                    var stu     = item["students"];
                    var bg      = stu?["belt_grades"];
                    var fn      = stu?["first_name"]?.ToString() ?? "";
                    var ln      = stu?["last_name"]?.ToString()  ?? "";
                    var belt    = bg?["name"]?.ToString()        ?? "";
                    result.Add((
                        item["id"]?.ToString()      ?? "",
                        item["tag_uid"]?.ToString() ?? "",
                        item["student_id"]?.ToString() ?? "",
                        $"{fn} {ln}".Trim(),
                        belt));
                }
            }
            catch { }
            return result;
        }

        public async Task<List<(string tagId, string tagUid, string notes, DateTime assignedAt)>>
            GetTagsForStudentAsync(string studentId)
        {
            var result = new List<(string, string, string, DateTime)>();
            try
            {
                var url = $"rfid_tags?student_id=eq.{studentId}&is_active=eq.true&select=id,tag_uid,notes,assigned_at";
                var req = BuildGet(url);
                var res = await _http.SendAsync(req);
                if (!res.IsSuccessStatusCode) return result;

                var json = await res.Content.ReadAsStringAsync();
                var arr  = JArray.Parse(json);
                foreach (var item in arr)
                {
                    var at = DateTime.TryParse(item["assigned_at"]?.ToString(), out var d) ? d : DateTime.UtcNow;
                    result.Add((
                        item["id"]?.ToString()     ?? "",
                        item["tag_uid"]?.ToString() ?? "",
                        item["notes"]?.ToString()  ?? "",
                        at));
                }
            }
            catch { }
            return result;
        }

        public async Task<bool> AssignTagAsync(string tagUid, string studentId, string? notes = null)
        {
            try
            {
                // Deactivate existing tag with same UID first
                await DeactivateTagByUidAsync(tagUid);

                var body = JsonConvert.SerializeObject(new
                {
                    tag_uid    = tagUid.ToUpperInvariant(),
                    student_id = studentId,
                    branch_id  = _cfg.BranchId,
                    notes      = notes ?? "",
                    is_active  = true
                });
                var req = BuildPost("rfid_tags", body);
                req.Headers.Add("Prefer", "return=minimal");
                var res = await _http.SendAsync(req);
                return res.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<bool> DeactivateTagAsync(string tagId)
        {
            try
            {
                var body = JsonConvert.SerializeObject(new
                {
                    is_active  = false,
                    revoked_at = DateTime.UtcNow.ToString("o")
                });
                var req = BuildPatch($"rfid_tags?id=eq.{tagId}", body);
                req.Headers.Add("Prefer", "return=minimal");
                var res = await _http.SendAsync(req);
                return res.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        private async Task DeactivateTagByUidAsync(string tagUid)
        {
            try
            {
                var body = JsonConvert.SerializeObject(new
                {
                    is_active  = false,
                    revoked_at = DateTime.UtcNow.ToString("o")
                });
                var req = BuildPatch($"rfid_tags?tag_uid=eq.{tagUid}", body);
                req.Headers.Add("Prefer", "return=minimal");
                await _http.SendAsync(req);
            }
            catch { }
        }

        // ── Attendance ───────────────────────────────────────────

        public async Task<bool> PostAttendanceAsync(AttendanceRecord rec)
        {
            try
            {
                var obj = new Dictionary<string, object?>
                {
                    ["id"]          = rec.Id,
                    ["student_id"]  = rec.StudentId,
                    ["branch_id"]   = rec.BranchId,
                    ["attended_at"] = rec.AttendedAt.ToString("o"),
                    ["source"]      = rec.Source,
                };
                if (rec.RfidTagId != null) obj["rfid_tag_id"] = rec.RfidTagId;

                var body = JsonConvert.SerializeObject(obj);
                var req  = BuildPost("attendance", body);
                req.Headers.Add("Prefer", "return=minimal");
                var res  = await _http.SendAsync(req);
                return res.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<DateTime?> GetLastAttendanceAsync(string studentId)
        {
            try
            {
                var url = $"attendance?student_id=eq.{studentId}&branch_id=eq.{_cfg.BranchId}" +
                          "&select=attended_at&order=attended_at.desc&limit=1";
                var req = BuildGet(url);
                var res = await _http.SendAsync(req);
                if (!res.IsSuccessStatusCode) return null;
                var json = await res.Content.ReadAsStringAsync();
                var arr  = JArray.Parse(json);
                if (arr.Count == 0) return null;
                return DateTime.Parse(arr[0]["attended_at"]!.ToString()).ToUniversalTime();
            }
            catch { return null; }
        }

        public async Task<Dictionary<string, DateTime>> GetTodayAttendanceAsync()
        {
            var result = new Dictionary<string, DateTime>();
            try
            {
                var start = DateTime.UtcNow.Date.ToString("yyyy-MM-ddTHH:mm:ssZ");
                var end   = DateTime.UtcNow.Date.AddDays(1).ToString("yyyy-MM-ddTHH:mm:ssZ");
                var url   = $"attendance?branch_id=eq.{_cfg.BranchId}" +
                            $"&attended_at=gte.{start}&attended_at=lt.{end}" +
                            "&select=student_id,attended_at&order=attended_at.asc";
                var req = BuildGet(url);
                var res = await _http.SendAsync(req);
                if (!res.IsSuccessStatusCode) return result;
                var json = await res.Content.ReadAsStringAsync();
                var arr  = JArray.Parse(json);
                foreach (var item in arr)
                {
                    var sid = item["student_id"]?.ToString() ?? "";
                    if (string.IsNullOrEmpty(sid) || result.ContainsKey(sid)) continue;
                    if (DateTimeOffset.TryParse(item["attended_at"]?.ToString(), out var dto))
                        result[sid] = dto.LocalDateTime;
                }
            }
            catch { }
            return result;
        }

        // ── RFID Scan Logs ───────────────────────────────────────

        public async Task LogRfidScanAsync(string tagUid, string result, string? studentId, string? studentName)
        {
            if (string.IsNullOrEmpty(_cfg.BranchId) || string.IsNullOrEmpty(tagUid)) return;
            try
            {
                // Look for existing log within 30-minute window
                var cutoff = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(-30).ToString("o"));
                var getUrl = $"rfid_scan_logs?tag_uid=eq.{tagUid}&branch_id=eq.{_cfg.BranchId}" +
                             $"&first_scanned_at=gte.{cutoff}&order=first_scanned_at.desc&limit=1";
                var getReq = BuildGet(getUrl);
                var getRes = await _http.SendAsync(getReq);
                if (getRes.IsSuccessStatusCode)
                {
                    var json = await getRes.Content.ReadAsStringAsync();
                    var arr  = JArray.Parse(json);
                    if (arr.Count > 0)
                    {
                        // Update existing record: increment count
                        var logId = arr[0]["id"]?.ToString();
                        var cnt   = arr[0]["scan_count"]?.Value<int>() ?? 1;
                        var pBody = JsonConvert.SerializeObject(new
                        {
                            scan_count      = cnt + 1,
                            last_scanned_at = DateTimeOffset.UtcNow.ToString("o"),
                            result          = result,
                        });
                        var pReq = BuildPatch($"rfid_scan_logs?id=eq.{logId}", pBody);
                        pReq.Headers.Add("Prefer", "return=minimal");
                        await _http.SendAsync(pReq);
                        return;
                    }
                }
                // Insert new log entry
                var iBody = JsonConvert.SerializeObject(new
                {
                    branch_id        = _cfg.BranchId,
                    tag_uid          = tagUid.ToUpperInvariant(),
                    student_id       = string.IsNullOrEmpty(studentId) ? null : (object?)studentId,
                    student_name     = studentName ?? "",
                    first_scanned_at = DateTimeOffset.UtcNow.ToString("o"),
                    last_scanned_at  = DateTimeOffset.UtcNow.ToString("o"),
                    scan_count       = 1,
                    result           = result,
                });
                var iReq = BuildPost("rfid_scan_logs", iBody);
                iReq.Headers.Add("Prefer", "return=minimal");
                await _http.SendAsync(iReq);
            }
            catch { }
        }

        public async Task<List<RfidLogEntry>> GetRfidLogsAsync(DateTime fromUtc, DateTime toUtc, int limit = 500)
        {
            var result = new List<RfidLogEntry>();
            try
            {
                var start = Uri.EscapeDataString(fromUtc.ToString("o"));
                var end   = Uri.EscapeDataString(toUtc.ToString("o"));
                var url   = $"rfid_scan_logs?branch_id=eq.{_cfg.BranchId}" +
                            $"&first_scanned_at=gte.{start}&first_scanned_at=lt.{end}" +
                            $"&order=first_scanned_at.desc&limit={limit}";
                var req = BuildGet(url);
                var res = await _http.SendAsync(req);
                if (!res.IsSuccessStatusCode) return result;
                var json = await res.Content.ReadAsStringAsync();
                var arr  = JArray.Parse(json);
                foreach (var item in arr)
                {
                    if (!DateTimeOffset.TryParse(item["first_scanned_at"]?.ToString(), out var first)) continue;
                    DateTimeOffset.TryParse(item["last_scanned_at"]?.ToString(), out var last);
                    result.Add(new RfidLogEntry
                    {
                        TagUid      = item["tag_uid"]?.ToString()      ?? "",
                        StudentName = item["student_name"]?.ToString() ?? "",
                        FirstScan   = first.LocalDateTime,
                        LastScan    = last.LocalDateTime,
                        ScanCount   = item["scan_count"]?.Value<int>() ?? 1,
                        Result      = item["result"]?.ToString()       ?? "",
                    });
                }
            }
            catch { }
            return result;
        }

        // ── Helpers ──────────────────────────────────────────────

        private HttpRequestMessage BuildGet(string path)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"{_cfg.SupabaseUrl}/rest/v1/{path}");
            AddHeaders(req);
            return req;
        }

        private HttpRequestMessage BuildPost(string path, string body)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, $"{_cfg.SupabaseUrl}/rest/v1/{path}");
            AddHeaders(req);
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");
            return req;
        }

        private HttpRequestMessage BuildPatch(string path, string body)
        {
            var req = new HttpRequestMessage(new HttpMethod("PATCH"), $"{_cfg.SupabaseUrl}/rest/v1/{path}");
            AddHeaders(req);
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");
            return req;
        }

        private void AddHeaders(HttpRequestMessage req)
        {
            req.Headers.Add("apikey", _cfg.SupabaseKey);
            if (!string.IsNullOrEmpty(_jwt))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _jwt);
            else
                req.Headers.Add("Authorization", $"Bearer {_cfg.SupabaseKey}");
        }
    }
}
