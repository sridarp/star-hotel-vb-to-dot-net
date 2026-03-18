I'll use LSP tools to get precise method signatures on the key modules before generating BDD artifacts.I now have everything needed. Let me generate the comprehensive BDD artifacts.

---

# BDD Behavior Specifications — Star Hotel VB6
## Hotel Room Reservation System — Migration Behavior Lock-in

---

## Feature Index

| # | Feature | Key Business Rules |
|---|---------|-------------------|
| F1 | Application Startup & Database Connection | BR-02 (Config.txt), DB version |
| F2 | User Authentication & Login | BR-01–BR-08 |
| F3 | Idle Session Timeout | BR-09, BR-10 |
| F4 | Dashboard & Room Status Display | BR-11, BR-16, BR-17, BR-25 |
| F5 | Room Booking | BR-12, BR-18–BR-20, BR-23, BR-24 |
| F6 | Guest Check-In | BR-13 |
| F7 | Guest Check-Out | BR-14, BR-15 |
| F8 | Receipt Printing | BR-21, BR-22 |
| F9 | Role-Based Access Control | BR-05, BR-28, BR-29 |
| F10 | Reporting | BR-26, BR-27, BR-28 |
| F11 | User & Account Management | BR-03, BR-04, BR-07 |
| F12 | Void / Unvoid Booking | `VoidBooking` logic |

---

## Feature F1 — Application Startup & Database Connection

**Source:** `Module/modMain.bas → Main()`, `frmSplash.Timer1_Timer`, `modDatabase.bas → ConnectDB()`, `DB_Version()`

```gherkin
Feature: Application Startup and Database Initialization
  As the hotel system
  I need to locate and connect to the Access database at startup
  So that all hotel operations have a valid data store

  Background:
    Given the application executable is launched

  # --- Happy Path ---
  Scenario: Successful startup with valid Config.txt and database file
    # Trace: Startup flow — frmSplash reads Config.txt lines 0 and 1
    Given a valid "Config.txt" exists with database path on line 0 and filename on line 1
    And the referenced .mdb file exists at that path
    And the database version equals the current application version "1.3"
    When the splash screen timer fires
    Then an ADO connection is opened with provider "Microsoft.Jet.OLEDB.4.0"
    And the Crystal Reports engine "CRAXDRT.Application" is initialised
    And "frmUserLogin" is shown

  # --- Edge Case ---
  Scenario: Database version is behind current version — triggers migration
    # Trace: DB_Version() returns value < 1.3 → Update_Database / Migrate_Database
    Given "Config.txt" and the .mdb file are both present
    And the value of "Company.DatabaseVersion" is less than "1.3"
    When the splash screen connects to the database
    Then the database migration routine is executed
    And "frmUserLogin" is shown after migration completes

  # --- Error Path ---
  Scenario: Missing Config.txt causes database path prompt
    # Trace: frmSplash — absent Config.txt → frmDatabase shown modally
    Given "Config.txt" does not exist
    When the splash screen timer fires
    Then "frmDatabase" is displayed modally
    And the user is prompted to enter the database file path

  # --- Error Path ---
  Scenario: Config.txt present but database file is missing
    # Trace: frmSplash — DB file not found → frmDatabase shown modally
    Given "Config.txt" exists with a path that points to a non-existent .mdb file
    When the splash screen timer fires
    Then "frmDatabase" is displayed modally
    And no ADO connection attempt is made
```

---

## Feature F2 — User Authentication & Login

**Source:** `Form/frmUserLogin.frm → cmdOK_Click`, `GoldFishEncode()`, `modFunction.bas → NeedChangePassword()`

```gherkin
Feature: User Authentication and Login
  As a hotel staff member
  I need to authenticate with my User ID and password
  So that I can access the system according to my role

  # --- Happy Path ---
  Scenario: Successful login with valid credentials
    # Trace: BR-01, BR-04 — UserID uppercased; password hash matches
    Given a user account "CLERK1" exists with UserGroup 4, Active = True, LoginAttempts = 0
    And the stored password hash equals GoldFishEncode("password123" + stored_salt)
    When the user enters User ID "clerk1" and password "password123"
    And clicks OK
    Then the User ID is uppercased to "CLERK1" before lookup
    And global variables gstrUserID = "CLERK1" and gintUserGroup = 4 are set
    And "frmDashboard" is shown

  Scenario: Login redirects to password change when flag is set
    # Trace: BR-07 — ChangePassword = True triggers frmUserChangePassword
    Given a user account "SUPER1" exists with ChangePassword = True
    And the password hash is valid
    When the user logs in successfully
    Then "frmUserChangePassword" is shown modally before the dashboard

  # --- Edge Case ---
  Scenario: Administrator account is exempt from lockout counter increment
    # Trace: BR-06 — UserGroup = 1 skips LoginAttempts increment
    Given an Administrator account "ADMIN" with UserGroup = 1
    When "ADMIN" enters an incorrect password
    Then the LoginAttempts counter for "ADMIN" is NOT incremented
    And the login form remains open

  Scenario: Idle timeout value outside 0–3600 is normalised to 0
    # Trace: BR-10 — out-of-range Idle forced to 0
    Given a user account with Idle = 9999 stored in UserData
    When the user logs in successfully
    Then gintUserIdle is set to 0
    And no automatic session timeout will trigger

  Scenario: Hidden admin shortcut populates credentials
    # Trace: BR-08 — lblCopyright_Click populates ADMIN/admin
    Given the login form is displayed
    When the copyright label is clicked
    Then txtUserID is populated with "ADMIN"
    And txtPassword is populated with "admin"

  # --- Error Paths ---
  Scenario: Third failed login attempt freezes non-admin account
    # Trace: BR-02 — LoginAttempts > 2 → Active = False, application terminates
    Given a non-admin user account "CLERK2" with Active = True and LoginAttempts = 2
    When the user enters an incorrect password
    Then LoginAttempts is incremented to 3
    And the account Active flag is set to False
    And the application terminates

  Scenario: Frozen account is rejected at login
    # Trace: BR-03 — Active = False prevents login
    Given a user account "FROZEN1" with Active = False
    When the user attempts to log in with correct credentials
    Then login is denied
    And an appropriate message is shown

  Scenario: Login fails for unknown User ID
    # Trace: BR-04 — no row returned from UserData → auth fails
    Given no user with ID "GHOST" exists in UserData
    When the user enters User ID "GHOST" and any password
    And clicks OK
    Then login is denied
    And the login form remains open
```

