param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [Parameter(Mandatory = $true)][string]$EvidenceLabel
)

$ErrorActionPreference = "Stop"
if ($env:GITHUB_ACTIONS -ne "true") {
    throw "视觉和性能验收只允许在隔离的 GitHub Actions Windows Runner 中执行。"
}

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
    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)] out bool enabled);
}
"@

$script:Process = $null
$script:Window = $null
$resolvedExe = (Resolve-Path $ExePath).Path
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

function Wait-MainWindow {
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
        $script:Process.Id)
    for ($attempt = 0; $attempt -lt 150; $attempt++) {
        if ($script:Process.HasExited) { throw "TPOS^ 意外退出：$($script:Process.ExitCode)" }
        $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
            [System.Windows.Automation.TreeScope]::Children, $condition)
        if ($null -ne $window) { return $window }
        Start-Sleep -Milliseconds 200
    }
    throw "等待主窗口超时。"
}

function Wait-Element([string]$AutomationId) {
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $AutomationId)
    for ($attempt = 0; $attempt -lt 200; $attempt++) {
        if ($script:Process.HasExited) { throw "等待 $AutomationId 时 TPOS^ 意外退出。" }
        $element = $script:Window.FindFirst(
            [System.Windows.Automation.TreeScope]::Descendants, $condition)
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
        Stage = $Stage
        TimestampUtc = [DateTimeOffset]::UtcNow.ToString("O")
        WorkingSetBytes = $script:Process.WorkingSet64
        PrivateMemoryBytes = $script:Process.PrivateMemorySize64
        PagedMemoryBytes = $script:Process.PagedMemorySize64
        HandleCount = $script:Process.HandleCount
        ThreadCount = $script:Process.Threads.Count
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
    catch {
        return [pscustomobject]@{ Stage = $Stage; Available = $false; UtilizationPercent = $null; Detail = $_.Exception.Message }
    }
}

function Get-NativeWindowEvidence {
    $hwnd = [IntPtr]$script:Window.Current.NativeWindowHandle
    $backdrop = 0
    $corner = 0
    $backdropHresult = [TposVisualNative]::DwmGetWindowAttribute($hwnd, 38, [ref]$backdrop, 4)
    $cornerHresult = [TposVisualNative]::DwmGetWindowAttribute($hwnd, 33, [ref]$corner, 4)
    $compositionEnabled = $false
    $compositionHresult = [TposVisualNative]::DwmIsCompositionEnabled([ref]$compositionEnabled)
    return [pscustomobject]@{
        DwmCompositionHresult = $compositionHresult
        DwmCompositionEnabled = $compositionEnabled
        BackdropAttributeHresult = $backdropHresult
        BackdropAttributeValue = $backdrop
        CornerAttributeHresult = $cornerHresult
        CornerAttributeValue = $corner
    }
}

