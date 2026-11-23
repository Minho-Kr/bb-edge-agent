using BBEdgeAgent.Models;

namespace BBEdgeAgent.Wpf.ViewModels
{
    public class AttendanceLogItem
    {
        public string Time   { get; }
        public string Name   { get; }
        public string Belt   { get; }
        public string Source { get; }

        public AttendanceLogItem(AttendanceRecord rec)
        {
            Time   = rec.AttendedAt.ToLocalTime().ToString("HH:mm:ss");
            Name   = rec.StudentName;
            Belt   = rec.BeltName;
            Source = rec.Source == "auto_rfid" ? "RFID" : "Manual";
        }
    }
}