---

## Feature F3 — Idle Session Timeout

**Source:** `frmBooking.frm → tmrClock_Timer`, `Form_Activate`, `Form_Deactivate`

```gherkin
Feature: Idle Session Timeout
  As a hotel administrator
  I need inactive sessions to prompt users after a configured idle period
  So that unauthorised use of unattended terminals is prevented

  # --- Happy Path ---
  Scenario: Session timeout dialog appears after idle period elapses
    # Trace: BR-09 — intTick > gintUserIdle → frmDialog shown
    Given gintUserIdle is set to 300 (5 minutes)
    And the booking form is active
    When 301 timer ticks elapse without user interaction
    Then the idle timeout dialog "frmDialog" is shown modally
    And the timer is disabled before the dialog appears

  # --- Edge Case ---
  Scenario: No timeout occurs when idle limit is 0
    # Trace: BR-10 — gintUserIdle = 0 disables timeout
    Given gintUserIdle is set to 0
    And the booking form is active
    When any number of timer ticks elapse
    Then "frmDialog" is never shown

  Scenario: Timer resets to 0 when booking form regains focus
    # Trace: frmBooking.Form_Activate — intTick = 0
    Given the booking form was inactive with intTick = 250 and gintUserIdle = 300
    When the booking form is activated
    Then intTick is reset to 0

  # --- Error Path ---
  Scenario: Timer is stopped when booking form loses focus
    # Trace: frmBooking.Form_Deactivate — tmrClock.Enabled = False
    Given the booking form's timer is running
    When the booking form is deactivated (loses focus)
    Then tmrClock is disabled
    And intTick does not continue incrementing
```

---

## Feature F4 — Dashboard & Room Status Display

**Source:** `frmDashboard.frm → SetButtonProperties`, `cmdUnit_Click`, `AlertBooking`

```gherkin
Feature: Dashboard Room Status Display and Navigation
  As a logged-in hotel staff member
  I need to see real-time room status on the dashboard
  So that I can identify available and occupied rooms quickly

  # --- Happy Path ---
  Scenario: Colour-coded room buttons reflect current status
    # Trace: BR-11 — 6-state lifecycle; BR-25 dashboard blink
    Given the dashboard is loaded for a user with dashboard access
    When room statuses are retrieved from the Room table
    Then each visible room button displays the correct status colour:
      | Status       | Expected Colour |
      | Open         | Green           |
      | Booked       | Yellow          |
      | Occupied     | Red             |
      | Housekeeping | Purple          |
      | Maintenance  | Blue            |
      | Void         | Gray            |

  Scenario: Overdue booking causes button to blink
    # Trace: BR-25 — Now > GuestCheckIN for Booked; Now > GuestCheckOUT for Occupied
    Given a room is in status "Booked"
    And the scheduled GuestCheckIN time is in the past
    And the user's DashboardBlink preference is True
    When the AlertBooking timer fires
    Then the room button blinks to indicate overdue check-in

  Scenario: Clicking a Booked room opens the booking form
    # Trace: Booking flow — cmdUnit_Click → frmBooking.SelectRoom
    Given room 5 has status "Booked" and the user has MOD_BOOKING access
    When the user clicks room button 5
    Then "frmBooking" opens with the existing booking data for room 5 pre-populated

  # --- Edge Cases ---
  Scenario: Room not configured in Room table is invisible
    # Trace: BR-17 — SetButtonProperties hides unconfigured rooms
    Given room index 42 has no corresponding row in the Room table
    When the dashboard loads
    Then button 42 is invisible

  Scenario: Clicking an unconfigured room slot prompts admin setup
    # Trace: BR-17 — clicking missing room prompts setup
    Given room index 42 has no row in the Room table
    And the current user has Administrator access
    When the user clicks button 42
    Then a prompt appears directing the administrator to set up the room

  # --- Error Path ---
  Scenario: Maintenance room cannot be booked
    # Trace: BR-16 — cmdUnit_Click blocks booking for Maintenance status
    Given room 10 has status "Maintenance"
    When the user clicks room button 10
    Then the booking form does NOT open
    And an informational message is shown indicating the room is under maintenance
```

---

## Feature F5 — Room Booking

