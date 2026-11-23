# BB Edge Agent — 프로젝트 가이드

## 프로젝트 개요
BB Taekwondo 도장 현장(미니PC)에서 실행되는 **RFID 출석 에이전트**.
UHF RFID 리더기로 도복 태그를 인식해 출석을 자동 기록하고, Supabase와 동기화한다.
bb-ai-hub 웹앱과 **Supabase API를 경계**로 분리된 독립 프로젝트.

## 기술 스택
- 언어/런타임: **C# .NET Framework 4.8 + WPF**
- 아키텍처: **Core(로직) + Wpf(UI) 분리, MVVM 패턴**
- 빌드: `dotnet build BBEdgeAgent.sln /p:Platform=x64` (IDE: Visual Studio 2022)
- 빌드 출력: `Wpf\bin\x64\Debug\net48\BBEdgeAgent.exe`
- 패키지: Newtonsoft.Json, System.Data.SQLite
- RFID SDK: ADRcpLib.dll + ADSioLib.dll (WiFi TCP, IP `192.168.2.116`, Port `49152`)
- 언어 지원: 한국어 / 영어 (프로세스 재시작으로 전환, `AppConfig.Language`)

## 프로젝트 구조
```
bb-edge-agent/
├── BBEdgeAgent.sln
├── Core/                         ← UI-무관 로직 라이브러리 (net48)
│   ├── BBEdgeAgent.Core.csproj
│   └── Services/RfidService.cs   ← System.Timers.Timer (WinForms 의존 없음)
├── Wpf/                          ← WPF UI 프로젝트 (net48)
│   ├── BBEdgeAgent.Wpf.csproj
│   ├── App.xaml / App.xaml.cs
│   ├── Resources/
│   │   ├── Theme.xaml            ← 전체 다크테마 ResourceDictionary
│   │   └── bb.png
│   ├── ViewModels/
│   │   ├── ViewModelBase.cs, RelayCommand.cs (RelayCommand / AsyncRelayCommand / RelayCommand<T>)
│   │   ├── MainViewModel.cs, LoginViewModel.cs
│   │   ├── SettingsViewModel.cs, TagMappingViewModel.cs
│   │   ├── ManualCheckInViewModel.cs, AttendanceLogItem.cs
│   │   ├── ClassViewViewModel.cs  ← ClassSlot, ClassStudentRow
│   │   └── RfidLogViewModel.cs
│   └── Views/
│       ├── LoginWindow, MainWindow
│       ├── SettingsWindow, TagMappingWindow, ManualCheckInWindow
│       ├── ClassViewWindow        ← 오늘의 클래스 + 학생 출석 + 수동 체크인
│       └── RfidLogWindow         ← RFID 스캔 로그 조회
├── AppConfig.cs, Lang.cs         ← 루트 공유 파일 (Core가 링크로 참조)
├── Models/Student.cs             ← Student, RfidTag, AttendanceRecord, RfidLogEntry
└── Services/
    ├── AttendanceService.cs      ← 디바운스 + 출석 기록 + 로그 + 큐 동기화
    ├── LocalDbService.cs         ← SQLite 로컬 캐시
    ├── RfidService.cs            ← WinForms용 (Core는 자체 버전 사용)
    └── SupabaseService.cs        ← Supabase REST API 전체
```

> 루트의 `Forms/`, `BBEdgeAgent.csproj` (구 WinForms)는 참조하지 않음. 빌드는 `BBEdgeAgent.sln`으로.

---

## 구현된 기능 (2026-06-16 기준)

### Core 서비스
| 파일 | 기능 |
|------|------|
| `AppConfig.cs` | JSON 설정 로드/저장 (`%APPDATA%\BBEdgeAgent\config.json`), bb-ai-hub `.env` 자동 읽기, PIN SHA-256 |
| `Lang.cs` | 한국어/영어 전환 (`L.ScanPrompt` 등 정적 속성) |
| `Models/Student.cs` | `Student` (ClassDays 포함), `RfidTag`, `AttendanceRecord`, `RfidLogEntry` |
| `Services/RfidService.cs` (Core) | WiFi TCP, 연속 인벤토리, EPC 파싱, `TagRead` 이벤트 (`System.Timers.Timer`) |
| `Services/SupabaseService.cs` | Supabase REST API: 학생/태그/출석/RFID로그 CRUD, service role key JWT 모드 |
| `Services/LocalDbService.cs` | SQLite: 출석 큐, 학생 캐시, RFID 캐시 |
| `Services/AttendanceService.cs` | 디바운스 → 출석 기록(온/오프라인) → RFID 로그 기록 (fire-and-forget) |

### WPF 화면
| 화면 | 기능 |
|------|------|
| `LoginWindow` | 4칸 PIN 박스(ATM 스타일), 자동 포커스, 언어 전환. Grid 2행(언어 버튼 항상 표시), Height=680 |
| `MainWindow` | Grid 2-Column(콘텐츠+사이드바), 스캔 배경 300ms ColorAnimation, 최근 출석 ListView |
| `SettingsWindow` | 4탭(RFID/Supabase/Attendance/Security). TextBox Height=42, Loaded 이벤트로 스크롤 초기화. 저장 시 Supabase 자동 재연결 + 학생/태그 캐시 갱신 |
| `TagMappingWindow` | 좌측 학생 ListBox + 우측 태그 ListView, 실시간 RFID 스캔-등록. 검색창 Height=42 |
| `ManualCheckInWindow` | 학생 검색 + 확인, MainViewModel.ShowResult()로 결과 전달 |
| `ClassViewWindow` | 좌: 오늘의 클래스 목록 (class_days 요일 필터), 우: 학생 목록 (출석 배지 or Check In 버튼). 수동 체크인 → Supabase attendance INSERT |
| `RfidLogWindow` | RFID 스캔 로그 조회 (Today/7d/30d), 30분 그룹핑 count 표시 |

