I'll start by getting an overview of the codebase, then efficiently explore the key files.Now let me read the remaining modules and key forms:Now let me read the remaining key forms:Now I have sufficient information to produce a comprehensive requirements document. Let me compile everything.

---

# Migration Intent Document: Star Hotel VB6 — Hotel Room Reservation System

---

## Business Goals

1. **Room reservation management** for a small/mid-scale hotel: book rooms, check guests in/out, collect payments, and issue receipts.
2. **Real-time room availability** dashboard showing colour-coded status across up to 61 rooms across 4 building levels.
3. **Role-based access control** — four user groups (Administrator, Manager, Supervisor, Clerk) each with individually configurable per-module permissions.
4. **Shift-level financial accountability** — per-staff and all-staff booking/payment reports to close daily/weekly/monthly shifts.
5. **Printable receipts** — Temporary Receipt at booking time; Official Receipt at or after check-out.

---

## Business Rules

### Authentication & Security
- **BR-01** (`Form/frmUserLogin.frm`, `cmdOK_Click`): User ID is uppercased before lookup (`txtUserID_KeyPress` forces `UCase`).
- **BR-02** (`frmUserLogin.frm`): After **3 failed password attempts** (`LoginAttempts > 2`), the account is frozen and the application terminates (`End`).
- **BR-03** (`frmUserLogin.frm`): Frozen accounts (` Active = False`) cannot log in.
- **BR-04** (`frmUserLogin.frm`): Password is authenticated by appending the stored salt and running `GoldFishEncode(plaintext & salt)`, comparing to `UserPassword` in `UserData`.
- **BR-05** (`Module/modFunction.bas`, `UserAccessModule`): Module access is resolved per user group (1–4) against `ModuleAccess.Group1–Group4` boolean flags.
- **BR-06** (`frmUserLogin.frm`): Administrator (UserGroup = 1) is exempt from the login-attempt lockout counter increment.
- **BR-07** (`frmUserLogin.frm`): If `ChangePassword = True` in `UserData`, the user is redirected to password-change form before accessing the system.
- **BR-08** (`frmUserLogin.frm`, `lblCopyright_Click`): Hidden credential shortcut — clicking the copyright label populates `txtUserID = "ADMIN"` / `txtPassword = "admin"` (technical debt).

### Idle Session Timeout
- **BR-09** (`frmBooking.frm`, `tmrClock_Timer`): If `gintUserIdle > 0`, each second increments `intTick`; when `intTick > gintUserIdle` the idle dialog (`frmDialog`) is shown modally.
- **BR-10** (`frmUserLogin.frm`, `cmdOK_Click`): Idle values outside 0–3600 seconds are forced to 0 (no timeout).

### Room Lifecycle
- **BR-11** (`frmBooking.frm`, `ShowStatus`): Room statuses: `Open` → `Booked` → `Occupied` → `Housekeeping` → back to `Open`. `Maintenance` and `Void` are orthogonal states.
- **BR-12** (`frmBooking.frm`, `SaveBooking`): When saving a booking where status is `Open`, status transitions to `Booked`; `CreatedDate/By` are stamped.
- **BR-13** (`frmBooking.frm`, `Check_IN`): Check-In requires that payment equals SubTotal + Deposit (`IsPaid`): `dblPayment = dblSubTotal + dblDeposit`.
- **BR-14** (`frmBooking.frm`, `Check_OUT`): Check-Out also requires full payment. After 2:00 PM check-out, deposit refund is forced to `0.00` ("DEPOSIT NO REFUND").
- **BR-15** (`frmBooking.frm`, `Check_OUT`): After check-out, room status transitions to `Housekeeping`.
- **BR-16** (`frmDashboard.frm`, `cmdUnit_Click`): Rooms in `Maintenance` status cannot be booked.
- **BR-17** (`frmDashboard.frm`, `SetButtonProperties`): Rooms not set up in the `Room` table are invisible; clicking a missing room prompts admin to set it up.

### Pricing & Calculation
- **BR-18** (`frmBooking.frm`, `SumTotal`): `SubTotal = StayDuration (nights) × RoomPrice`.
- **BR-19** (`frmBooking.frm`, `SumTotal`): `TotalDue = SubTotal + Deposit`.
- **BR-20** (`frmBooking.frm`, default): Default deposit is hardcoded to `"20.00"` in `ResetFields`.
- **BR-21** (`frmBooking.frm`, `PrintReceipt TEMPORARY`): Temporary Receipt shows `SubTotal = Payment - Deposit`, `Total = Payment`.
- **BR-22** (`frmBooking.frm`, `PrintReceipt OFFICIAL`): Official Receipt shows `Total = Payment - Refund`.