function Capture-Window([string]$Name) {
    $hwnd = [IntPtr]$script:Window.Current.NativeWindowHandle
    $rect = [TposVisualNative+RECT]::new()
    $hresult = [TposVisualNative]::DwmGetWindowAttribute(
        $hwnd, 9, [ref]$rect, [Runtime.InteropServices.Marshal]::SizeOf($rect))
    if ($hresult -ne 0) { throw "无法读取窗口可见边界，HRESULT=$hresult" }
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -le 0 -or $height -le 0) { throw "窗口截图尺寸无效：$width x $height" }
    $bitmap = [System.Drawing.Bitmap]::new($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size)
        $path = Join-Path $OutputDirectory "$Name.png"
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        return [pscustomobject]@{ Name = $Name; Path = $path; X = $rect.Left; Y = $rect.Top; Width = $width; Height = $height }
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Enter-KnownTestPassword {
    $password = Wait-Element "LoginPassword"
    $password.SetFocus()
    Start-Sleep -Milliseconds 150
    [System.Windows.Forms.SendKeys]::SendWait("UiTest-Password-2026!")
    [System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
}

$report = [ordered]@{
    SchemaVersion = 1
    Label = $EvidenceLabel
    ExePath = $resolvedExe
    StartedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    Environment = [ordered]@{
        OsCaption = (Get-CimInstance Win32_OperatingSystem).Caption
        OsVersion = [Environment]::OSVersion.VersionString
        OsBuild = [Environment]::OSVersion.Version.Build
        Is64BitOperatingSystem = [Environment]::Is64BitOperatingSystem
        Cpu = (Get-CimInstance Win32_Processor | Select-Object -First 1).Name
        LogicalProcessors = [Environment]::ProcessorCount
        TotalPhysicalMemoryBytes = (Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory
        ScreenWidth = [System.Windows.Forms.SystemInformation]::PrimaryMonitorSize.Width
        ScreenHeight = [System.Windows.Forms.SystemInformation]::PrimaryMonitorSize.Height
        RunnerName = $env:RUNNER_NAME
        IsVirtualizedCi = $true
    }
    Screenshots = @()
    WindowChecks = [System.Collections.Generic.List[object]]::new()
    Memory = [System.Collections.Generic.List[object]]::new()
    Gpu = [System.Collections.Generic.List[object]]::new()
    ReopenCycles = [System.Collections.Generic.List[object]]::new()
}

try {
    $graphics = [System.Drawing.Graphics]::FromHwnd([IntPtr]::Zero)
    try { $report.Environment.DpiX = $graphics.DpiX; $report.Environment.DpiY = $graphics.DpiY }
    finally { $graphics.Dispose() }

    Open-Application
    Start-Sleep -Seconds 2
    $report.NativeWindow = Get-NativeWindowEvidence
    $report.Screenshots += Capture-Window "01-login-normal"
    $report.Memory.Add((Get-MemorySample "启动后登录窗口"))
    $report.Gpu.Add((Get-GpuSample "启动后"))

    $script:Process.Refresh()
    $idleCpuStart = $script:Process.TotalProcessorTime
    $idleWatch = [Diagnostics.Stopwatch]::StartNew()
    Start-Sleep -Seconds 60
    $script:Process.Refresh()
    $idleWatch.Stop()
    $idleCpu = ($script:Process.TotalProcessorTime - $idleCpuStart).TotalSeconds
    $report.Idle60Seconds = [pscustomobject]@{
        ElapsedSeconds = [math]::Round($idleWatch.Elapsed.TotalSeconds, 3)
        ProcessCpuSeconds = [math]::Round($idleCpu, 4)
        CpuPercentOfOneCore = [math]::Round(100 * $idleCpu / $idleWatch.Elapsed.TotalSeconds, 4)
        CpuPercentOfAllLogicalProcessors = [math]::Round(100 * $idleCpu / $idleWatch.Elapsed.TotalSeconds / [Environment]::ProcessorCount, 4)
    }
    $report.Memory.Add((Get-MemorySample "登录窗口静止60秒后"))
    $report.Gpu.Add((Get-GpuSample "静止60秒后"))
    $report.Screenshots += Capture-Window "02-login-idle-60s"

    $script:Process.Refresh()
    $animationCpuStart = $script:Process.TotalProcessorTime
    $animationWatch = [Diagnostics.Stopwatch]::StartNew()
    for ($cycle = 1; $cycle -le 3; $cycle++) {
        Invoke-Button "MaximizeWindowButton"
        Start-Sleep -Milliseconds 700
        $state = [System.Windows.Automation.WindowPattern]$script:Window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
        if ($state.Current.WindowVisualState -ne [System.Windows.Automation.WindowVisualState]::Maximized) {
            throw "第 $cycle 次最大化未达到 Maximized 状态。"
        }
        if ($cycle -eq 1) { $report.Screenshots += Capture-Window "03-maximized" }
        Invoke-Button "MaximizeWindowButton"
        Start-Sleep -Milliseconds 700
        if ($state.Current.WindowVisualState -ne [System.Windows.Automation.WindowVisualState]::Normal) {
            throw "第 $cycle 次恢复未达到 Normal 状态。"
        }
        if ($cycle -eq 1) { $report.Screenshots += Capture-Window "04-restored" }
        $report.WindowChecks.Add([pscustomobject]@{ Cycle = $cycle; Maximize = "Passed"; Restore = "Passed" })
    }
    $script:Process.Refresh()
    $animationWatch.Stop()
    $animationCpu = ($script:Process.TotalProcessorTime - $animationCpuStart).TotalSeconds
    $report.TransitionCpu = [pscustomobject]@{
        ElapsedSeconds = [math]::Round($animationWatch.Elapsed.TotalSeconds, 3)
        ProcessCpuSeconds = [math]::Round($animationCpu, 4)
        CpuPercentOfOneCore = [math]::Round(100 * $animationCpu / $animationWatch.Elapsed.TotalSeconds, 4)
        Note = "包含三次最大化与恢复以及 UI Automation 操作，不是纯 GPU 基准。"
    }
    $report.Gpu.Add((Get-GpuSample "最大化与恢复操作后"))

    Invoke-Button "MinimizeWindowButton"
    $script:Process.Refresh()
    $minCpuStart = $script:Process.TotalProcessorTime
    $minWatch = [Diagnostics.Stopwatch]::StartNew()
    Start-Sleep -Seconds 10
    $script:Process.Refresh()
    $minWatch.Stop()
    $minCpu = ($script:Process.TotalProcessorTime - $minCpuStart).TotalSeconds
    $windowPattern = [System.Windows.Automation.WindowPattern]$script:Window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
    if ($windowPattern.Current.WindowVisualState -ne [System.Windows.Automation.WindowVisualState]::Minimized) {
        throw "最小化后窗口状态不正确。"
    }
    $report.Minimized10Seconds = [pscustomobject]@{
        Outcome = "Passed"
        ProcessCpuSeconds = [math]::Round($minCpu, 4)
        CpuPercentOfOneCore = [math]::Round(100 * $minCpu / $minWatch.Elapsed.TotalSeconds, 4)
    }
    $report.Memory.Add((Get-MemorySample "最小化10秒后"))
    $report.Gpu.Add((Get-GpuSample "最小化10秒后"))
    $windowPattern.SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Normal)
    Start-Sleep -Seconds 1

    Enter-KnownTestPassword
    Wait-Element "DashboardView" | Out-Null
    Invoke-Button "LogoutButton"
    Wait-Element "LoginView" | Out-Null
    $report.AuthenticationRoundTrip = "Passed"
    $report.Screenshots += Capture-Window "05-after-login-logout"
    $report.FinalExitCode = Close-Application

    for ($cycle = 1; $cycle -le 5; $cycle++) {
        Open-Application
        Start-Sleep -Seconds 1
        $memory = Get-MemorySample "连续重启第 $cycle 次"
        $exitCode = Close-Application
        $report.ReopenCycles.Add([pscustomobject]@{ Cycle = $cycle; ExitCode = $exitCode; WorkingSetBytes = $memory.WorkingSetBytes; PrivateMemoryBytes = $memory.PrivateMemoryBytes })
    }

    $report.Outcome = "Passed"
    $report.CompletedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    $report | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $OutputDirectory "visual-performance-results.json") -Encoding UTF8
    Write-Host "::notice title=TPOS 视觉与性能验收::$EvidenceLabel 完成；60 秒静止、窗口状态、截图、认证往返和 5 次重启均已执行。"
}
catch {
    $report.Outcome = "Failed"
    $report.Error = $_.Exception.Message
    $report | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $OutputDirectory "visual-performance-results.json") -Encoding UTF8
    Write-Host "::error title=TPOS 视觉与性能验收失败::$($_.Exception.Message)"
    if ($null -ne $script:Process -and -not $script:Process.HasExited) { $script:Process.Kill() }
    exit 1
}
