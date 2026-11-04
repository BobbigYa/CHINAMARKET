param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [Parameter(Mandatory = $true)][string]$EvidenceLabel
)

$ErrorActionPreference = "Stop"
if ($env:GITHUB_ACTIONS -ne "true") { throw "视觉验收只允许在隔离的 GitHub Actions Windows Runner 中执行。" }

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class TposVisualNative {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out RECT value, int size);
    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);
}
"@

$script:Process = $null
$script:Window = $null
$resolvedExe = (Resolve-Path $ExePath).Path
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

function Wait-MainWindow {
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $script:Process.Id)
    for ($attempt = 0; $attempt -lt 150; $attempt++) {
        if ($script:Process.HasExited) { throw "TPOS^ 意外退出：$($script:Process.ExitCode)" }
        $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
            [System.Windows.Automation.TreeScope]::Children, $condition)
        if ($null -ne $window) { return $window }
        Start-Sleep -Milliseconds 200
    }
    throw "等待主窗口超时。"
}

function Find-Element([string]$AutomationId) {
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $AutomationId)
    return $script:Window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

function Wait-Element([string]$AutomationId) {
    for ($attempt = 0; $attempt -lt 250; $attempt++) {
        if ($script:Process.HasExited) { throw "等待 $AutomationId 时 TPOS^ 意外退出。" }
        $element = Find-Element $AutomationId
        if ($null -ne $element) { return $element }
        Start-Sleep -Milliseconds 100
    }
    throw "等待 $AutomationId 超时。"
}

function Invoke-Button([string]$AutomationId) {
    $element = Wait-Element $AutomationId
    $pattern = [System.Windows.Automation.InvokePattern]$element.GetCurrentPattern(
        [System.Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke()
}

function Set-Text([string]$AutomationId, [string]$Text) {
    $element = Wait-Element $AutomationId
    $element.SetFocus()
    $pattern = [System.Windows.Automation.ValuePattern]$element.GetCurrentPattern(
        [System.Windows.Automation.ValuePattern]::Pattern)
    $pattern.SetValue($Text)
}

function Open-Application {
    $script:Process = Start-Process -FilePath $resolvedExe -WorkingDirectory (Split-Path $resolvedExe) -PassThru
    $script:Window = Wait-MainWindow
    Wait-Element "LoginView" | Out-Null
}

function Close-Application {
    Invoke-Button "CloseWindowButton"
    if (-not $script:Process.WaitForExit(10000)) {
        $script:Process.Kill()
        throw "TPOS^ 未在 10 秒内退出。"
    }
    $exitCode = $script:Process.ExitCode
    $script:Process.Dispose()
    $script:Process = $null
    $script:Window = $null
    return $exitCode
}

function Get-MemorySample([string]$Stage) {
    $script:Process.Refresh()
    return [pscustomobject]@{
        Stage = $Stage; TimestampUtc = [DateTimeOffset]::UtcNow.ToString("O")
        WorkingSetBytes = $script:Process.WorkingSet64; PrivateMemoryBytes = $script:Process.PrivateMemorySize64
        HandleCount = $script:Process.HandleCount; ThreadCount = $script:Process.Threads.Count
    }
}

function Get-GpuSample([string]$Stage) {
    try {
        $samples = (Get-Counter '\GPU Engine(*)\Utilization Percentage' -ErrorAction Stop).CounterSamples |
            Where-Object { $_.InstanceName -match "pid_$($script:Process.Id)(_|$)" -and $_.CookedValue -gt 0 }
        $value = ($samples | Measure-Object -Property CookedValue -Sum).Sum
        if ($null -eq $value) { $value = 0.0 }
        return [pscustomobject]@{ Stage = $Stage; Available = $true; UtilizationPercent = [math]::Round($value, 4); Engines = @($samples).Count }
    }
    catch { return [pscustomobject]@{ Stage = $Stage; Available = $false; UtilizationPercent = $null; Detail = $_.Exception.Message } }
}

function Get-NativeWindowEvidence {
    $hwnd = [IntPtr]$script:Window.Current.NativeWindowHandle
    $backdrop = 0; $corner = 0
    $backdropHresult = [TposVisualNative]::DwmGetWindowAttribute($hwnd, 38, [ref]$backdrop, 4)
    $cornerHresult = [TposVisualNative]::DwmGetWindowAttribute($hwnd, 33, [ref]$corner, 4)
    if ($backdropHresult -eq 0 -and $backdrop -ne 1) { throw "DWM 背景不是 None，实际值 $backdrop。" }
    if ($cornerHresult -eq 0 -and $corner -ne 1) { throw "DWM 窗口角不是 DoNotRound，实际值 $corner。" }
    return [pscustomobject]@{
        ReportedWindowStatus = $script:Window.Current.ItemStatus
        BackdropAttributeHresult = $backdropHresult; BackdropAttributeValue = $backdrop
        CornerAttributeHresult = $cornerHresult; CornerAttributeValue = $corner
    }
}

function Get-LoginLayoutEvidence {
    $windowRect = $script:Window.Current.BoundingRectangle
    $items = [System.Collections.Generic.List[object]]::new()
    foreach ($id in @("TposLogo", "LoginEmail", "LoginPassword", "LoginSubmitButton")) {
        $element = Wait-Element $id
        $rect = $element.Current.BoundingRectangle
        if ($element.Current.IsOffscreen -or $rect.Left -lt $windowRect.Left - 1 -or $rect.Top -lt $windowRect.Top - 1 -or
            $rect.Right -gt $windowRect.Right + 1 -or $rect.Bottom -gt $windowRect.Bottom + 1) {
            throw "$id 未完整显示在登录窗口内。"
        }
        $items.Add([pscustomobject]@{ Id = $id; X = $rect.X; Y = $rect.Y; Width = $rect.Width; Height = $rect.Height })
    }
    $logo = $items | Where-Object Id -eq "TposLogo"
    if ([math]::Abs($logo.Width / $logo.Height - 1) -gt 0.03) { throw "Logo 宽高比例异常。" }
    $scrollBars = $script:Window.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::ScrollBar))
    foreach ($bar in $scrollBars) { if (-not $bar.Current.IsOffscreen) { throw "发现可见滚动条。" } }
    return $items
}