**Source:** `frmBooking.frm → SaveBooking`, `SumTotal`, `CreateTempBookingID`, `cboStayDuration_Click`

```gherkin
Feature: Room Booking Creation and Pricing Calculation
  As a hotel clerk
  I need to create bookings with correct pricing and dates
  So that guest reservations are accurately recorded

  Background:
    Given the user is logged in with MOD_BOOKING access
    And an Open room is selected from the dashboard

  # --- Happy Path ---
  Scenario: Saving a new booking transitions room to Booked
    # Trace: BR-12 — SaveBooking; status Open → Booked; CreatedDate/By stamped
    Given the booking form opens for an Open room
    And the user enters:
      | Field          | Value            |
      | GuestName      | John Smith       |
      | GuestPassport  | A1234567         |
      | TotalGuest     | 2                |
      | StayDuration   | 3                |
    When the user clicks SAVE and confirms the dialog
    Then a Booking record is written with Temp = False
    And the Room status is updated to "Booked"
    And CreatedDate and CreatedBy are stamped with current datetime and UserID
    And the Booking ID is formatted as 6 digits (e.g. "100003")

  Scenario: SubTotal and TotalDue are correctly calculated
    # Trace: BR-18, BR-19 — SubTotal = nights × price; TotalDue = SubTotal + Deposit
    Given a room with RoomPrice = 150.00
    And default deposit = 20.00
    When the user selects StayDuration = 3
    Then lblSubTotal displays "450.00"
    And lblTotalDue displays "470.00"

  Scenario: Temporary booking ID is reserved before form is filled
    # Trace: BR-24 — CreateTempBookingID inserts Temp=TRUE record
    Given no existing Temp=TRUE booking exists for the current user
    When the booking form opens for a new (Open) room
    Then a Booking row with Temp = True and Active = True is inserted
    And the displayed Booking ID is formatted as 6 digits
    And Check-In and Check-Out toolbar buttons are disabled

  # --- Edge Cases ---
  Scenario: Check-out date calculation for check-in before noon
    # Trace: BR-23 — check-in before 12:00 PM → checkout = CheckIn + (days-1)
    Given the user sets CheckInTime = "09:00 AM" and StayDuration = 2
    When StayDuration is selected
    Then dtpCheckOutDate = CheckInDate + 1 day (StayDuration - 1)
    And dtpCheckOutTime = "12:00 PM"

  Scenario: Check-out date calculation for check-in at or after noon
    # Trace: BR-23 — check-in ≥ 12:00 PM → checkout = CheckIn + days
    Given the user sets CheckInTime = "02:00 PM" and StayDuration = 2
    When StayDuration is selected
    Then dtpCheckOutDate = CheckInDate + 2 days
    And dtpCheckOutTime = "12:00 PM"

  Scenario: Deposit defaults to 20.00 for every new booking
    # Trace: BR-20 — ResetFields hardcodes txtDeposit = "20.00"
    When the booking form is opened or reset
    Then txtDeposit displays "20.00"

  Scenario: Parameterised pricing boundary values
    # Trace: BR-18 — SubTotal = nights × RoomPrice
    Given a room with the following prices:
      | RoomPrice | StayDuration | ExpectedSubTotal | DefaultDeposit | ExpectedTotalDue |
      | 0.00      | 1            | 0.00             | 20.00          | 20.00            |
      | 100.00    | 1            | 100.00           | 20.00          | 120.00           |
      | 100.00    | 10           | 1000.00          | 20.00          | 1020.00          |
      | 999.99    | 10           | 9999.90          | 20.00          | 10019.90         |
    When SumTotal is called
    Then lblSubTotal and lblTotalDue display the expected values

  # --- Error Paths ---
  Scenario: Save is blocked when Guest Name is blank
    # Trace: SaveBooking — Trim(txtGuestName) = "" → validation error
    Given the booking form is open
    And txtGuestName is empty
    When the user clicks SAVE
    Then a validation message "Please key in Guest Name" is shown
    And the booking is NOT saved

  Scenario: Save is blocked when Guest Passport/IC is blank
    # Trace: SaveBooking — Trim(txtGuestPassport) = "" → validation error
    Given txtGuestPassport is empty and all other fields are valid
    When the user clicks SAVE
    Then a validation message "Please key in Guest Passport/IC No" is shown

  Scenario: Save is blocked when TotalGuest is not selected
    # Trace: SaveBooking — cboTotalGuest.ListIndex < 0
    Given cboTotalGuest has no selection
    When the user clicks SAVE
    Then a validation message "Please select Total Guest" is shown

  Scenario: Save is blocked when StayDuration is not selected
    # Trace: SaveBooking — cboStayDuration.ListIndex < 0
    Given cboStayDuration has no selection
    When the user clicks SAVE
    Then a validation message "Please select Stay Duration" is shown
```

---

## Feature F6 — Guest Check-In

**Source:** `frmBooking.frm → Check_IN`, `IsPaid()`