### Checkout Date Calculation
- **BR-23** (`frmBooking.frm`, `cboStayDuration_Click`): If check-in time ≥ 12:00 PM, check-out date = `CheckIn + StayDuration days`; otherwise `CheckIn + (StayDuration - 1) days`, always at 12:00 PM.

### Booking ID Generation
- **BR-24** (`frmBooking.frm`, `CreateTempBookingID`): A temporary booking record (`Temp = TRUE`) is inserted to reserve a booking ID before the form is filled. The ID is formatted with 6 digits (`Format(ID,'100000')`).

### Alert/Blink Logic
- **BR-25** (`frmDashboard.frm`, `AlertBooking`): A `Booked` room blinks if `Now > GuestCheckIN` (overdue check-in); an `Occupied` room blinks if `Now > GuestCheckOUT` (overdue check-out). Blink preference is stored per user in `UserData.DashboardBlink`.

### Reporting
- **BR-26** (`frmReport.frm`, `PrintReport`): Report editing requires a separate developer password (`"expert"`) plus module-access `MOD_REPORT_EDIT`.
- **BR-27** (`modDatabase.bas`, `CreateSampleData`, Report table): Report queries are stored in `Report.ReportQuery` and `Report.SubQuery` fields; the app substitutes `$UserID$` and `$BookingID$` at runtime.
- **BR-28** (`frmReport.frm`, `LoadReports`): Reports are shown only if `UserAccessModule(ReportID + 11) = True` — i.e., module IDs 12–18 map to reports 1–7.
- **BR-29** (`frmReport.frm`): If the report query returns no rows, a `NullQuery` (company header only) is used as fallback.

### Access Control Defaults (from `CreateSampleData`)
- Group1 (Administrator): access to all modules.
- Group4 (Clerk): access to Dashboard, Booking, List Report, Find Customer, Shift Report for User (`modDatabase.bas`).
- Groups 2 (Manager) and 3 (Supervisor): no access by default (all flags `False`).

---

## Data Flows

### Startup & Configuration
1. `Main()` (`Module/modMain.bas`) → shows `frmSplash`.
2. `frmSplash.Timer1_Timer`: reads `Config.txt` (lines 0 and 1 = path and filename) via `ReadTextFile`; sets `gstrDatabasePath`.
3. If `Config.txt` absent or DB file missing → `frmDatabase` shown modally for path entry.
4. `ConnectDB()` (`modDatabase.bas`): opens ADO connection with `Microsoft.Jet.OLEDB.4.0` provider + password from `GenWord()`.
5. Version check: `DB_Version()` reads `Company.DatabaseVersion`; triggers `Update_Database` or `Migrate_Database` if behind current version (1.3).
6. `Set CrApplication = New CRAXDRT.Application` initialises Crystal Reports engine.
7. → `frmUserLogin.Show`.

### Login Flow
1. User enters User ID + Password → `frmUserLogin.cmdOK_Click`.
2. Query `UserData WHERE UserID = ?` → retrieve `UserPassword, Salt, Active, LoginAttempts, Idle, UserGroup, ChangePassword`.
3. Authenticate: `GoldFishEncode(enteredPassword & salt)` compared to stored `UserPassword`.
4. On success: global variables `gstrUserID`, `gintUserGroup`, `gintUserIdle` populated.
5. → Check `NeedChangePassword` → `frmUserChangePassword` if needed.
6. → Check `UserAccessModule(MOD_DASHBOARD)` → `frmDashboard.Show`.

### Booking Flow
1. User clicks room button on `frmDashboard` (Index 1–61).
2. `cmdUnit_Click` checks `UserAccessModule(MOD_BOOKING)`, `GetRoomStatus`, `RoomSetup`.
3. `frmBooking.SelectRoom(Index)` → calls `GetRoomDetails` → loads `Room` table row.
4. If `BookingID = 0`: `CreateTempBookingID` inserts minimal `Booking` row (`Temp=TRUE, Active=TRUE`).
5. User fills guest fields; `SumTotal` recalculates on duration change.
6. **Save**: `UPDATE Booking SET ...` with all fields; `Temp` set to `FALSE`; `UpdateRoomStatus("Booked", roomID, bookingID)`.
7. **Check-In**: validates `IsPaid`; `UPDATE Booking SET GuestCheckIN=...`; `UpdateRoomStatus("Occupied")`.
8. **Check-Out**: validates `IsPaid`, enforces refund rule; `UPDATE Booking SET GuestCheckOUT=..., Refund=...`; `UpdateRoomStatus("Housekeeping")`.
9. On `frmBooking.Unload`: `frmDashboard` refreshes all summary counts and button colours.

