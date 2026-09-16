#Requires -Version 5.1
<#
    DEPLOY.md 6절이 부르는 스크립트. 사전 조건 -> 게시 -> (선택) 사이트 등록 -> 스모크 테스트를
    하나로 묶는다. 문서를 읽고 사람이 순서를 외우는 대신 스크립트가 먼저 잡아 준다.

    knox-mail-pipeline 의 preflight_adfs.ps1 과 같은 방식: 실패하면 무엇이 왜 막혔는지
    바로 알 수 있게, 각 단계를 독립된 함수로 나누고 결과를 하나씩 찍는다.

    사용 예:
      # 사전 조건만 확인
      .\preflight-iis.ps1 -CheckOnly

      # 사전 조건 확인 + 게시
      .\preflight-iis.ps1 -PublishOutput C:\inetpub\wwwroot\workbench

      # 사전 조건 확인 + 게시 + 사이트 등록(WebAdministration 필요, 관리자 권한)
      .\preflight-iis.ps1 -PublishOutput C:\inetpub\wwwroot\workbench `
          -RegisterSite -SiteName Workbench -AppPoolName Workbench -Port 443 -Hostname workbench.internal

      # 이미 떠 있는 사이트에 스모크 테스트만
      .\preflight-iis.ps1 -CheckOnly:$false -SkipPreflight -SkipPublish -SmokeTestUrl https://workbench.internal
#>

[CmdletBinding()]
param(
    [string]$ProjectPath = "src\Workbench.Web\Workbench.Web.csproj",
    [string]$PublishOutput = ".\publish",

    [switch]$CheckOnly,
    [switch]$SkipPreflight,
    [switch]$SkipPublish,

    [switch]$RegisterSite,
    [string]$SiteName,
    [string]$AppPoolName,
    [string]$Hostname,
    [int]$Port = 443,

    [string]$SmokeTestUrl,
    [switch]$SkipSmokeTest,

    # DEPLOY.md 4절: 이 엔드포인트들은 dotnet run 데모 모드 확인 목록과 같다.
    [string[]]$SmokeTestPaths = @('/', '/projects', '/issues', '/board', '/pages', '/search')
)

$ErrorActionPreference = 'Stop'
$script:FailureCount = 0

function Write-Section {
    param([string]$Title)
    Write-Host ""
    Write-Host "== $Title ==" -ForegroundColor Cyan
}

function Write-CheckResult {
    param(
        [string]$Name,
        [bool]$Passed,
        [string]$Detail = "",
        [bool]$Fatal = $true
    )

    if ($Passed) {
        Write-Host "  [OK] $Name" -ForegroundColor Green
    }
    elseif ($Fatal) {
        Write-Host "  [FAIL] $Name" -ForegroundColor Red
        if ($Detail) { Write-Host "         $Detail" -ForegroundColor Red }
        $script:FailureCount++
    }
    else {
        Write-Host "  [WARN] $Name" -ForegroundColor Yellow
        if ($Detail) { Write-Host "         $Detail" -ForegroundColor Yellow }
    }
}

# --- 1. 사전 조건 (DEPLOY.md 1절 표와 1:1 대응) ---------------------------------

function Test-HostingBundle {
    # ANCM v2 모듈 DLL 이 inetsrv 에 있는지로 판정한다 — Hosting Bundle 이 없으면 아예 없다.
    $inetsrv = Join-Path $env:WINDIR 'System32\inetsrv'
    $ancmV2 = Join-Path $inetsrv 'aspnetcore*.dll'
    $found = @(Get-ChildItem -Path $ancmV2 -ErrorAction SilentlyContinue)

    $runtimeOk = $false
    try {
        $runtimes = & dotnet --list-runtimes 2>$null
        $runtimeOk = [bool]($runtimes | Select-String 'Microsoft\.AspNetCore\.App 8\.')
    }
    catch {
        $runtimeOk = $false
    }

    $passed = ($found.Count -gt 0) -and $runtimeOk
    $detail = if (-not $passed) {
        ".NET 8 Hosting Bundle 을 설치하라 (ANCM v2 DLL 없음 또는 ASP.NET Core 8 런타임 없음). " +
        "없이 배포하면 500.19 로 죽는다."
    } else { "" }

    Write-CheckResult -Name '.NET 8 Hosting Bundle (ANCM v2)' -Passed $passed -Detail $detail
}

function Test-WebSocketFeature {
    $passed = $false
    $detail = ""

    try {
        if (Get-Command Get-WindowsFeature -ErrorAction SilentlyContinue) {
            # Windows Server
            $feature = Get-WindowsFeature -Name Web-WebSockets -ErrorAction SilentlyContinue
            $passed = [bool]($feature -and $feature.Installed)
        }
        elseif (Get-Command Get-WindowsOptionalFeature -ErrorAction SilentlyContinue) {
            # Windows 클라이언트(테스트/개발용 IIS)
            $feature = Get-WindowsOptionalFeature -Online -FeatureName IIS-WebSockets -ErrorAction SilentlyContinue
            $passed = [bool]($feature -and $feature.State -eq 'Enabled')
        }
        else {
            $detail = "이 서버에서 IIS 기능 조회 cmdlet 을 찾지 못했다 - 수동으로 " +
                "'WebSocket Protocol' 이 켜져 있는지 확인하라."
        }
    }
    catch {
        $detail = "확인 중 오류: $($_.Exception.Message)"
    }

    if (-not $passed -and -not $detail) {
        $detail = "IIS 역할 서비스 'WebSocket Protocol' 이 꺼져 있다. Blazor Server 가 " +
            "에러 없이 long polling 으로 조용히 강등된다 - 켜라."
    }

    Write-CheckResult -Name 'IIS WebSocket Protocol' -Passed $passed -Detail $detail
}

function Test-AppPoolNoManagedCode {
    param([string]$Name)

    if (-not $Name) {
        $skipDetail = "-AppPoolName 이 지정되지 않아 건너뜀. 배포 전 IIS 관리자에서 " +
            "해당 앱 풀의 .NET CLR 버전이 '관리 코드 없음' 인지 직접 확인하라."
        Write-CheckResult -Name '앱 풀: 관리 코드 없음' -Passed $true -Fatal $false -Detail $skipDetail
        return
    }

    try {
        Import-Module WebAdministration -ErrorAction Stop
        $pool = Get-ItemProperty "IIS:\AppPools\$Name" -ErrorAction Stop
        $passed = [string]::IsNullOrEmpty($pool.managedRuntimeVersion)
        $detail = if (-not $passed) {
            "앱 풀 '$Name' 의 .NET CLR 버전이 '$($pool.managedRuntimeVersion)' 이다. " +
            "'관리 코드 없음' 으로 바꿔라 - .NET 8 은 IIS 의 CLR 관리 기능을 쓰지 않는다."
        } else { "" }
        Write-CheckResult -Name "앱 풀 '$Name': 관리 코드 없음" -Passed $passed -Detail $detail
    }
    catch {
        $errorDetail = "WebAdministration 모듈로 조회하지 못함 ($($_.Exception.Message)) - " +
            "IIS 관리자에서 수동 확인하라."
        Write-CheckResult -Name "앱 풀 '$Name': 관리 코드 없음" -Passed $false -Fatal $false -Detail $errorDetail
    }
}

function Invoke-Preflight {
    Write-Section "1. 사전 조건 (DEPLOY.md 1절)"
    Test-HostingBundle
    Test-WebSocketFeature
    Test-AppPoolNoManagedCode -Name $AppPoolName

    if ($script:FailureCount -gt 0) {
        Write-Host ""
        Write-Host "사전 조건 $($script:FailureCount)건이 막혔다. 위 항목을 먼저 해결하라." -ForegroundColor Red
        exit 1
    }
}

# --- 2. 게시 ---------------------------------------------------------------

function Invoke-Publish {
    Write-Section "2. dotnet publish"

    if (-not (Test-Path $ProjectPath)) {
        Write-Host "  프로젝트를 찾을 수 없다: $ProjectPath" -ForegroundColor Red
        exit 1
    }

    Write-Host "  $ProjectPath -> $PublishOutput" -ForegroundColor Gray
    & dotnet publish $ProjectPath -c Release -o $PublishOutput
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  dotnet publish 실패 (exit $LASTEXITCODE)" -ForegroundColor Red
        exit 1
    }

    $webConfig = Join-Path $PublishOutput 'web.config'
    if (Test-Path $webConfig) {
        Write-Host "  web.config 생성됨: $webConfig" -ForegroundColor Green
        Write-Host "  -> deploy\web.config.snippet.xml 의 <environmentVariables>/<security> 를 병합하라." -ForegroundColor Yellow
    }
    else {
        Write-Host "  web.config 가 생성되지 않았다 - Microsoft.NET.Sdk.Web 프로젝트가 맞는지 확인하라." -ForegroundColor Red
        exit 1
    }
}

# --- 3. 사이트 등록 (선택) ----------------------------------------------------

function Invoke-RegisterSite {
    Write-Section "3. IIS 사이트 등록"

    if (-not $SiteName -or -not $AppPoolName) {
        Write-Host "  -SiteName 과 -AppPoolName 이 모두 필요하다. 건너뛴다." -ForegroundColor Yellow
        return
    }

    try {
        Import-Module WebAdministration -ErrorAction Stop
    }
    catch {
        $message = "  WebAdministration 모듈을 불러오지 못했다 - 이 서버에 IIS 관리 도구가 없다. " +
            "IIS 관리자에서 수동으로 등록하라."
        Write-Host $message -ForegroundColor Yellow
        return
    }

    if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
        New-WebAppPool -Name $AppPoolName | Out-Null
        Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name managedRuntimeVersion -Value ''
        Write-Host "  앱 풀 생성: $AppPoolName (관리 코드 없음)" -ForegroundColor Green
    }
    else {
        Write-Host "  앱 풀 '$AppPoolName' 이미 있음 - 그대로 사용" -ForegroundColor Gray
    }

    $physicalPath = (Resolve-Path $PublishOutput).Path

    if (-not (Test-Path "IIS:\Sites\$SiteName")) {
        $binding = if ($Hostname) { "*:${Port}:$Hostname" } else { "*:${Port}:" }
        New-Website -Name $SiteName -PhysicalPath $physicalPath -ApplicationPool $AppPoolName `
            -Port $Port -HostHeader $Hostname | Out-Null
        Write-Host "  사이트 생성: $SiteName ($physicalPath, 포트 $Port)" -ForegroundColor Green
    }
    else {
        Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $physicalPath
        Write-Host "  사이트 '$SiteName' 이미 있음 - physicalPath 갱신" -ForegroundColor Gray
    }

    Write-Host "  -> web.config 의 environmentVariables (2절 표) 를 IIS 관리자 구성 편집기로 채워라." -ForegroundColor Yellow
}