```gherkin
Feature: Guest Check-In
  As a hotel clerk
  I need to record guest check-in only when full payment is confirmed
  So that room occupancy is accurately tracked

  Background:
    Given the user has MOD_BOOKING access
    And a Booked room is selected with a saved BookingID

  # --- Happy Path ---
  Scenario: Successful check-in when full payment is received
    # Trace: BR-13 — IsPaid: Payment = SubTotal + Deposit
    Given Booking ID 100005 has SubTotal = 300.00, Deposit = 20.00, Payment = 320.00
    When the user clicks Check-IN and confirms the dialog
    Then GuestCheckIN timestamp is updated to the selected check-in date/time
    And Room status transitions to "Occupied"
    And the form reloads with updated values via PopulateValues

  # --- Edge Case ---
  Scenario: IsPaid evaluates true only when Payment equals SubTotal plus Deposit exactly
    # Trace: BR-13 — strict equality check
    Given the following payment scenarios:
      | SubTotal | Deposit | Payment | IsPaid |
      | 300.00   | 20.00   | 320.00  | True   |
      | 300.00   | 20.00   | 319.99  | False  |
      | 300.00   | 20.00   | 320.01  | False  |
      | 0.00     | 20.00   | 20.00   | True   |
    When IsPaid is evaluated for each booking
    Then it returns the expected result

  # --- Error Paths ---
  Scenario: Check-in blocked when payment is insufficient
    # Trace: BR-13 — IsPaid = False → block with message
    Given Booking ID 100006 has SubTotal = 300.00, Deposit = 20.00, Payment = 200.00
    When the user attempts Check-IN
    Then Check-In is blocked
    And the message "Please make payment first!" is shown

  Scenario: Check-in blocked when BookingID is 0 (booking not saved)
    # Trace: Check_IN — lngBookingID = 0 guard
    Given the booking form is open but the booking has not been saved (lngBookingID = 0)
    When the user attempts Check-IN
    Then Check-In is blocked
    And the message "Booking has not yet saved. Please save it first!" is shown
```

---

## Feature F7 — Guest Check-Out

**Source:** `frmBooking.frm → Check_OUT`, `IsPaid()`

```gherkin
Feature: Guest Check-Out
  As a hotel clerk
  I need to process check-out and apply the correct deposit refund policy
  So that guests are charged accurately and rooms are queued for housekeeping

  Background:
    Given the user has MOD_BOOKING access
    And an Occupied room with a valid BookingID is selected
    And Payment = SubTotal + Deposit (IsPaid = True)

  # --- Happy Path ---
  Scenario: Check-out before 2:00 PM allows deposit refund
    # Trace: BR-14 — checkout time < 14:00 → refund dialog shown
    Given the checkout time is set to "11:00 AM"
    And the deposit is 20.00
    When the user clicks Check-OUT
    Then a dialog asks whether to fully refund the deposit
    And if Yes is selected, txtRefund is set to the deposit amount "20.00"
    And GuestCheckOUT timestamp is saved
    And Room status transitions to "Housekeeping"

  Scenario: Check-out after 2:00 PM forces zero refund
    # Trace: BR-14 — "DEPOSIT NO REFUND" rule; checkout ≥ 14:00
    Given the checkout time is set to "02:00 PM"
    When the user clicks Check-OUT
    Then a "DEPOSIT NO REFUND" message is shown
    And txtRefund is automatically set to "0.00"
    And txtRefund is disabled (non-editable)
    And Room status transitions to "Housekeeping" after confirmation

  Scenario: Room status transitions to Housekeeping on check-out
    # Trace: BR-15 — UpdateRoomStatus("Housekeeping")
    When the user completes Check-OUT
    Then UpdateRoomStatus is called with status "Housekeeping" and the room ID
    And the dashboard button for this room shows the Housekeeping (purple) colour

  # --- Edge Case ---
  Scenario: Refund boundary — check-out exactly at 2:00 PM triggers no-refund policy
    # Trace: BR-14 — TimeValue >= "2:00 PM" is inclusive
    Given the checkout time is exactly "02:00 PM"
    When the user clicks Check-OUT
    Then txtRefund is set to "0.00"
    And the no-refund message is shown

  Scenario: Checkout controls are disabled after Housekeeping status is set
    # Trace: BR-11, ShowStatus("Housekeeping") → DisableControls
    Given a room is in "Housekeeping" status
    When the booking form loads for that room
    Then all input fields are disabled
    And Check-IN and Check-OUT buttons are disabled
    And SAVE button is disabled

  # --- Error Paths ---
  Scenario: Check-out blocked when payment is incomplete
    # Trace: BR-14 — IsPaid = False → block
    Given Booking ID 100007 has SubTotal = 200.00, Deposit = 20.00, Payment = 180.00
    When the user attempts Check-OUT
    Then Check-Out is blocked
    And the message "Please make payment first!" is shown

  Scenario: Check-out blocked when BookingID is 0
    # Trace: Check_OUT — lngBookingID = 0 guard
    Given the booking has not been saved (lngBookingID = 0)
    When the user attempts Check-OUT
    Then the message "Booking has not yet saved. Please save it first!" is shown
```

---

## Feature F8 — Receipt Printing

**Source:** `frmBooking.frm → PrintReceipt("TEMPORARY")`, `PrintReceipt("OFFICIAL")`