### Receipt Printing
- Both `PrintReceipt("TEMPORARY")` and `PrintReceipt("OFFICIAL")` (`frmBooking.frm`) set `gstrSQL`, `gstrReportFileName`, `gstrReportTitle` as globals, then `frmPrint.Show` reads those globals to feed Crystal Reports.

### Report Generation
1. `frmReport.LoadReports`: reads `Report` table, filters by `UserAccessModule(ReportID + 11)`.
2. User selects report → `PrintReport(lngReportID)`.
3. Date range applied per `DateType1` (Single/Weekly/Monthly/Yearly/Since Start/Range).
4. `$UserID$` and `$BookingID$` placeholders replaced at runtime.
5. For Weekly Graph: `UpdateWeekDayTable` updates all 7 rows in `WeeklyBooking` table to match the selected week's dates.
6. Global `gstrSQL` is set, then `frmPrint` is shown.

### Error Logging (dual-channel)
- `LogErrorText` (`modTextFile.bas`): appends to `<AppPath>\Error.txt` (flat file).
- `LogErrorDB` (`modFunction.bas`): inserts into `LogError` table with user, module, method, description.

---

## Integration Points

### Crystal Reports 8.5 (Local)
- **Runtime DLL**: `craxdrt.dll` (Crystal Reports 8.5 ActiveX Designer Runtime), `crviewer.dll` (viewer control) — referenced in `StarHotel.vbp`.
- **Report files**: `.rpt` files in `./Report/` directory (15 files including `Daily Booking Report.rpt`, `Weekly Booking Report.rpt`, `Monthly Booking Report.rpt`, `Official Receipt.rpt`, `Temporary Receipt.rpt`, `Booking Report.rpt`, `Booking Report by Staff.rpt`, `Customer Transaction History.rpt`, `Weekly Booking Graph.rpt`).
- **Integration pattern**: Global variable `gstrSQL` is set by the calling form, then `frmPrint.Show` passes the query to `CrApplication` (`CRAXDRT.Application`).

### Microsoft Access (Jet OLEDB)
- **Provider**: `Microsoft.Jet.OLEDB.4.0` (`modDatabase.bas`, `OpenDB`).
- **Database file**: `DemoData.mdb` (or custom `.mdb`) — MS Access 97/2000/2003 format.
- **Password**: Generated at runtime via `GenWord()` (hardcoded ASCII array decoding to `"Computerise"` — see `modCommon.bas`).
- **DDL extension**: `Microsoft ADO Ext. 2.8 for DDL and Security` (ADOX) used in `CreateData()` to create new `.mdb` files.

### Windows Shell
- `ShellExecute` declared in `modCommon.bas` — present but no call sites found in read files (potential dead code or used in unread forms).
- `P2smon.dll` present in root — referenced in commented-out code (`CrApplication.LogOffServer "P2smon.dll", "VMPC"`) in `frmSplash` migration code. Purpose unclear.

### File System
- `Config.txt` — two-line file: line 0 = database folder path, line 1 = database filename.
- `.bak` files — created automatically before database upgrades (`FileCopy` in `frmSplash.Timer1_Timer`).
- `Error.txt` — flat-file error log written by `LogErrorText`.
- `MigrateDB.log` — written during version 1.1→1.2 migration.

---

## Constraints

### Technology
- **Language**: Visual Basic 6.0 (Win32, single-threaded, `MaxNumberOfThreads=1` in `StarHotel.vbp`).
- **OS requirement**: Windows XP or above (`frmSplash.lblPlatform`).
- **Database**: Microsoft Access `.mdb` (Jet 4.0). No SQL Server, no ODBC, no standard SQL — uses Jet-specific syntax (e.g., `AUTOINCREMENT`, `YESNO`, `BIT`, `MEMO`, `#date#` literals).
- **Reporting**: Crystal Reports 8.5 only (`craxdrt.dll v8.5`, `crviewer.dll v8.0`). Not upgradeable without rewriting all `.rpt` files.
- **Controls**: `MSCOMCTL.OCX` (ListView, Toolbar, ImageList), `MSCOMCT2.OCX` (DTPicker — date/time pickers) — 32-bit OCX, not supported on 64-bit Windows without WOW64.
- **Encryption**: Custom `GoldFishEncode` (XOR-like binary stream cipher, `modEncryption.bas`) + random 3-byte hex salt — not a standard algorithm, not salted-hash (bcrypt/PBKDF2). Password storage is **not compliant** with modern security standards.