# --- 4. 스모크 테스트 --------------------------------------------------------

function Invoke-SmokeTest {
    Write-Section "4. 스모크 테스트"

    if (-not $SmokeTestUrl) {
        Write-Host "  -SmokeTestUrl 이 없어 건너뜀." -ForegroundColor Yellow
        return
    }

    $failed = 0
    foreach ($path in $SmokeTestPaths) {
        $url = $SmokeTestUrl.TrimEnd('/') + $path
        try {
            $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 15
            if ($response.StatusCode -eq 200) {
                Write-Host "  [OK] $url -> $($response.StatusCode)" -ForegroundColor Green
            }
            else {
                Write-Host "  [FAIL] $url -> $($response.StatusCode)" -ForegroundColor Red
                $failed++
            }
        }
        catch {
            Write-Host "  [FAIL] $url -> $($_.Exception.Message)" -ForegroundColor Red
            $failed++
        }
    }

    if ($failed -gt 0) {
        Write-Host ""
        Write-Host "스모크 테스트 $failed 건 실패." -ForegroundColor Red
        exit 1
    }

    Write-Host ""
    Write-Host "스모크 테스트 전부 통과. 브라우저 콘솔 에러는 이 스크립트가 볼 수 없으니 직접 확인하라." -ForegroundColor Green
}

# --- 실행 -------------------------------------------------------------------

if (-not $SkipPreflight) {
    Invoke-Preflight
}

if ($CheckOnly) {
    Write-Host ""
    Write-Host "사전 조건만 확인했다 (-CheckOnly)." -ForegroundColor Cyan
    exit 0
}

if (-not $SkipPublish) {
    Invoke-Publish
}

if ($RegisterSite) {
    Invoke-RegisterSite
}

if (-not $SkipSmokeTest) {
    Invoke-SmokeTest
}