```gherkin
Feature: Receipt Printing
  As a hotel clerk
  I need to print temporary and official receipts
  So that guests receive a financial record of their stay

  # --- Happy Path ---
  Scenario: Temporary Receipt shows correct financial breakdown
    # Trace: BR-21 — SubTotal = Payment - Deposit; Total = Payment
    Given a booking with Payment = 320.00 and Deposit = 20.00
    When the user prints a Temporary Receipt (Ctrl+F11)
    Then the report query uses "B.Payment - B.Deposit AS SubTotal"
    And "B.Deposit" is listed separately
    And "B.Payment AS Total" is the receipt total
    And the report filename is "Temporary Receipt.rpt"

  Scenario: Official Receipt shows net total after refund
    # Trace: BR-22 — Total = Payment - Refund
    Given a booking with Payment = 320.00 and Refund = 20.00
    When the user prints an Official Receipt (Ctrl+F12)
    Then the report query uses "B.Payment - B.Refund AS Total"
    And the report filename is "Official Receipt.rpt"

  Scenario: Booking ID is formatted as 6-digit padded number on receipt
    # Trace: BR-24 — Format(ID, '100000')
    Given Booking ID is 42
    When any receipt is printed
    Then the BookingID on the receipt displays as "100042"

  # --- Edge Case ---
  Scenario: Official Receipt printable for non-checked-out room with confirmation
    # Trace: PrintReceipt — status ≠ Housekeeping prompts confirmation
    Given a room is in status "Occupied" (not yet checked out)
    And lngBookingID ≠ 0
    When the user presses Ctrl+F12
    Then a confirmation dialog "This Room is not Checked Out. Are you sure to continue?" is shown
    And if confirmed, the Official Receipt is printed

  # --- Error Path ---
  Scenario: NullQuery fallback when booking record returns no rows
    # Trace: BR-29 — QueryHasData = False → NullQuery with company header only
    Given the booking query returns 0 rows (data inconsistency)
    When a receipt is printed
    Then the fallback query selecting company header with zero-value placeholders is used
    And the report still renders without crashing
```

---

## Feature F9 — Role-Based Access Control

**Source:** `modFunction.bas → UserAccessModule()`, `modDatabase.bas → CreateSampleData`, `frmDashboard.frm → cmdUnit_Click`

```gherkin
Feature: Role-Based Module Access Control
  As a hotel system administrator
  I need to restrict module access by user group
  So that staff only access features appropriate to their role

  Background:
    Given the ModuleAccess table has Group1–Group4 boolean flags per ModuleID

  # --- Happy Path ---
  Scenario: Administrator (Group 1) accesses all modules
    # Trace: BR-05, BR-29 — Group1 all True by default
    Given a user with UserGroup = 1 (Administrator)
    When UserAccessModule is called for any ModuleID
    Then it returns True for all modules

  Scenario: Clerk (Group 4) can access booking and dashboard modules
    # Trace: BR-29 — Clerk default: Dashboard, Booking, List Report, Find Customer, Shift Report
    Given a user with UserGroup = 4 (Clerk)
    When UserAccessModule is called for ModuleID = MOD_DASHBOARD
    Then it returns True
    When UserAccessModule is called for MOD_BOOKING
    Then it returns True

  Scenario: Manager and Supervisor (Groups 2, 3) have no default access
    # Trace: BR-29 — Groups 2 and 3 default all False
    Given a user with UserGroup = 2 (Manager) and default ModuleAccess settings
    When UserAccessModule is called for any ModuleID
    Then it returns False

  # --- Edge Case ---
  Scenario: UserAccessModule with unknown UserID returns False
    # Trace: UserAccessModule — UserData query returns EOF → False
    Given no user with ID "NOBODY" exists in UserData
    When UserAccessModule(MOD_BOOKING, "NOBODY") is called
    Then it returns False

  Scenario: UserAccessModule defaults to current session user when no UserID provided
    # Trace: UserAccessModule — Optional strUserID defaults to gstrUserID
    Given the current session user is "CLERK1" (UserGroup 4)
    When UserAccessModule(MOD_BOOKING) is called without a UserID argument
    Then it evaluates against "CLERK1"'s group

  Scenario: Report modules 12–18 map to reports 1–7
    # Trace: BR-28 — ReportID + 11 = ModuleID
    Given a user has access to ModuleID 12
    When LoadReports runs
    Then Report 1 (ReportID = 1, ModuleID = 12) is visible in the report list

  # --- Error Path ---
  Scenario: Missing ModuleID row in ModuleAccess returns False for all groups
    # Trace: UserAccessModule — rst.EOF → all blnGroup = False
    Given ModuleAccess has no row for ModuleID = 99
    When UserAccessModule(99) is called
    Then it returns False regardless of user group
```

---

## Feature F10 — Report Management