### Hardcoded Values
- Database password: `GenWord()` returns `"Computerise"` (encoded in `intArray` in `modCommon.bas`) — same for all installations.
- Developer/report-edit password: `"expert"` hardcoded in `frmReport.frm`.
- Default deposit: `"20.00"` in `frmBooking.ResetFields`.
- Maximum total-guest: 6 (`For i = 1 To 6` in `frmBooking.Form_Load`).
- Maximum stay duration: 10 nights (`For i = 1 To 10`).
- Maximum idle timeout: 3600 seconds (enforced in `cmdOK_Click`).
- Dashboard supports exactly 61 rooms (buttons index 1–61 hardcoded in `frmDashboard.frm`).
- Currency symbol seed data: `"MYR"` (`modDatabase.bas`, `CreateSampleData`).
- Company address seed data: `"9, Jalan Bintang, 50100 Kuala Lumpur, Malaysia"`.

### Compliance
- **PCI-DSS**: No payment gateway integration observed. No card data stored.
- **HIPAA/SOX**: Not applicable based on domain.
- **Data residency**: All data stored locally in `.mdb` file. No cloud/network storage.

---

## Key Workflows

### WF-1: Application Startup
`Sub Main` → `frmSplash` → Read `Config.txt` → Connect DB → Version-check → (migrate/update if needed) → Init Crystal Reports → `frmUserLogin`

### WF-2: Login
Enter UserID+Password → Validate active/frozen → Encrypt & compare → On success: check ChangePassword → check Dashboard access → `frmDashboard`

### WF-3: Room Booking (New)
Dashboard click room → `frmBooking.SelectRoom` → `CreateTempBookingID` → Fill guest/booking fields → Ctrl+S (`SaveBooking`) → status → `Booked` → Room button turns yellow

### WF-4: Check-In
Select Booked room → `frmBooking.Check_IN` → Verify `IsPaid` → UPDATE `GuestCheckIN` → `UpdateRoomStatus("Occupied")` → Room button turns red

### WF-5: Check-Out
Select Occupied room → `frmBooking.Check_OUT` → Verify `IsPaid` → Enforce 2PM refund rule → Confirm refund amount → UPDATE `GuestCheckOUT, Refund` → `UpdateRoomStatus("Housekeeping")` → Room button turns purple

### WF-6: Receipt Printing
After save/check-in: F11 → Temporary Receipt (Crystal Reports `Temporary Receipt.rpt`)
After check-out: F12 → Official Receipt (`Official Receipt.rpt`)

### WF-7: Report Generation
`frmDashboard` → F2 → `frmReport` → Select report → Select date → Preview → Crystal Reports window

### WF-8: Database First-Run Setup
`Config.txt` missing/DB not found → `frmDatabase` → User specifies path or selects Demo → `CreateData()` + `CreateDB()` + `CreateSampleData()` → `frmSplash`

---

## Domain Entities

| Entity | Table | Key Fields | States/Invariants |
|---|---|---|---|
| **Booking** | `Booking` | `ID (AUTOINCREMENT)`, `GuestName*`, `GuestPassport*`, `RoomID`, `BookingDate`, `GuestCheckIN`, `GuestCheckOUT`, `SubTotal`, `Deposit`, `Payment`, `Refund`, `Active`, `Temp` | `Temp=TRUE` = draft; `Active=FALSE` = voided; `Temp=FALSE AND Active=TRUE` = live booking |
| **Room** | `Room` | `ID (LONG, PK)`, `RoomShortName`, `RoomStatus`, `RoomType`, `RoomLocation`, `RoomPrice`, `Breakfast`, `BreakfastPrice`, `BookingID`, `Maintenance`, `Active` | Statuses: Open / Booked / Occupied / Housekeeping / Maintenance |
| **RoomType** | `RoomType` | `ID`, `TypeShortName`, `TypeLongName`, `Active` | Seed: SINGLE BED, DOUBLE BED, TWIN BED, DORM |
| **Company** | `Company` | `CompanyName`, `StreetAddress`, `ContactNo`, `SystemStartDate`, `DatabaseVersion`, `CurrencySymbol` | Single-row configuration table |
| **UserData** | `UserData` | `ID`, `UserGroup (FK)`, `UserID (unique)`, `UserPassword (GoldFish encrypted)`, `Salt`, `Idle`, `LoginAttempts`, `ChangePassword`, `DashboardBlink`, `Active` | |
| **UserGroup** | `UserGroup` | `GroupID`, `GroupName`, `SecurityLevel` | IDs 1–4: Administrator(99), Manager(98), Supervisor(20), Clerk(10) |
| **ModuleAccess** | `ModuleAccess` | `ModuleID`, `ModuleDesc1`, `ModuleType`, `Group1–Group4`, `Active` | 18 module IDs; Group flags per UserGroup |
| **Report** | `Report` | `ReportID`, `ReportName1`, `ReportFile`, `ReportQuery (MEMO)`, `SubQuery`, `NullQuery`, `DateField1`, `DateType1`, `Active` | Queries stored as raw SQL strings with `$UserID$`/`$BookingID$` placeholders |
| **WeeklyBooking** | `WeeklyBooking` | `ID (1–7)`, `RoomPrice`, `SubTotal`, `Deposit`, `Payment`, `Refund`, `CreatedDate` | Pre-populated 7-row table overwritten each week for chart data |
| **LogBooking** | `LogBooking` | Mirror of `Booking` + `BookingID` FK | Audit log — populated during DB migration, not actively written in v1.3 |
| **LogRoom** | `LogRoom` | Mirror of `Room` + `BookingID` FK | Audit log — same as above |
| **LogError** | `LogError` | `LogDateTime`, `LogErrorNum`, `LogErrorDescription`, `LogUserName`, `LogModule`, `LogMethod`, `LogType` | Runtime error audit trail |

