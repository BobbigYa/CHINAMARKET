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
$testEmail = "ui-admin@example.test"
$testPassword = "UiTest-Password-2026!"

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
        if ($script:Process.HasExited) { throw "应用程序意外退出，退出码：$($script:Process.ExitCode)" }
        $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
            [System.Windows.Automation.TreeScope]::Children, $condition)
        if ($null -ne $window) { return $window }
        Start-Sleep -Milliseconds 200
    }
    throw "等待 TPOS^ 主窗口超时。"
}

function Find-Element([string]$AutomationId) {
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $AutomationId)
    return $script:Window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

function Wait-Element([string]$AutomationId, [int]$TimeoutSeconds = 25) {
    for ($attempt = 0; $attempt -lt ($TimeoutSeconds * 10); $attempt++) {
        if ($script:Process.HasExited) { throw "等待 $AutomationId 时应用程序意外退出。" }
        $element = Find-Element $AutomationId
        if ($null -ne $element) { return $element }
        Start-Sleep -Milliseconds 100
    }
    throw "等待界面元素 $AutomationId 超时。"
}

function Assert-ElementMissing([string]$AutomationId) {
    if ($null -ne (Find-Element $AutomationId)) { throw "界面元素 $AutomationId 不应出现。" }
}

function Set-Text([string]$AutomationId, [string]$Text) {
    $element = Wait-Element $AutomationId
    $element.SetFocus()
    $pattern = [System.Windows.Automation.ValuePattern]$element.GetCurrentPattern(
        [System.Windows.Automation.ValuePattern]::Pattern)
    $pattern.SetValue($Text)
    Start-Sleep -Milliseconds 100
}

function Enter-Password([string]$AutomationId, [string]$Password) {
    $element = Wait-Element $AutomationId
    $element.SetFocus()
    Start-Sleep -Milliseconds 120
    [System.Windows.Forms.SendKeys]::SendWait("^a{DELETE}")
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

function Assert-LoginLayout {
    $started = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    Wait-Element "LoginView" | Out-Null
    $windowRect = $script:Window.Current.BoundingRectangle
    foreach ($id in @("TposLogo", "LoginEmail", "LoginPassword", "LoginSubmitButton")) {
        $element = Wait-Element $id
        $rect = $element.Current.BoundingRectangle
        if ($element.Current.IsOffscreen -or $rect.Width -le 0 -or $rect.Height -le 0) {
            throw "$id 被裁切或离屏：$rect"
        }
        if ($rect.Left -lt $windowRect.Left - 1 -or $rect.Top -lt $windowRect.Top - 1 -or
            $rect.Right -gt $windowRect.Right + 1 -or $rect.Bottom -gt $windowRect.Bottom + 1) {
            throw "$id 超出窗口可视边界：元素 $rect；窗口 $windowRect"
        }
    }
    $logoRect = (Wait-Element "TposLogo").Current.BoundingRectangle
    $ratio = $logoRect.Width / $logoRect.Height
    if ([math]::Abs($ratio - 1.0) -gt 0.03) { throw "Logo 比例被改变：$ratio" }
    if ((Wait-Element "LoginSubmitButton").Current.Name -ne "登录") { throw "登录按钮文字不是“登录”。" }
    foreach ($id in @("ChangePasswordButton", "ChangePasswordLink", "ShopName", "StoreName", "RegistrationCode", "Captcha", "ForgotPassword")) {
        Assert-ElementMissing $id
    }
    $scrollBars = $script:Window.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::ScrollBar))
    foreach ($scrollBar in $scrollBars) {
        if (-not $scrollBar.Current.IsOffscreen) { throw "登录窗口出现可见滚动条。" }
    }
    Add-Pass "Logo、邮箱、密码、登录按钮完整可见且无滚动条" $started
}

function Assert-PasswordReveal {
    $started = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    Set-Text "LoginEmail" $testEmail
    Enter-Password "LoginPassword" $testPassword
    Invoke-Button "RevealPasswordButton"
    $visible = Wait-Element "LoginVisiblePassword"
    $valuePattern = [System.Windows.Automation.ValuePattern]$visible.GetCurrentPattern(
        [System.Windows.Automation.ValuePattern]::Pattern)
    if ($valuePattern.Current.Value -ne $testPassword) { throw "密码显示功能未保留输入内容。" }
    Invoke-Button "RevealPasswordButton"
    Assert-ElementMissing "LoginVisiblePassword"
    Add-Pass "密码默认隐藏且显示／隐藏保持输入内容" $started
}