function Capture-Window([string]$Name) {
    $hwnd = [IntPtr]$script:Window.Current.NativeWindowHandle
    $rect = [TposVisualNative+RECT]::new()
    $hresult = [TposVisualNative]::DwmGetWindowAttribute($hwnd, 9, [ref]$rect, [Runtime.InteropServices.Marshal]::SizeOf($rect))
    if ($hresult -ne 0) { throw "无法读取窗口可见边界，HRESULT=$hresult" }
    $width = $rect.Right - $rect.Left; $height = $rect.Bottom - $rect.Top
    if ($width -le 0 -or $height -le 0) { throw "窗口截图尺寸无效。" }
    $bitmap = [System.Drawing.Bitmap]::new($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size)
        $path = Join-Path $OutputDirectory "$Name.png"
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        return [pscustomobject]@{ Name = $Name; Path = $path; X = $rect.Left; Y = $rect.Top; Width = $width; Height = $height }
    }
    finally { $graphics.Dispose(); $bitmap.Dispose() }
}

function Start-MinimizeRecording {
    $ffmpeg = Get-Command ffmpeg -ErrorAction Stop
    $rect = $script:Window.Current.BoundingRectangle
    $videoPath = Join-Path $OutputDirectory "login-minimize-restore.mp4"
    $logPath = Join-Path $OutputDirectory "ffmpeg-minimize-restore.log"
    $arguments = @(
        "-y", "-f", "gdigrab", "-framerate", "30",
        "-offset_x", [int]$rect.X, "-offset_y", [int]$rect.Y,
        "-video_size", "$([int]$rect.Width)x$([int]$rect.Height)",
        "-i", "desktop", "-t", "30", "-an", "-c:v", "libx264",
        "-preset", "veryfast", "-pix_fmt", "yuv420p", $videoPath)
    $process = Start-Process -FilePath $ffmpeg.Source -ArgumentList $arguments -PassThru -WindowStyle Hidden -RedirectStandardError $logPath
    Start-Sleep -Milliseconds 700
    if ($process.HasExited) { throw "ffmpeg 未能开始录屏，详见 $logPath。" }
    return [pscustomobject]@{ Process = $process; Path = $videoPath; LogPath = $logPath }
}

