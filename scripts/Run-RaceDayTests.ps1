
param(
    [string]$BaseUrl = 'http://localhost:8080',
    [string]$OrganiserCode = ''
)

$ErrorActionPreference = 'Stop'
$inv  = [Globalization.CultureInfo]::InvariantCulture
$Base = $BaseUrl.TrimEnd('/')
$run  = Get-Date -Format 'yyMMddHHmmss'
$script:pass = 0
$script:fail = 0

if (-not $OrganiserCode) {
    $envFile = Join-Path (Split-Path $PSScriptRoot -Parent) '.env'
    if (Test-Path $envFile) {
        $line = Get-Content $envFile |
            Where-Object { $_ -match '^\s*ORGANISER_CODE\s*=' } |
            Select-Object -First 1
        if ($line) {
            $OrganiserCode = ($line -split '=', 2)[1].Trim().Trim('"')
        }
    }
}

if (-not $OrganiserCode) {
    throw 'Pass -OrganiserCode, or keep ORGANISER_CODE in the .env next to docker-compose.yml.'
}

# ---------- helpers ----------

function Check([string]$Name, [bool]$Condition, [string]$Detail = '') {
    if ($Condition) {
        $script:pass++
        Write-Host "  PASS  $Name" -ForegroundColor Green
    }
    else {
        $script:fail++
        Write-Host "  FAIL  $Name  $Detail" -ForegroundColor Red
    }
}

function Get-Page($Session, [string]$Path) {
    Invoke-WebRequest -Uri "$Base$Path" -WebSession $Session -UseBasicParsing -MaximumRedirection 10
}

function Get-FinalPath($Response) {
    $b = $Response.BaseResponse

    if ($b.PSObject.Properties['ResponseUri']) {
        return $b.ResponseUri.AbsolutePath
    }

    return $b.RequestMessage.RequestUri.AbsolutePath
}

function Get-Token([string]$Html) {
    $m = [regex]::Match(
        $Html,
        'name="__RequestVerificationToken"[^>]*value="([^"]+)"'
    )

    if (-not $m.Success) {
        throw 'Antiforgery token not found on the page.'
    }

    return $m.Groups[1].Value
}

function New-Session {
    $null = Invoke-WebRequest -Uri "$Base/Auth/Login" `
        -SessionVariable s -UseBasicParsing

    return $s
}

# Loads a page for its antiforgery token, then posts the fields to PostPath.
function Send-Form(
    $Session,
    [string]$PostPath,
    [hashtable]$Fields,
    [string]$TokenPath
) {
    $page = Get-Page $Session $TokenPath
    $body = @{}

    foreach ($k in $Fields.Keys) {
        $body[$k] = $Fields[$k]
    }

    $body['__RequestVerificationToken'] = Get-Token $page.Content

    Invoke-WebRequest -Uri "$Base$PostPath" -Method Post -Body $body `
        -WebSession $Session -UseBasicParsing -MaximumRedirection 10
}

function Get-Ids($Session, [string]$Path, [string]$Pattern) {
    $html = (Get-Page $Session $Path).Content

    return @(
        [regex]::Matches($html, $Pattern) |
        ForEach-Object { [int]$_.Groups[1].Value } |
        Sort-Object -Unique
    )
}

function Invoke-Register($Session, [hashtable]$Fields) {
    Send-Form $Session '/Auth/Register' $Fields '/Auth/Register'
}

function Invoke-Login($Session, [string]$Email, [string]$Password) {
    Send-Form $Session '/Auth/Login' @{
        Email = $Email
        Password = $Password
    } '/Auth/Login'
}

function New-EventFields(
    [string]$Name,
    [datetime]$Date,
    [datetime]$Deadline
) {
    return @{
        Name                 = $Name
        Description          = 'Automated test event'
        Date                 = $Date.ToString('yyyy-MM-dd', $inv)
        Location             = 'Test Park'
        StartTime            = '08:00'
        Routemap             = 'City loop'
        EventType            = 'Run'
        Distance             = '5'
        RegistrationDeadline = $Deadline.ToString('yyyy-MM-dd\THH:mm', $inv)
    }
}

function New-RaceEvent($Session, [hashtable]$Fields) {
    $before = Get-Ids $Session '/Organiser' '/Organiser/Edit/(\d+)'

    $r = Send-Form $Session '/Organiser/Create' $Fields '/Organiser/Create'

    $after = Get-Ids $Session '/Organiser' '/Organiser/Edit/(\d+)'

    $id = $after |
        Where-Object { $before -notcontains $_ } |
        Select-Object -First 1

    return [pscustomobject]@{
        Id       = $id
        Response = $r
    }
}