### MainWindow 사이드바 버튼 순서
```
Stop/Start Scan  (GoldButton / ErrorBrush)
─────────────────
Class View
Tag Mapping
Manual Check-in
Sync Now
RFID Log
Settings
─────────────────
한국어/English  |  Logout
```

---

## 출결 처리 흐름
```
RFID 태그 스캔 → EPC 추출 (Core/RfidService, System.Timers.Timer 600ms)
→ AttendanceService.ProcessTagAsync(epc)
→ [1] 메모리 디바운스 (같은 태그 N분 이내 → AlreadyCheckedIn + RFID 로그)
→ [2] SQLite rfid_cache에서 student_id 조회 (없으면 → TagNotFound + RFID 로그)
→ [3] 학생 디바운스 (SQLite 마지막 출석 → AlreadyCheckedIn + RFID 로그)
→ [4] 온라인? → Supabase attendance INSERT + RFID 로그(success)
         오프라인? → SQLite attendance_queue + RFID 로그(success)
→ MainViewModel → Dispatcher.BeginInvoke → ShowResult() → ColorAnimation
→ 4초 후 DispatcherTimer 대기화면 복귀 / 30초마다 SyncQueue
```

---

## SupabaseService 주요 메서드
| 메서드 | 설명 |
|--------|------|
| `SignInAsync()` | EdgeEmail 비워두면 service role key를 JWT로 직접 사용 |
| `GetStudentsAsync()` | `class_days` 포함 조회. 요일 필터는 ClassViewViewModel에서 처리 |
| `GetTodayAttendanceAsync()` | 오늘 UTC 범위, `DateTimeOffset.TryParse` + `.LocalDateTime` (타임존 안전) |
| `LogRfidScanAsync(tagUid, result, ...)` | 30분 이내 동일 태그 → scan_count 증가 PATCH, 신규면 INSERT |
| `GetRfidLogsAsync(fromUtc, toUtc)` | rfid_scan_logs 조회 (최대 500건, DESC) |
| `PostAttendanceAsync(rec)` | attendance 테이블 INSERT |

---

## 타임존 주의
- DB 저장: `DateTime.UtcNow` (UTC)
- DB 조회 후 표시: `DateTimeOffset.TryParse` + `.LocalDateTime` 사용 (로컬 시간 변환)
- `DateTime.TryParse`는 timezone offset 처리 불완전 → **반드시 `DateTimeOffset` 사용**

---

## LocalDbService SQLite 스키마
경로: `%APPDATA%\BBEdgeAgent\attendance.db`
- `attendance_queue`: id, student_id, branch_id, attended_at, source, rfid_tag_id, rfid_tag_uid, synced
- `cached_students`: id, first_name, last_name, belt_name, belt_code, class_type, class_days_json
- `rfid_cache`: tag_uid, student_id, cached_at

---

## Theme.xaml 다크 테마
| 키 | 색 | 용도 |
|----|----|------|
| BgDark | `#121A2A` | 기본 배경 |
| BgCard | `#1E2A3E` | 카드/헤더 |
| BgSidebar | `#18263C` | 사이드바 |
| AccentGold | `#F59E0B` | 강조/버튼/배지 |
| SuccessBrush | `#10B981` | 출석 성공 |
| ErrorBrush | `#EF4444` | 오류 / Stop Scan |

스캔 배경: Idle `#1E2A3E` → Success `#0A2820` → Already `#2D2500` → NotFound `#2D0A0A`

커스텀 스타일: `GoldButton`, `GhostButton`, `NavButton`, `DangerButton`, `PinBox`,
`ComboBox` + `ComboBoxItem` (완전한 다크 ControlTemplate), `GridViewColumnHeader` (다크)

---

## 배포
```
Wpf\bin\x64\Debug\net48\ 폴더 전체 → 미니PC에 복사 후 BBEdgeAgent.exe 실행
최초 설정:
  Settings → Supabase 탭 → URL + Service Role Key 입력 → Load Branches → Branch 선택 → Save
  Settings → RFID Reader 탭 → IP 192.168.2.116, Port 49152 → Test Connection → Save
이후: 앱 시작 시 자동 연결 + 스캔 시작 (AutoStart 옵션)
```

---

## Supabase 연동 규칙
- **service role key 필수**: students, attendance, rfid_scan_logs 등 RLS 차단
- **branches만 예외**: `008_branches_public_read.sql` 적용 시 anon key로도 읽기 가능
- EdgeEmail 비워두면 service role key를 Bearer JWT로 직접 사용 (권장)

## 작업 규칙 (AI에게)
- Supabase 시간 처리: `DateTimeOffset.TryParse` + `.LocalDateTime` 필수
- WPF TextBox/PasswordBox: Height 최소 42px (Padding 12,8 + FontSize 13 수용)
- ComboBox: Theme.xaml에 완전한 ControlTemplate 있음, 추가 스타일 불필요
- 빌드: `dotnet build BBEdgeAgent.sln /p:Platform=x64 /p:Configuration=Debug`
- 앱 실행 중 빌드 → DLL 잠금 오류 → 앱 닫고 재빌드
- 한국어로 소통한다.