function Enter-KnownTestLogin {
    Set-Text "LoginEmail" "ui-admin@example.test"
    $password = Wait-Element "LoginPassword"
    $password.SetFocus()
    Start-Sleep -Milliseconds 120
    [System.Windows.Forms.SendKeys]::SendWait("UiTest-Password-2026!")
    [System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
}

$report = [ordered]@{
    SchemaVersion = 2; Label = $EvidenceLabel; ExePath = $resolvedExe
    StartedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    Environment = [ordered]@{
        OsCaption = (Get-CimInstance Win32_OperatingSystem).Caption; OsVersion = [Environment]::OSVersion.VersionString
        OsBuild = [Environment]::OSVersion.Version.Build; Is64BitOperatingSystem = [Environment]::Is64BitOperatingSystem
        Cpu = (Get-CimInstance Win32_Processor | Select-Object -First 1).Name; LogicalProcessors = [Environment]::ProcessorCount
        TotalPhysicalMemoryBytes = (Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory
        ScreenWidth = [System.Windows.Forms.SystemInformation]::PrimaryMonitorSize.Width
        ScreenHeight = [System.Windows.Forms.SystemInformation]::PrimaryMonitorSize.Height
        RunnerName = $env:RUNNER_NAME; IsVirtualizedCi = $true
    }
    Screenshots = @(); WindowChecks = [System.Collections.Generic.List[object]]::new()
    Memory = [System.Collections.Generic.List[object]]::new(); Gpu = [System.Collections.Generic.List[object]]::new()
    ReopenCycles = [System.Collections.Generic.List[object]]::new()
}

try {
    $graphics = [System.Drawing.Graphics]::FromHwnd([IntPtr]::Zero)
    try {
        $report.Environment.DpiX = $graphics.DpiX; $report.Environment.DpiY = $graphics.DpiY
        $report.Environment.ActualScalingPercent = [math]::Round($graphics.DpiX / 96 * 100)
    }
    finally { $graphics.Dispose() }
    $report.DpiCoverage = [pscustomobject]@{
        RequestedPercent = @(100, 125, 150, 200); ActualPercent = $report.Environment.ActualScalingPercent
        Status = "OnlyActualRunnerDpi"
        Note = "未通过缩放截图模拟其他 DPI；四档真实系统缩放需人工 Windows 10/11 或可切换 DPI 的 Runner。"
    }

    Open-Application
    Start-Sleep -Seconds 2
    $report.NativeWindow = Get-NativeWindowEvidence
    $report.LoginLayout = Get-LoginLayoutEvidence
    $report.Screenshots += Capture-Window "01-login-shapewaves"
    $report.Memory.Add((Get-MemorySample "启动后登录窗口"))
    $report.Gpu.Add((Get-GpuSample "启动后"))

    $script:Process.Refresh(); $idleCpuStart = $script:Process.TotalProcessorTime
    $idleWatch = [Diagnostics.Stopwatch]::StartNew(); Start-Sleep -Seconds 60
    $script:Process.Refresh(); $idleWatch.Stop()
    $idleCpu = ($script:Process.TotalProcessorTime - $idleCpuStart).TotalSeconds
    $report.Idle60Seconds = [pscustomobject]@{
        ElapsedSeconds = [math]::Round($idleWatch.Elapsed.TotalSeconds, 3); ProcessCpuSeconds = [math]::Round($idleCpu, 4)
        CpuPercentOfOneCore = [math]::Round(100 * $idleCpu / $idleWatch.Elapsed.TotalSeconds, 4)
    }
    $report.Memory.Add((Get-MemorySample "动态背景运行60秒后")); $report.Gpu.Add((Get-GpuSample "动态背景60秒后"))

    $recording = Start-MinimizeRecording
    $script:Process.Refresh(); $transitionCpuStart = $script:Process.TotalProcessorTime
    $transitionWatch = [Diagnostics.Stopwatch]::StartNew()
    $windowPattern = [System.Windows.Automation.WindowPattern]$script:Window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
    for ($cycle = 1; $cycle -le 10; $cycle++) {
        Invoke-Button "MinimizeWindowButton"; Start-Sleep -Milliseconds 650
        if ($windowPattern.Current.WindowVisualState -ne [System.Windows.Automation.WindowVisualState]::Minimized) { throw "第 $cycle 次未最小化。" }
        $windowPattern.SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Normal)
        Start-Sleep -Milliseconds 750
        if ($windowPattern.Current.WindowVisualState -ne [System.Windows.Automation.WindowVisualState]::Normal) { throw "第 $cycle 次未还原。" }
        $report.WindowChecks.Add([pscustomobject]@{ Cycle = $cycle; Minimize = "Passed"; Restore = "Passed" })
        if ($cycle -eq 1) { $report.Screenshots += Capture-Window "02-after-first-restore" }
        if ($cycle -eq 10) { $report.Screenshots += Capture-Window "03-after-tenth-restore"; $report.RestoredNativeWindow = Get-NativeWindowEvidence }
    }
    $script:Process.Refresh(); $transitionWatch.Stop()
    $transitionCpu = ($script:Process.TotalProcessorTime - $transitionCpuStart).TotalSeconds
    $report.TransitionCpu = [pscustomobject]@{
        ElapsedSeconds = [math]::Round($transitionWatch.Elapsed.TotalSeconds, 3); ProcessCpuSeconds = [math]::Round($transitionCpu, 4)
        CpuPercentOfOneCore = [math]::Round(100 * $transitionCpu / $transitionWatch.Elapsed.TotalSeconds, 4)
        Note = "包含 10 次最小化／还原和 UI Automation；ffmpeg 是独立进程，未计入 TPOS CPU。"
    }
    if (-not $recording.Process.WaitForExit(40000)) { $recording.Process.Kill(); throw "ffmpeg 录屏未正常结束。" }
    if ($recording.Process.ExitCode -ne 0 -or -not (Test-Path $recording.Path)) { throw "ffmpeg 录屏失败。" }
    $video = Get-Item $recording.Path
    if ($video.Length -le 1000) { throw "录屏文件为空或异常小。" }
    $report.Recording = [pscustomobject]@{
        Path = $video.FullName; Bytes = $video.Length; SHA256 = (Get-FileHash $video.FullName -Algorithm SHA256).Hash
        DurationSecondsRequested = 30; MinimizeRestoreCycles = 10; Capture = "ffmpeg gdigrab actual desktop region"
    }

    Invoke-Button "MinimizeWindowButton"; $script:Process.Refresh(); $minCpuStart = $script:Process.TotalProcessorTime
    $minWatch = [Diagnostics.Stopwatch]::StartNew(); Start-Sleep -Seconds 10
    $script:Process.Refresh(); $minWatch.Stop()
    $minCpu = ($script:Process.TotalProcessorTime - $minCpuStart).TotalSeconds
    if ($windowPattern.Current.WindowVisualState -ne [System.Windows.Automation.WindowVisualState]::Minimized) { throw "最小化状态不正确。" }
    $report.Minimized10Seconds = [pscustomobject]@{
        Outcome = "Passed"; ProcessCpuSeconds = [math]::Round($minCpu, 4)
        CpuPercentOfOneCore = [math]::Round(100 * $minCpu / $minWatch.Elapsed.TotalSeconds, 4)
    }
    $report.Memory.Add((Get-MemorySample "最小化10秒后")); $report.Gpu.Add((Get-GpuSample "最小化10秒后"))
    $windowPattern.SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Normal); Start-Sleep -Seconds 1

    Enter-KnownTestLogin; Wait-Element "DashboardView" | Out-Null
    Invoke-Button "LogoutButton"; Wait-Element "LoginView" | Out-Null
    $report.AuthenticationRoundTrip = "Passed"
    $report.Screenshots += Capture-Window "04-after-login-logout"
    $report.FinalExitCode = Close-Application

    $report.Outcome = "Passed"; $report.CompletedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    $report | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $OutputDirectory "visual-performance-results.json") -Encoding UTF8
    Write-Host "::notice title=TPOS 窗口与录屏::$EvidenceLabel；10 次最小化／还原通过；MP4 $($video.Length) 字节；SHA-256 $($report.Recording.SHA256)。"
    Write-Host "::notice title=TPOS 实测环境::$($report.Environment.OsCaption)；DPI $($report.Environment.DpiX)（$($report.Environment.ActualScalingPercent)%）；屏幕 $($report.Environment.ScreenWidth)x$($report.Environment.ScreenHeight)。未模拟其余 DPI。"
    Write-Host "::notice title=TPOS 资源实测::60 秒动态背景 CPU（单核当量）$($report.Idle60Seconds.CpuPercentOfOneCore)%；最小化10秒 CPU $($report.Minimized10Seconds.CpuPercentOfOneCore)%。"
}
catch {
    $report.Outcome = "Failed"; $report.Error = $_.Exception.Message
    $report | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $OutputDirectory "visual-performance-results.json") -Encoding UTF8
    Write-Host "::error title=TPOS 视觉验收失败::$($_.Exception.Message)"
    if ($null -ne $script:Process -and -not $script:Process.HasExited) { $script:Process.Kill() }
    exit 1
}
