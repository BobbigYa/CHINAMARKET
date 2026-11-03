param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [switch]$ResetTestData
)

$ErrorActionPreference = "Stop"
if ($env:GITHUB_ACTIONS -ne "true" -or -not $ResetTestData) {
    throw "此脚本会重置测试数据，只允许在隔离的 GitHub Actions Windows Runner 中使用 -ResetTestData。"
}

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms

$script:Process = $null
$script:Window = $null
$script:Results = [System.Collections.Generic.List[object]]::new()
$resolvedExe = (Resolve-Path $ExePath).Path
$dataRoot = Join-Path $env:ProgramData "Tpos"

function Add-Pass([string]$Name, [long]$StartedAt) {
    $duration = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds() - $StartedAt
    $script:Results.Add([pscustomobject]@{ Name = $Name; Outcome = "Passed"; DurationMs = $duration })
    Write-Host "PASS: $Name ($duration ms)"
}

function Wait-MainWindow {
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
        $script:Process.Id)
    for ($attempt = 0; $attempt -lt 150; $attempt++) {
        if ($script:Process.HasExited) {
            throw "应用程序意外退出，退出码：$($script:Process.ExitCode)"
        }
        $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
            [System.Windows.Automation.TreeScope]::Children,
            $condition)
        if ($null -ne $window) { return $window }
        Start-Sleep -Milliseconds 200
    }
    throw "等待 TPOS^ 主窗口超时。"
}

function Wait-Element([string]$AutomationId, [int]$TimeoutSeconds = 20) {
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        $AutomationId)
    for ($attempt = 0; $attempt -lt ($TimeoutSeconds * 10); $attempt++) {
        if ($script:Process.HasExited) {
            throw "等待 $AutomationId 时应用程序意外退出，退出码：$($script:Process.ExitCode)"
        }
        $element = $script:Window.FindFirst(
            [System.Windows.Automation.TreeScope]::Descendants,
            $condition)
        if ($null -ne $element) { return $element }
        Start-Sleep -Milliseconds 100
    }
    throw "等待界面元素 $AutomationId 超时。"
}

function Assert-ElementMissing([string]$AutomationId) {
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        $AutomationId)
    $element = $script:Window.FindFirst(
        [System.Windows.Automation.TreeScope]::Descendants,
        $condition)
    if ($null -ne $element) {
        throw "界面元素 $AutomationId 不应出现。"
    }
}

function Enter-Password([string]$AutomationId, [string]$Password) {
    $element = Wait-Element $AutomationId
    $element.SetFocus()
    Start-Sleep -Milliseconds 150
    [System.Windows.Forms.SendKeys]::SendWait($Password)
}

function Invoke-Button([string]$AutomationId) {
    $element = Wait-Element $AutomationId
    $pattern = [System.Windows.Automation.InvokePattern]$element.GetCurrentPattern(
        [System.Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke()
}

function Open-Application {
    $script:Process = Start-Process -FilePath $resolvedExe -WorkingDirectory (Split-Path $resolvedExe) -PassThru
    $script:Window = Wait-MainWindow
}

function Close-Application([string]$EvidenceName) {
    $started = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    Invoke-Button "CloseWindowButton"
    if (-not $script:Process.WaitForExit(10000)) {
        $script:Process.Kill()
        throw "应用程序未能在 10 秒内完全关闭。"
    }
    Add-Pass $EvidenceName $started
    $script:Process.Dispose()
    $script:Process = $null
    $script:Window = $null
}

function Assert-WrongPassword {
    $started = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    Enter-Password "LoginPassword" "UiTest-Wrong-Password!"
    Invoke-Button "LoginSubmitButton"
    $message = Wait-Element "LoginMessage"
    if ($message.Current.Name -notmatch "密码不正确") {
        throw "错误密码提示不符合预期：$($message.Current.Name)"
    }
    Assert-ElementMissing "DashboardView"
    Add-Pass "错误密码不能进入 Dashboard" $started
}

function Assert-CorrectPassword {
    $started = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    Enter-Password "LoginPassword" "UiTest-Password-2026!"
    Invoke-Button "LoginSubmitButton"
    Wait-Element "DashboardView" | Out-Null
    Add-Pass "正确密码进入 Dashboard（生产 Argon2id 参数）" $started
}

function Assert-Logout {
    $started = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    Invoke-Button "LogoutButton"
    Wait-Element "LoginView" | Out-Null
    Assert-ElementMissing "DashboardView"
    Add-Pass "退出登录后必须重新输入密码" $started
}

try {
    if (Test-Path $dataRoot) {
        Remove-Item $dataRoot -Recurse -Force
    }

    Open-Application
    $started = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    Wait-Element "SetupView" | Out-Null
    Enter-Password "SetupNewPassword" "UiTest-Password-2026!"
    Enter-Password "SetupPasswordConfirmation" "UiTest-Password-2026!"
    Invoke-Button "SetupSubmitButton"
    Wait-Element "LoginView" | Out-Null
    Add-Pass "首次初始化后返回登录页" $started

    Assert-WrongPassword
    Assert-CorrectPassword
    Assert-Logout
    Close-Application "完全关闭发布的 EXE"

    Open-Application
    $started = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    Wait-Element "LoginView" | Out-Null
    Assert-ElementMissing "SetupView"
    Assert-ElementMissing "DashboardView"
    Add-Pass "重新启动 EXE 后要求密码且不自动进入 Dashboard" $started

    Assert-WrongPassword
    Assert-CorrectPassword
    Assert-Logout
    Close-Application "第二次完全关闭发布的 EXE"

    $started = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $databasePath = Join-Path $dataRoot "Security\tpos-auth.db"
    if (-not (Test-Path $databasePath)) {
        throw "未在预期位置找到生产数据库：$databasePath"
    }
    $acl = Get-Acl $databasePath
    if (-not $acl.AreAccessRulesProtected) {
        throw "数据库仍允许继承访问规则，未达到显式 ACL 要求。"
    }
    $forbiddenSids = @("S-1-5-32-545", "S-1-1-0")
    foreach ($rule in $acl.Access) {
        $sid = $rule.IdentityReference.Translate([System.Security.Principal.SecurityIdentifier]).Value
        if ($forbiddenSids -contains $sid) {
            throw "数据库 ACL 包含不应存在的 Users/Everyone 权限：$sid"
        }
    }
    Write-Host "DATABASE_PATH=$databasePath"
    Write-Host "DATABASE_SDDL=$($acl.Sddl)"
    Add-Pass "数据库位置与显式 ACL 检查" $started

    New-Item -ItemType Directory -Path "artifacts" -Force | Out-Null
    $script:Results | ConvertTo-Json -Depth 3 | Set-Content "artifacts/ui-smoke-results.json" -Encoding UTF8
    Write-Host "::notice title=TPOS EXE 验收汇总::$($script:Results.Count) 项真实 EXE 自动化检查全部通过。"
}
catch {
    if ($null -ne $script:Process -and -not $script:Process.HasExited) {
        $script:Process.Kill()
    }
    Write-Host "::error title=TPOS EXE 验收失败::$($_.Exception.Message)"
    exit 1
}
