using System;
using System.Collections.Generic;

namespace BBEdgeAgent.Models
{
    public class Student
    {
        public string   Id          { get; set; } = "";
        public string   FirstName   { get; set; } = "";
        public string   LastName    { get; set; } = "";
        public string   BeltName    { get; set; } = "";   // resolved from belt_grades
        public string   BeltCode    { get; set; } = "";
        public string   ClassType   { get; set; } = "";
        public string   Status      { get; set; } = "active";
        public List<int> ClassDays  { get; set; } = new List<int>(); // 1=Mon,...,6=Sat

        public string   FullName    => $"{FirstName} {LastName}";
        public string   Display     => $"{FullName}  [{BeltName}]";

        public List<RfidTag> Tags   { get; set; } = new();
    }

    public class RfidTag
    {
        public string   Id          { get; set; } = "";
        public string   TagUid      { get; set; } = "";
        public string   StudentId   { get; set; } = "";
        public string   Notes       { get; set; } = "";
        public bool     IsActive    { get; set; } = true;
        public DateTime AssignedAt  { get; set; } = DateTime.UtcNow;
    }

    public class AttendanceRecord
    {
        public string   Id          { get; set; } = Guid.NewGuid().ToString();
        public string   StudentId   { get; set; } = "";
        public string   BranchId    { get; set; } = "";
        public DateTime AttendedAt  { get; set; } = DateTime.UtcNow;
        public string   Source      { get; set; } = "auto_rfid";  // or "manual"
        public string?  RfidTagId   { get; set; }
        public string?  RfidTagUid  { get; set; }
        public bool     Synced      { get; set; } = false;

        // UI helpers (not in DB)
        public string   StudentName { get; set; } = "";
        public string   BeltName    { get; set; } = "";
    }

    public class RfidLogEntry
    {
        public string   TagUid      { get; set; } = "";
        public string   StudentName { get; set; } = "";
        public DateTime FirstScan   { get; set; }
        public DateTime LastScan    { get; set; }
        public int      ScanCount   { get; set; } = 1;
        public string   Result      { get; set; } = "";
        public string   FirstScanDisplay => FirstScan.ToString("HH:mm:ss");
        public string   ResultDisplay => Result switch
        {
            "success"            => "✓ Success",
            "already_checked_in" => "↩ Already In",
            "tag_not_found"      => "? Unknown Tag",
            _                    => Result,
        };
    }
}