**Source:** `frmReport.frm → PrintReport`, `LoadReports`, modDatabase.bas report query substitution`

```gherkin
Feature: Report Generation and Access Control
  As a hotel manager
  I need to view and print operational reports
  So that I can manage shift accountability and financial reconciliation

  # --- Happy Path ---
  Scenario: Authorised user can view a report they have access to
    # Trace: BR-28 — UserAccessModule(ReportID + 11) = True
    Given the current user has access to ModuleID 12
    When the user opens the Report module
    Then Report 1 appears in the report list

  Scenario: Runtime query substitution replaces $UserID$ placeholder
    # Trace: BR-27 — app substitutes $UserID$ in ReportQuery at runtime
    Given a report with ReportQuery containing "$UserID$"
    When the report is executed for user "CLERK1"
    Then "$UserID$" is replaced with "CLERK1" in the final SQL

  Scenario: Runtime query substitution replaces $BookingID$ placeholder
    # Trace: BR-27 — app substitutes $BookingID$
    Given a report with ReportQuery containing "$BookingID$"
    When the report is executed for booking 100010
    Then "$BookingID$" is replaced with "100010" in the final SQL

  # --- Edge Case ---
  Scenario: Empty report result uses NullQuery fallback
    # Trace: BR-29 — no rows → NullQuery (company header only)
    Given the report query returns 0 rows
    When PrintReport runs
    Then the NullQuery (company header only) is used as the data source
    And the report renders without an empty-data crash

  # --- Error Path ---
  Scenario: Report editing requires developer password and MOD_REPORT_EDIT
    # Trace: BR-26 — requires password "expert" AND module access
    Given the current user has MOD_REPORT_EDIT access
    But has not entered the developer password "expert"
    When the user attempts to enter report-edit mode
    Then access is denied until the correct developer password is entered

  Scenario: User without report module access cannot see that report
    # Trace: BR-28 — UserAccessModule returns False → report hidden
    Given the current user does NOT have access to ModuleID 14
    When the user opens the Report module
    Then Report 3 (ModuleID = 14) is NOT shown in the report list
```

---

## Feature F11 — User Account Management

**Source:** `frmAdmin.frm`, `modFunction.bas → NeedChangePassword`, `AdminUser`

```gherkin
Feature: User Account Administration
  As a hotel administrator
  I need to manage staff accounts and passwords
  So that access credentials remain secure and current

  # --- Happy Path ---
  Scenario: Administrator identifies user as admin
    # Trace: modFunction.bas — AdminUser() returns True for UserGroup = 1
    Given a user with UserGroup = 1 in UserData
    When AdminUser("ADMIN") is called
    Then it returns True

  Scenario: Force-password-change flag triggers redirect after login
    # Trace: BR-07 — NeedChangePassword returns True → frmUserChangePassword
    Given "NEW_STAFF" has ChangePassword = True
    When "NEW_STAFF" logs in with a valid password
    Then NeedChangePassword("NEW_STAFF") returns True
    And the system redirects to frmUserChangePassword before showing the dashboard

  # --- Edge Case ---
  Scenario: Non-admin user is not identified as administrator
    # Trace: AdminUser — UserGroup ≠ 1 → False
    Given users with UserGroups 2, 3, and 4
    When AdminUser is called for each
    Then it returns False in all cases

  Scenario: NeedChangePassword returns False for non-existent user
    # Trace: NeedChangePassword — rst.EOF → False
    Given no user with ID "GHOST" exists in UserData
    When NeedChangePassword("GHOST") is called
    Then it returns False

  # --- Error Path ---
  Scenario: Account locked after maximum login failures (non-admin)
    # Trace: BR-02, BR-03 — LoginAttempts > 2 → Active = False → system exits
    Given user "CLERK3" has LoginAttempts = 2 and Active = True
    When an incorrect password is submitted
    Then LoginAttempts becomes 3
    And Active is set to False in UserData
    And the application terminates (End is called)
```

---

## Feature F12 — Void and Unvoid Booking

**Source:** `frmBooking.frm → VoidBooking(plngBookingID)`

```gherkin
Feature: Void and Unvoid Booking
  As a hotel administrator
  I need to void or reinstate a booking record
  So that incorrect bookings can be corrected without deletion

  # --- Happy Path ---
  Scenario: Active booking is voided (Active set to False)
    # Trace: VoidBooking — strStatus ≠ "Void" → Active = False
    Given a booking with BookingID = 100015 and Active = True
    And strStatus is "Booked"
    When VoidBooking(100015) is called
    Then Booking.Active is set to False in the database
    And LastModifiedDate and LastModifiedBy are updated
    And PopulateValues reloads the form showing "Void" status

  Scenario: Voided booking is reinstated (Active set to True)
    # Trace: VoidBooking — strStatus = "Void" → Active = True
    Given a booking with BookingID = 100015 and Active = False
    And strStatus is "Void"
    When VoidBooking(100015) is called
    Then Booking.Active is set to True in the database
    And the form displays the original booking status

  # --- Edge Case ---
  Scenario: Void operation is skipped for BookingID = 0
    # Trace: VoidBooking — plngBookingID = 0 → Exit Sub
    Given plngBookingID = 0
    When VoidBooking(0) is called
    Then no database update is performed
    And the sub exits silently

  Scenario: Void does not affect Temp = TRUE records
    # Trace: VoidBooking — SQL includes "AND Temp = FALSE"
    Given a booking record with Temp = True
    When VoidBooking is called for its ID
    Then the WHERE clause filters out Temp records
    And no update is applied to the temporary booking
```

---

## Acceptance Criteria

```gherkin
# AC-01 (BR-01): User ID lookup MUST always use uppercase form.
#   PASS: "clerk1" → stored as "CLERK1" for all DB queries.
#   FAIL: case-sensitive mismatch causes login failure for valid users.

# AC-02 (BR-02): LoginAttempts counter for non-admin accounts MUST reach exactly 3 before lockout.
#   PASS: 3rd failure → Active = False, app terminates.
#   FAIL: lockout triggers on 2nd failure, or never triggers.

# AC-03 (BR-04): Password hash MUST equal GoldFishEncode(plaintext + stored_salt).
#   PASS: correct algorithm applied; wrong password always fails.
#   FAIL: plain-text comparison, or wrong salt concatenation order.