function Assert-WrongEmail {
    $started = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    Set-Text "LoginEmail" "other@example.test"
    Enter-Password "LoginPassword" $testPassword
    [System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
    $message = Wait-Element "LoginMessage"
    if ($message.Current.Name -notmatch "邮箱或密码不正确") { throw "错误邮箱提示不符合预期：$($message.Current.Name)" }
    Assert-ElementMissing "DashboardView"
    Add-Pass "错误邮箱不能进入 Dashboard 且计入失败提示" $started
}

function Assert-WrongPassword {
    $started = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    Set-Text "LoginEmail" $testEmail
    Enter-Password "LoginPassword" "UiTest-Wrong-Password!"
    [System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
    $message = Wait-Element "LoginMessage"
    if ($message.Current.Name -notmatch "邮箱或密码不正确") { throw "错误密码提示不符合预期：$($message.Current.Name)" }
    Assert-ElementMissing "DashboardView"
    Add-Pass "错误密码不能进入 Dashboard" $started
}

function Assert-CorrectLogin {
    $started = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    Set-Text "LoginEmail" $testEmail
    Enter-Password "LoginPassword" $testPassword
    [System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
    Wait-Element "DashboardView" | Out-Null
    Add-Pass "正确邮箱和密码通过 Enter 进入 Dashboard（生产 Argon2id）" $started
}

function Assert-Logout {
    $started = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    Invoke-Button "LogoutButton"
    Wait-Element "LoginView" | Out-Null
    Assert-ElementMissing "DashboardView"
    Add-Pass "退出登录后必须重新认证" $started
}

try {
    if (Test-Path $dataRoot) { Remove-Item $dataRoot -Recurse -Force }

    Open-Application
    $started = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    Wait-Element "SetupView" | Out-Null
    Set-Text "SetupEmail" $testEmail
    Enter-Password "SetupNewPassword" $testPassword
    Enter-Password "SetupPasswordConfirmation" $testPassword
    Invoke-Button "SetupSubmitButton"
    Wait-Element "LoginView" | Out-Null
    Add-Pass "首次初始化同时设置邮箱与密码并返回登录页" $started

    Assert-LoginLayout
    Assert-PasswordReveal
    Assert-WrongEmail
    Assert-WrongPassword
    Assert-CorrectLogin
    Assert-Logout
    Close-Application "完全关闭发布的 EXE"

    Open-Application
    $started = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    Wait-Element "LoginView" | Out-Null
    Assert-ElementMissing "SetupView"
    Assert-ElementMissing "DashboardView"
    Assert-LoginLayout
    Add-Pass "重新启动 EXE 后要求邮箱和密码且不自动进入 Dashboard" $started
    Assert-CorrectLogin
    Assert-Logout
    Close-Application "第二次完全关闭发布的 EXE"

    $started = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $databasePath = Join-Path $dataRoot "Security\tpos-auth.db"
    if (-not (Test-Path $databasePath)) { throw "未在预期位置找到生产数据库：$databasePath" }
    $acl = Get-Acl $databasePath
    if (-not $acl.AreAccessRulesProtected) { throw "数据库仍允许继承访问规则。" }
    foreach ($rule in $acl.Access) {
        $sid = $rule.IdentityReference.Translate([System.Security.Principal.SecurityIdentifier]).Value
        if (@("S-1-5-32-545", "S-1-1-0") -contains $sid) { throw "数据库 ACL 包含 Users/Everyone 权限：$sid" }
    }
    Write-Host "DATABASE_PATH=$databasePath"
    Write-Host "DATABASE_SDDL=$($acl.Sddl)"
    Add-Pass "数据库位置与显式 ACL 检查" $started

    New-Item -ItemType Directory -Path "artifacts" -Force | Out-Null
    $script:Results | ConvertTo-Json -Depth 3 | Set-Content "artifacts/ui-smoke-results.json" -Encoding UTF8
    Write-Host "::notice title=TPOS EXE 验收汇总::$($script:Results.Count) 项真实 EXE 自动化检查全部通过。"
}
catch {
    if ($null -ne $script:Process -and -not $script:Process.HasExited) { $script:Process.Kill() }
    Write-Host "::error title=TPOS EXE 验收失败::$($_.Exception.Message)"
    exit 1
}
