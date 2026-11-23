namespace BBEdgeAgent
{
    public static class L
    {
        public static string Current { get; set; } = "en";
        private static bool Ko => Current == "ko";
        private static string T(string en, string ko) => Ko ? ko : en;

        // ── Login ───────────────────────────────────────────
        public static string AppTitle          => T("BB Edge Agent", "BB 엣지 에이전트");
        public static string LoginTitle        => T("Operator Login", "운영자 로그인");
        public static string PinLabel          => T("PIN", "PIN");
        public static string LoginBtn          => T("Login", "로그인");
        public static string InvalidPin        => T("Invalid PIN. Please try again.", "PIN이 잘못되었습니다. 다시 시도하세요.");
        public static string DefaultPinHint    => T("Default PIN: 1234", "기본 PIN: 1234");

        // ── Main ────────────────────────────────────────────
        public static string StartScan         => T("Start Scan", "스캔 시작");
        public static string StopScan          => T("Stop Scan", "스캔 중지");
        public static string ManualCheckIn     => T("Manual Check-in", "수동 출석");
        public static string SyncNow           => T("Sync Now", "동기화");
        public static string TagMapping        => T("Tag Mapping", "태그 맵핑");
        public static string SettingsMenu      => T("Settings", "설정");
        public static string Logout            => T("Logout", "로그아웃");
        public static string ScanPrompt        => T("Please scan your RFID tag", "RFID 태그를 스캔해 주세요");
        public static string ScanPromptSub     => T("Hold tag near the reader", "리더기에 도복 태그를 가까이 대세요");
        public static string CheckedIn         => T("Checked In!", "출석 완료!");
        public static string AlreadyCheckedIn  => T("Already Checked In", "이미 출석했습니다");
        public static string AlreadyTime       => T("min ago", "분 전");
        public static string TagNotFound       => T("Tag Not Registered", "등록되지 않은 태그");
        public static string TodayLabel        => T("Today", "오늘");
        public static string QueuedLabel       => T("Queued", "미동기");
        public static string RecentTitle       => T("Recent Attendance", "최근 출석");
        public static string StatusReader      => T("Reader", "리더기");
        public static string StatusSupabase    => T("Cloud", "클라우드");
        public static string Connected         => T("Connected", "연결됨");
        public static string Disconnected      => T("Disconnected", "연결 없음");
        public static string Online            => T("Online", "온라인");
        public static string Offline           => T("Offline", "오프라인");
        public static string Syncing           => T("Syncing...", "동기화 중...");

        // ── Settings ────────────────────────────────────────
        public static string SettingsTitle     => T("Settings", "설정");
        public static string TabReader         => T("RFID Reader", "RFID 리더기");
        public static string TabSupabase       => T("Supabase", "Supabase");
        public static string TabAttendance     => T("Attendance", "출석");
        public static string TabSecurity       => T("Security", "보안");
        public static string ReaderIpLabel     => T("Reader IP Address", "리더기 IP 주소");
        public static string ReaderPortLabel   => T("Port", "포트");
        public static string TestConn          => T("Test Connection", "연결 테스트");
        public static string ConnOk            => T("Connection successful!", "연결 성공!");
        public static string ConnFail          => T("Connection failed.", "연결 실패.");
        public static string SupabaseUrlLabel  => T("Supabase Project URL", "Supabase 프로젝트 URL");
        public static string AnonKeyLabel      => T("Anon / Service Key", "Anon / Service Key");
        public static string EdgeEmailLabel    => T("Edge Account Email", "엣지 계정 이메일");
        public static string EdgePassLabel     => T("Edge Account Password", "엣지 계정 비밀번호");
        public static string BranchIdLabel     => T("Branch ID (UUID)", "도장 ID (UUID)");
        public static string BranchNameLabel   => T("Branch Name", "도장 이름");
        public static string TestSupabase      => T("Test Supabase", "Supabase 테스트");
        public static string DebounceLabel     => T("Debounce Window (minutes)", "디바운스 시간 (분)");
        public static string AutoStartLabel    => T("Auto-start scanning on launch", "시작 시 자동 스캔");
        public static string NewPinLabel       => T("New PIN (4 digits)", "새 PIN (4자리)");
        public static string ConfirmPinLabel   => T("Confirm PIN", "PIN 확인");
        public static string ChangePin         => T("Change PIN", "PIN 변경");
        public static string PinMismatch       => T("PINs do not match.", "PIN이 일치하지 않습니다.");
        public static string PinChanged        => T("PIN changed successfully.", "PIN이 변경되었습니다.");
        public static string SaveSettings      => T("Save", "저장");
        public static string CancelBtn        => T("Cancel", "취소");

        // ── Tag Mapping ──────────────────────────────────────
        public static string TagMappingTitle   => T("Tag Mapping", "태그 맵핑 관리");
        public static string StudentList       => T("Students", "학생 목록");
        public static string SearchPlaceholder => T("Search student...", "학생 검색...");
        public static string TagsForStudent    => T("Tags for selected student", "선택 학생의 태그");
        public static string ScanModeBtn       => T("Scan New Tag", "새 태그 스캔");
        public static string ScanWaiting       => T("Waiting for scan... (reader active)", "스캔 대기 중... (리더기 활성화됨)");
        public static string RemoveTagBtn      => T("Remove Selected Tag", "선택 태그 삭제");
        public static string TagUid            => T("Tag UID", "태그 UID");
        public static string AssignedAt        => T("Assigned", "등록일");
        public static string TagNote           => T("Note", "메모");
        public static string AssignTagBtn      => T("Assign Tag", "태그 등록");
        public static string TagAssignOk       => T("Tag assigned successfully.", "태그가 등록되었습니다.");
        public static string TagRemoveOk       => T("Tag removed.", "태그가 제거되었습니다.");
        public static string SelectStudent     => T("Please select a student first.", "먼저 학생을 선택하세요.");
        public static string SelectTag         => T("Please select a tag to remove.", "제거할 태그를 선택하세요.");
        public static string ScanToAssign      => T("Tap 'Scan New Tag' then hold tag near reader.", "태그 스캔 버튼 후 리더기에 태그를 대세요.");
        public static string LoadingStudents   => T("Loading students...", "학생 목록 불러오는 중...");
        public static string NoStudents        => T("No students found.", "학생이 없습니다.");
        public static string RefreshList       => T("Refresh", "새로고침");
        public static string CloseBtn          => T("Close", "닫기");
        public static string Connecting        => T("Connecting...", "연결 중...");

        // ── Manual Check-in ──────────────────────────────────
        public static string ManualTitle       => T("Manual Check-in", "수동 출석");
        public static string SelectAndCheckIn  => T("Select student and confirm", "학생 선택 후 확인");
        public static string ConfirmCheckIn    => T("Confirm Check-in", "출석 확인");
        public static string ManualOk          => T("Manual check-in recorded.", "수동 출석이 기록되었습니다.");
    }
}