---

## Assumptions

- **(Inference)** The system is intended for a single-property hotel with a fixed layout of up to 61 rooms across 4 levels. The dashboard button array is static and hard-coded; the system cannot support multi-property or dynamic room configurations without significant redesign.
- **(Inference)** The `WeeklyBooking` table (7 static rows) serves as a workaround for Crystal Reports inability to produce graphs with zero-data days; it is not a real transaction table.
- **(Inference)** The `LogBooking` and `LogRoom` tables were originally populated during the v1.1→v1.2 database migration as a one-time historical copy. No code in v1.3 actively writes to them for ongoing audit purposes.
- **(Inference)** `P2smon.dll` (Crystal Reports offline activation) is present but its call (`CrApplication.LogOffServer`) is commented out, suggesting it was used for licensing in earlier versions.
- The `Void` booking feature (`VoidBooking` function exists in `frmBooking.frm`) is **explicitly disabled** in the UI: `'Case "VOID"` is commented out in `tbrMenu_ButtonClick`. `frmAdmin` (admin re-auth dialog) exists to authorize voids but is currently unreachable.

---

## Open Questions

1. **`LogBooking` / `LogRoom` write path**: Is there a business requirement for ongoing audit logging of booking changes? The tables exist and were populated during migration, but no active INSERT calls were found in v1.3 code paths for new bookings.
2. **`WeeklyBooking` pre-population**: How are the 7 baseline rows created? `CreateDB` creates the table but `CreateSampleData` does not insert rows. The weekly graph may be broken on a fresh install. *Needs verification.*
3. **Void Booking**: The `VoidBooking` sub exists and `frmAdmin` was built for it, but the UI trigger is commented out. Was this feature intentionally removed or deferred? Should it be included in modernisation?
4. **Currency/multi-currency**: `Company.CurrencySymbol` is stored (`"MYR"`) and labels say `"(MYR)"` hardcoded in `frmBooking.frm`. The symbol is not used dynamically in the UI labels. Is multi-currency a future requirement?
5. **Breakfast pricing**: `lblBreakfast` and `lblBreakfastPrice` are hidden (`Visible = False`) in `frmBooking.frm`. The data is stored in `Room` and `Booking` tables. Is breakfast billing currently active or intentionally suppressed?
6. **`P2smon.dll`**: What is its current purpose? The only reference is commented-out code. Is it required for Crystal Reports licensing?
7. **`ShellExecute` usage**: Declared in `modCommon.bas` but no active call site found in read files. Is it used in unread forms (e.g., `frmFindCustomer`, `frmRoomMaintain`)?
8. **Report file security**: Report SQL queries are stored in plain text in the `Report` table (readable by any DB viewer). Is query tampering a concern in the target environment?
9. **Salt length cap**: `GenSalt` caps salt at max 3 hex bytes (`StringLen \ 2`, max 6): `"Hex((Rnd * 64) Mod 100)"`. This produces only ~16 bits of entropy — far below modern standards. Does the modernisation require proper password hashing (bcrypt/Argon2)?
10. **Max guests/duration**: Total guests hard-capped at 6 and stay duration at 10 nights. Are these valid business limits or UI oversights?