# AC-04 (BR-13): Check-In MUST be blocked unless Payment = SubTotal + Deposit exactly.
#   PASS: Payment 319.99 blocks check-in; 320.00 allows.
#   FAIL: rounding tolerance permits under-payment.

# AC-05 (BR-14): Check-out at or after 14:00 MUST set Refund = 0.00 and disable refund field.
#   PASS: TimeValue("02:00 PM") triggers no-refund path.
#   FAIL: boundary is exclusive, allowing 14:00 refund.

# AC-06 (BR-15): Room status after check-out MUST be "Housekeeping" (not "Open").
#   PASS: UpdateRoomStatus("Housekeeping") called.
#   FAIL: room set directly to "Open", skipping housekeeping step.

# AC-07 (BR-18/19): SubTotal = nights × RoomPrice; TotalDue = SubTotal + Deposit.
#   PASS: 3 nights × 150.00 = 450.00; TotalDue = 470.00.
#   FAIL: off-by-one in nights, or deposit excluded from TotalDue.

# AC-08 (BR-23): Checkout date MUST account for check-in time relative to noon.
#   PASS: 09:00 AM + 2 nights = CheckInDate + 1 day at noon.
#   PASS: 02:00 PM + 2 nights = CheckInDate + 2 days at noon.
#   FAIL: always adding full StayDuration days regardless of check-in time.

# AC-09 (BR-24): Booking ID MUST be formatted as 6-digit number with Format(ID,'100000').
#   PASS: ID=42 → "100042"; ID=1 → "100001".
#   FAIL: ID displayed as unpadded integer.

# AC-10 (BR-21): Temporary Receipt SubTotal = Payment - Deposit (not the stored SubTotal).
#   PASS: Payment=320, Deposit=20 → SubTotal on receipt = 300.
#   FAIL: uses Booking.SubTotal directly from field.

# AC-11 (BR-22): Official Receipt Total = Payment - Refund.
#   PASS: Payment=320, Refund=20 → Total=300.
#   FAIL: Total = Payment (ignores refund).