function New-CategoryFields(
    [int]$EventId,
    [string]$Level,
    [string]$Distance
) {
    return @{
        'Category.EventID'         = "$EventId"
        'Category.CategoryName'    = 'Open 5K'
        'Category.Distance'        = $Distance
        'Category.CategoryLevel'   = $Level
        'Category.EntryFee'        = '0'
        'Category.MaxParticipants' = '100'
    }
}

# ---------- run ----------

Write-Host "RaceDay MVC tests against $Base (run $run)" -ForegroundColor Cyan

try {
    $orgEmail  = "rdtest-org-$run@raceday.test"
    $partEmail = "rdtest-run-$run@raceday.test"
    $password  = 'Passw0rd!x'
    $nameA     = "RDTestFuture$run"
    $nameB     = "RDTestPast$run"

    Write-Host "`nSetup: accounts and data"

    $org  = New-Session
    $part = New-Session

    $r = Invoke-Register $org @{
        FirstName          = 'Test'
        LastName           = 'Organiser'
        Email              = $orgEmail
        Password           = $password
        PhoneNumber        = '0821112222'
        Role               = 'Organiser'
        OrganisationName   = 'RDTest Org'
        OrganisationContact = '0821112222'
        OrganiserCode      = $OrganiserCode
    }

    Check 'Organiser registers and lands on Login' `
        ((Get-FinalPath $r) -eq '/Auth/Login') (Get-FinalPath $r)

    $r = Invoke-Register $part @{
        FirstName              = 'Test'
        LastName               = 'Runner'
        Email                  = $partEmail
        Password               = $password
        PhoneNumber            = '0823334444'
        Role                   = 'Participant'
        DateOfBirth            = '1995-05-05'
        Gender                 = 'Male'
        EmergencyContactName   = 'Test Contact'
        EmergencyContactNumber = '0827654321'
    }

    Check 'Participant registers and lands on Login' `
        ((Get-FinalPath $r) -eq '/Auth/Login') (Get-FinalPath $r)

    $r = Invoke-Login $org $orgEmail $password

    Check 'Organiser logs in and lands on Home' `
        ((Get-FinalPath $r) -eq '/') (Get-FinalPath $r)

    $r = Invoke-Login $part $partEmail $password

    Check 'Participant logs in and lands on Home' `
        ((Get-FinalPath $r) -eq '/') (Get-FinalPath $r)

    $dash = Get-Page $org '/Organiser'

    Check 'Organiser session opens the dashboard' `
        ($dash.Content -match 'Dashboard')

    $future = New-RaceEvent $org (
        New-EventFields $nameA (Get-Date).AddDays(60) (Get-Date).AddDays(30)
    )

    Check 'Future event created' `
        ($future.Id -and $future.Response.Content -match 'Event created successfully')

    $past = New-RaceEvent $org (
        New-EventFields $nameB (Get-Date).AddDays(-30) (Get-Date).AddDays(-60)
    )

    Check 'Past event created' ([bool]$past.Id)

    if (-not $future.Id) {
        throw 'Future event was not created, cannot continue.'
    }

    $eventId = [int]$future.Id

    $beforeCats = Get-Ids $org '/Categories' '/Categories/Edit/(\d+)'

    $r = Send-Form $org '/Categories/Create' `
        (New-CategoryFields $eventId 'All' '5') '/Categories'

    Check 'Category created' `
        ($r.Content -match 'Category created successfully')

    $afterCats = Get-Ids $org '/Categories' '/Categories/Edit/(\d+)'

    $categoryId = $afterCats |
        Where-Object { $beforeCats -notcontains $_ } |
        Select-Object -First 1

    if (-not $categoryId) {
        throw 'Category id not found, cannot continue.'
    }

    # ------------------------------------------------------------
    # 1. Participant enrols in an event
    # ------------------------------------------------------------

    Write-Host "`n1. Participant enrols in an event"

    $r = Send-Form $part '/Events/Enrol' @{
        eventId    = "$eventId"
        categoryId = "$categoryId"
    } "/Events/Details/$eventId"

    Check 'Event page shows the participant enrolment' `
        ($r.Content -match 'Your enrolment')

    $mine = Get-Page $part '/MyEnrolments'

    Check 'My enrolments lists the event' `
        ($mine.Content -match [regex]::Escape($nameA))

    # ------------------------------------------------------------
    # 2. Organiser dashboard
    # ------------------------------------------------------------

    Write-Host "`n2. Organiser dashboard"

    $dash = Get-Page $org '/Organiser'

    Check 'Shows Total enrolments' ($dash.Content -match 'Total enrolments')
    Check 'Shows Next event' ($dash.Content -match 'Next event')
    Check 'Shows 1 enrolled link' ($dash.Content -match '1 enrolled')
    Check 'Shows Next up badge' ($dash.Content -match 'Next up')

    # ------------------------------------------------------------
    # 3. Registrations: Pending then Confirm
    # ------------------------------------------------------------

    Write-Host "`n3. Registrations: Pending then Confirm"

    $regs = Get-Page $org "/Organiser/Registrations/$eventId"

    Check 'Pending badge is shown (amber)' `
        ($regs.Content -match 'rd-badge-pending"\s*>\s*Pending')

    $m = [regex]::Match(
        $regs.Content,
        'name="enrolmentId"\s+value="(\d+)"'
    )

    Check 'Confirm button is present' $m.Success

    if ($m.Success) {
        $r = Send-Form $org '/Organiser/UpdateEnrolmentStatus' @{
            eventId     = "$eventId"
            enrolmentId = $m.Groups[1].Value
            status      = 'Confirmed'
        } "/Organiser/Registrations/$eventId"

        Check 'Flash says Enrolment marked as Confirmed' `
            ($r.Content -match 'Enrolment marked as Confirmed')

        Check 'Confirmed badge is green' `
            ($r.Content -match 'rd-badge-success"\s*>\s*Confirmed')
    }

    # ------------------------------------------------------------
    # 4. Closed registration still shows the real status
    # ------------------------------------------------------------

    Write-Host "`n4. Closed registration still shows the real status"

    $edit = New-EventFields $nameA `
        (Get-Date).AddDays(60) `
        (Get-Date).AddDays(-1)

    $edit['EventId'] = "$eventId"

    $r = Send-Form $org '/Organiser/Edit' $edit "/Organiser/Edit/$eventId"

    Check 'Event deadline moved to the past' `
        ($r.Content -match 'Event updated successfully')

    $regs = Get-Page $org "/Organiser/Registrations/$eventId"

    Check 'Header shows Registration closed' `
        ($regs.Content -match 'Registration closed')

    Check 'Real status (Confirmed) still shown' `
        ($regs.Content -match 'rd-badge-success"\s*>\s*Confirmed')

    # ------------------------------------------------------------
    # 5. Categories: bad data gives plain messages
    # ------------------------------------------------------------

    Write-Host "`n5. Categories: bad data gives plain messages"

    $r = Send-Form $org '/Categories/Create' `
        (New-CategoryFields $eventId 'Elite' '5') '/Categories'

    Check 'Invalid level shows a plain message' `
        ($r.Content -match 'Category level must be')

    Check 'No raw JSON shown' (
        ($r.Content -notmatch '\{"message"') -and
        ($r.Content -notmatch 'The API could not')
    )

    $r = Send-Form $org '/Categories/Create' `
        (New-CategoryFields $eventId 'All' '0') '/Categories'

    Check 'Zero distance shows a plain message' `
        ($r.Content -match 'Distance must be greater than zero')

    # ------------------------------------------------------------
    # 6. Participant cannot open the organiser area
    # ------------------------------------------------------------

    Write-Host "`n6. Participant cannot open the organiser area"

    $r = Get-Page $part '/Organiser'
    $p = Get-FinalPath $r

    Check 'Redirected to Home' `
        (@('/', '/Home', '/Home/Index') -contains $p) $p

    Check 'Shows a message, not an error page' `
        ($r.Content -match 'That page is for organisers only')

    # ------------------------------------------------------------
    # 7. Events page lists upcoming and completed events
    # ------------------------------------------------------------

    Write-Host "`n7. Events page lists upcoming and completed events"

    $anon = New-Session
    $events = Get-Page $anon '/Events'

    Check 'Events page loads successfully' `
        ($events.StatusCode -eq 200)

    Check 'Future event is listed' `
        ($events.Content -match [regex]::Escape($nameA))

    Check 'Past event is listed' `
        ($events.Content -match [regex]::Escape($nameB))

    Check 'Closed registration event is labelled' `
        ($events.Content -match 'Registration closed')

    Check 'Completed event is labelled' `
        ($events.Content -match 'Event completed')

    # ------------------------------------------------------------
    # Extra: unauthenticated users and logout
    # ------------------------------------------------------------

    Write-Host "`nExtra: unauthenticated users and logout"

    foreach ($path in '/Organiser', '/MyEnrolments', '/Profile') {
        $r = Get-Page (New-Session) $path

        Check "Guest on $path is sent to Login" `
            ((Get-FinalPath $r) -eq '/Auth/Login') (Get-FinalPath $r)
    }

    $null = Send-Form $org '/Auth/Logout' @{} '/Organiser'

    $r = Get-Page $org '/Organiser'

    Check 'After logout the organiser area needs login again' `
        ((Get-FinalPath $r) -eq '/Auth/Login') (Get-FinalPath $r)
}
catch {
    $script:fail++

    Write-Host "`n  ERROR  $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nResult: $script:pass passed, $script:fail failed" -ForegroundColor Cyan

if ($script:fail -gt 0) {
    exit 1
}