# AC-12 (BR-05): UserAccessModule MUST respect the user's group flag in ModuleAccess.
#   PASS: Group4 user blocked from MOD_ADMIN module.
#   FAIL: function returns True by default for any authenticated user.
```

---

## Regression Checklist

### 🔢 Data Integrity

| Check ID | Description | Source Rule | Pass Condition |
|----------|-------------|-------------|----------------|
| DI-01 | `SubTotal = StayDuration × RoomPrice` — no floating-point drift | BR-18 | Exact match to 2 decimal places |
| DI-02 | `TotalDue = SubTotal + Deposit` | BR-19 | Matches UI display |
| DI-03 | `IsPaid` returns True only when `Payment = SubTotal + Deposit` exactly | BR-13 | Strict equality, no tolerance |
| DI-04 | Late check-out (≥ 14:00) forces `Refund = 0.00` | BR-14 | Field value AND disabled state |
| DI-05 | `Format(BookingID, '100000')` pads to 6 digits | BR-24 | ID=1→"100001", ID=999999→"999999" |
| DI-06 | Temp=TRUE rows excluded from Void operation WHERE clause | `VoidBooking` | No temp records mutated |
| DI-07 | `CreatedDate/CreatedBy` stamped only on first save (`strStatus = "Open"`) | BR-12 | Not overwritten on subsequent saves |
| DI-08 | `LastModifiedDate/By` updated on every save, check-in, check-out | `SaveBooking`, `Check_IN`, `Check_OUT` | Timestamp ≠ NULL after each operation |
| DI-09 | Checkout date formula: before noon → `+days-1`, at/after noon → `+days` | BR-23 | Both branches tested with noon boundary |
| DI-10 | Temp receipt: `SubTotal = Payment - Deposit` (not stored SubTotal) | BR-21 | SQL expression verified in report query |
| DI-11 | Official receipt: `Total = Payment - Refund` | BR-22 | SQL expression verified in report query |
| DI-12 | Room.BookingID is set when status transitions to "Booked" | `UpdateRoomStatus` | Foreign key populated correctly |
| DI-13 | Room.BookingID is NOT reset by Occupied or Housekeeping transitions | `UpdateRoomStatus` | Only "Booked" case sets BookingID |

### 🔒 Security & Access Control

| Check ID | Description | Source Rule | Pass Condition |
|----------|-------------|-------------|----------------|
| SEC-01 | Password stored as `GoldFishEncode(plain + salt)`, never plain-text | BR-04 | Hash comparison only |
| SEC-02 | UserID forced to uppercase before all lookups | BR-01 | `UCase` applied pre-query |
| SEC-03 | Non-admin account locked after 3 failed attempts | BR-02 | `LoginAttempts > 2 → Active = False` |
| SEC-04 | Admin account exempt from lockout increment | BR-06 | Counter unchanged after admin bad password |
| SEC-05 | Frozen accounts (`Active = False`) rejected at login | BR-03 | Auth denied regardless of correct password |
| SEC-06 | `UserAccessModule` returns False for unknown ModuleID | BR-05 | Missing ModuleAccess row → False |
| SEC-07 | `UserAccessModule` returns False for unknown UserID | BR-05 | Missing UserData row → False |
| SEC-08 | Developer password "expert" required to edit reports | BR-26 | Separate credential check enforced |
| SEC-09 | Idle timeout terminates session after configured seconds | BR-09 | `intTick > gintUserIdle → frmDialog modal` |
| SEC-10 | Idle value outside 0–3600 normalised to 0 (no timeout) | BR-10 | `gintUserIdle = 0` for out-of-range values |
| SEC-11 | Hidden credential shortcut (copyright label) must be removed in migration | BR-08 | Shortcut absent in modern codebase |

### 🔗 Integration Points

| Check ID | Description | Components | Pass Condition |
|----------|-------------|------------|----------------|
| INT-01 | ADO connection uses `Microsoft.Jet.OLEDB.4.0` + `GenWord()` password | `ConnectDB()` | Connection opens without error |
| INT-02 | DB version check runs before login screen is shown | `DB_Version()` → `frmUserLogin` | Version ≥ 1.3 or migration completes |
| INT-03 | `frmDashboard` refreshes all 4 summary counts on `frmBooking.Unload` | `Form_Unload` | All `ShowSummaryN` calls executed |
| INT-04 | Crystal Reports engine initialised before any print operation | `CRAXDRT.Application` | No null-reference on print |
| INT-05 | Report query `$UserID$` token substituted at runtime | BR-27 | Token absent from final SQL |
| INT-06 | Report query `$BookingID$` token substituted at runtime | BR-27 | Correct booking ID injected |
| INT-07 | `LogErrorDB` writes to `LogError` table on any runtime error | `modFunction.bas` | Row inserted; no silent swallowing |
| INT-08 | `frmDatabase` path entry persists to `Config.txt` and reconnects | Startup flow | Next launch uses saved path |
| INT-09 | Dashboard blink preference (`DashboardBlink`) read from `UserData` per user | BR-25 | Each user's preference respected independently |

### 🔄 State Machine Integrity

| Check ID | Room Status Transition | Trigger | Invalid Transitions Must Be Blocked |
|----------|----------------------|---------|-------------------------------------|
| SM-01 | Open → Booked | `SaveBooking` (first save) | Cannot skip to Occupied |
| SM-02 | Booked → Occupied | `Check_IN` + `IsPaid = True` | Cannot check in without full payment |
| SM-03 | Occupied → Housekeeping | `Check_OUT` + `IsPaid = True` | Cannot check out without full payment |
| SM-04 | Housekeeping → (manual reset) | Admin action | Auto-transition not permitted |
| SM-05 | Any → Maintenance | Admin action | Booking blocked from Maintenance rooms |
| SM-06 | Active → Void | `VoidBooking` (Active = False) | Temp records exempt |
| SM-07 | Void → Active | `VoidBooking` (unvoid) | Restores prior status |

---

## Traceability Matrix

| Scenario ID | Feature | Business Rule | Method / Form |
|-------------|---------|---------------|---------------|
| F1-SC1 | Startup | Config.txt flow | `frmSplash.Timer1_Timer` |
| F1-SC2 | Startup | DB version migration | `DB_Version()`, `Migrate_Database` |
| F1-SC3 | Startup | Missing Config.txt | `frmDatabase` |
| F2-SC1 | Login | BR-01, BR-04 | `cmdOK_Click` |
| F2-SC2 | Login | BR-07 | `NeedChangePassword` |
| F2-SC3 | Login | BR-06 | `cmdOK_Click` admin exemption |
| F2-SC4 | Login | BR-10 | Idle normalisation |
| F2-SC5 | Login | BR-08 | `lblCopyright_Click` |
| F2-SC6 | Login | BR-02 | 3-attempt lockout |
| F2-SC7 | Login | BR-03 | Frozen account |
| F3-SC1 | Idle | BR-09 | `tmrClock_Timer` |
| F3-SC2 | Idle | BR-10 | `gintUserIdle = 0` |
| F4-SC1 | Dashboard | BR-11 | `SetButtonProperties` |
| F4-SC2 | Dashboard | BR-25 | `AlertBooking` |
| F4-SC5 | Dashboard | BR-16 | `cmdUnit_Click` Maintenance |
| F5-SC1 | Booking | BR-12 | `SaveBooking` |
| F5-SC2 | Booking | BR-18, BR-19 | `SumTotal` |
| F5-SC3 | Booking | BR-24 | `CreateTempBookingID` |
| F5-SC4/5 | Booking | BR-23 | `cboStayDuration_Click` |
| F5-SC6 | Booking | BR-20 | `ResetFields` |
| F6-SC1 | Check-In | BR-13 | `Check_IN`, `IsPaid` |
| F7-SC1/2 | Check-Out | BR-14 | `Check_OUT` |
| F7-SC3 | Check-Out | BR-15 | `UpdateRoomStatus` |
| F8-SC1 | Receipt | BR-21 | `PrintReceipt("TEMPORARY")` |
| F8-SC2 | Receipt | BR-22 | `PrintReceipt("OFFICIAL")` |
| F9-SC1–SC7 | RBAC | BR-05, BR-28 | `UserAccessModule` |
| F10-SC1–SC5 | Reports | BR-26–BR-29 | `PrintReport`, `LoadReports` |
| F11-SC1–SC5 | User Mgmt | BR-03, BR-07 | `NeedChangePassword`, `AdminUser` |
| F12-SC1–SC4 | Void | `VoidBooking` logic | `VoidBooking` |