# TPOS^ 极简圆角玻璃登录窗口：实现、真实验收与 EXE 交付

> 日期：2026-11-04
> 产品提交：`89e41c93002d9c0f906e3d9b61f6918a278b73d0`
> 最终运行：[GitHub Actions #41834038978](https://github.com/BobbigYa/CHINAMARKET/actions/runs/41834038978)
> 基线运行：[GitHub Actions #41832113470](https://github.com/BobbigYa/CHINAMARKET/actions/runs/41832113470)
> 开源评估：[`login-window-oss-review.md`](./login-window-oss-review.md)

## 1. 交付结论与测试边界

本次只修改登录窗口、共享窗口行为、动画生命周期和相应验收脚本。

- 登录窗口默认 **440×540 DIP**，居中显示，最小 360×400 DIP。
- 保留 TPOS^、一个密码输入框、Enter 登录、密码显隐、必要错误／锁定提示和修改密码入口。
- 移除登录页的欢迎语、说明、重复标签、隐私说明和单独登录按钮。
- 使用用户提供的最终 Logo，应用于登录页、窗口标题栏和 EXE 图标；未重绘 Logo 图形。
- 保留原暗色／金色界面配色及 Aurora 动画；Dashboard 的 XAML 和业务内容未改。
- 未改密码验证、数据库结构、Argon2id 参数、安全策略或任何业务功能。
- 新增 NuGet／运行时 UI 依赖：**0**。

**Windows 10 和 Windows 11 桌面版均未进行人工实机验收。** 本报告的真实结果来自 Windows Server 2025 和 Windows Server 2022 Runner，不能冒充两个桌面操作系统的测试通过。

## 2. 最终 EXE

| 项目 | 值 |
|---|---|
| 下载 | [tpos-win-x64-self-contained](https://github.com/BobbigYa/CHINAMARKET/actions/runs/41834038978/artifacts/13324197590) |
| 文件名称 | `Tpos.Desktop.exe` |
| Runner 实际生成路径 | `artifacts/Tpos-win-x64-self-contained/Tpos.Desktop.exe` |
| 大小 | **66,298,793 字节，约 63.23 MiB** |
| SHA-256 | `8B5DCFEF4E4373D44A8310F43A79F4A89F43A740FEC2A3E0DF76665C8B58A9FF` |
| 架构 | Windows x64 |
| Runtime | .NET 10，self-contained，目标机无需另装 .NET Runtime |
| 形式 | Single-file、压缩、原生库自解压 |
| 签名 | 未进行商业代码签名 |

EXE 位于上述 GitHub Artifact，不在当前 Linux 工作区。Artifact 当前保留至 **2026-11-18**。

核对命令：

```powershell
Get-FileHash .\Tpos.Desktop.exe -Algorithm SHA256
```

## 3. 修改前后真实截图

以下均为真实发布 EXE 的屏幕截图，不是设计稿或 HTML 模拟。

| 版本 | Artifact | 内容 |
|---|---|---|
| 修改前 | [基线 tpos-visual-performance](https://github.com/BobbigYa/CHINAMARKET/actions/runs/41832113470/artifacts/13323745801) | 原 1100×700 登录窗口及窗口操作 |
| 修改后，Server 2025 | [tpos-visual-performance](https://github.com/BobbigYa/CHINAMARKET/actions/runs/41834038978/artifacts/13324941680) | Acrylic 小窗口、最大化、恢复及性能 JSON |
| 修改后，Server 2022 | [tpos-windows-server-2022-visual](https://github.com/BobbigYa/CHINAMARKET/actions/runs/41834038978/artifacts/13324807885) | BlurBehind、原生 Region 圆角及性能 JSON |

每个 Artifact 包含：

```text
01-login-normal.png
02-login-idle-60s.png
03-maximized.png
04-restored.png
05-after-login-logout.png
visual-performance-results.json
```

截图由自动化真实生成；没有人工逐像素审美验收，不能据此声称所有显卡上都没有任何视觉瑕疵。

## 4. 玻璃、圆角与窗口行为

### 4.1 实现路径

- Windows 11 Build 22621+：公开 `DWMWA_SYSTEMBACKDROP_TYPE`，选择 Desktop Acrylic。
- 支持 System Backdrop 前的系统：公开 `DwmEnableBlurBehindWindow`。
- Windows 11：公开 `DWMWA_WINDOW_CORNER_PREFERENCE`，普通状态 Round，最大化 Square，恢复 Round。
- 旧系统：`CreateRoundRectRgn` + `SetWindowRgn`，按 DPI 换算约 22 DIP 圆角；最大化移除 Region，恢复重新创建。
- 高对比度、系统透明效果关闭、DWM 不可用或调用失败：使用不透明降级背景，不崩溃。
- 密码、Logo 和输入框不设置整体透明；透明只用于背景层。
- 最大化通过 `WM_GETMINMAXINFO` 使用当前显示器工作区。
- 保留原生 `WindowState` 和 DWM 窗口动画，没有自制逐帧最小化动画。
- 未使用私有 Accent API、DWM 注入、其他进程修改或管理员级系统修改。

### 4.2 真实运行结果

| 项目 | Server 2025 Build 26100 | Server 2022 Build 20348 |
|---|---|---|
| 实际背景路径 | `Windows11-DesktopAcrylic` | `Documented-DWM-BlurBehind` |
| DWM Backdrop 属性 | 3，Desktop Acrylic | 属性不适用，BlurBehind 调用成功 |
| 启动圆角 | `DWM-Rounded` | `Native-Rounded-Region` |
| 最大化 | `Square-Maximized` | `Square-Maximized` |
| 恢复圆角 | `DWM-Rounded` | `Native-Rounded-Region` |
| 三次最大化／恢复 | 全部通过 | 全部通过 |
| 最小化与恢复 | 通过 | 通过 |
| 登录／退出 | 通过 | 通过 |

四角结论基于真实 DWM 属性、窗口 Region 和截图证据。黑色／白色方角、透明残留的**人工视觉确认尚未完成**；Windows 10／11 桌面版四角也未实测。

原生 DWM 动画路径和窗口状态已真实执行，但没有人工观看高帧率录像，因此不对“所有机器都绝对流畅”作保证。

## 5. 认证保持情况

最终主任务：

- **12 项单元／集成测试：12 通过，0 失败，0 跳过。**
- **12 项真实 EXE UI 检查：全部通过。**
- 两个 Windows Runner 均各自完成相同的 12 项真实 EXE UI 检查。

UI 检查覆盖：

- 首次初始化并返回登录页。
- 密码显示和隐藏保持输入内容。
- 错误密码不能进入 Dashboard。
- 使用 Enter 输入正确密码并进入 Dashboard。
- 退出后必须重新输入密码。
- 两次完全关闭和重新启动，均不自动登录。
- 数据库位置与显式 ACL。

真实数据库继续核验为 Argon2id `m=65536,t=3,p=1`、16 字节盐、32 字节哈希，未发现测试密码明文或未预期敏感日志。Core、Infrastructure、LoginViewModel、DashboardView 和 LocalDataSecurity 文件无差异。

## 6. 真实性能测试

### 6.1 测试条件

| 项目 | 修改前基线 | 修改后 Server 2025 | 修改后 Server 2022 |
|---|---|---|---|
| OS | Windows Server 2025 Datacenter | Windows Server 2025 Datacenter | Windows Server 2022 Datacenter |
| Build | 26100 | 26100 | 20348 |
| CPU | AMD EPYC 9V74 | AMD EPYC 9V74 | AMD EPYC 7763 |
| 逻辑处理器 | 4 | 4 | 4 |
| RAM | 15.99 GiB | 15.99 GiB | 15.99 GiB |
| DPI | 96 | 96 | 96 |
| 分辨率 | 1024×768 | 1024×768 | 1024×768 |
| 条件 | GitHub 虚拟 Runner | GitHub 虚拟 Runner | GitHub 虚拟 Runner |

### 6.2 内存与 CPU 实测

| 指标 | 修改前 | 修改后 Server 2025 | 修改后 Server 2022 |
|---|---:|---:|---:|
| 启动后工作集 | 213.39 MiB | 204.17 MiB | 195.77 MiB |
| 启动后私有内存 | 110.34 MiB | 99.42 MiB | 97.74 MiB |
| 静止 60 秒后工作集 | 212.49 MiB | 202.82 MiB | 195.01 MiB |
| 静止 60 秒后私有内存 | 110.07 MiB | 98.86 MiB | 97.46 MiB |
| 静止 60 秒 CPU，单核当量 | 0% | 0% | 0% |
| 最小化 10 秒后工作集 | 226.28 MiB | 222.88 MiB | 212.16 MiB |
| 最小化 CPU，单核当量 | 0.1562% | 0.1563% | 0.7808% |
| 三次最大化／恢复期间 CPU | 40.6433% | 13.4817% | 11.3406% |

说明：

- CPU 来自进程 `TotalProcessorTime` 差值；0% 表示本次采样精度下未观察到可计 CPU 时间，不是所有硬件上的绝对零占用。
- 窗口切换 CPU 包含 UI Automation；修改后还包含 GPU 计数器采样，不是纯动画基准。
- 修改前后是相同规格、同型号 vCPU 的不同 CI 实例，不是同机同刻严格实验室对比。Server 2025 启动工作集差值为 9.22 MiB，但不能把全部差值都归因于代码。
- 每个环境都真实连续启动、关闭 EXE 5 次，退出正常；逐次工作集和私有内存保存在 `visual-performance-results.json` 的 `ReopenCycles` 中。
- 五次重启不是长期内存泄漏证明，未进行数小时浸泡测试。

### 6.3 GPU 实测

GPU 数值来自该进程的 `GPU Engine ... Utilization Percentage` 计数器，不是整台机器 GPU 占用。

- Server 2025：启动及静止 60 秒后均为 0%；Actions 摘要中可见的最大化／恢复采样范围为 **0.4076%–0.8718%**。
- Server 2022：启动及静止 60 秒后均为 0%；可见窗口操作采样范围为 **0%–1.0103%**。
- 每次最大化／恢复的原始采样和引擎信息保存在 JSON；Actions 注释放不下全部条目时，以 JSON 为准。

这些数字只代表虚拟 Runner，不能直接外推到收银机核显或远程桌面。

## 7. 低资源实现

- Aurora 和 Logo 保留原动画，20 秒无输入后暂停；交互后继续。
- 最小化或失去焦点时立即暂停，恢复后按需继续。
- 只新增一个 20 秒单次空闲计时器，不是高频刷新。
- 锁定倒计时保留原有必要的一秒计时器，未改安全逻辑。
- 不在每帧创建 Bitmap、Brush、Visual 或背景截图。
- Logo 只加载压缩后的共享 PNG 资源，原图约 1024×1024，处理后 PNG 为 256×256、30,207 字节。
- 没有新增大型控件库、字体包、WebView2、WinUI 3 或图像运行时。
- 原生窗口动画交给 DWM；窗口静止后不运行自制动画循环。

“动画已暂停”属于代码路径分析；60 秒资源数值是真实测量，两者没有混为一谈。

## 8. DPI、分辨率与操作系统未测项

真实测试只有 **96 DPI、1024×768** 及各自最大化工作区。

以下未实测：

- 125%、150%、200% DPI。
- 1920×1080、2K、4K、多显示器和显示器间拖动。
- Windows 10 x64 桌面版。
- Windows 11 x64 桌面版。
- 门店触摸屏、远程桌面和真实核显。
- 人工检查透明残留、阴影、动画观感和所有显卡上的圆角抗锯齿。

应用已声明 PerMonitorV2，并在 DPI 变化时更新圆角 Region，但这属于兼容实现，不等同于上述 DPI 已通过。

## 9. GitHub 复用与许可证

实际复用少量代码模式：

- [ModernWpf](https://github.com/Kinnara/ModernWpf)，MIT：Windows 11 System Backdrop。
- [picoe/Eto](https://github.com/picoe/Eto)，BSD-3-Clause：公开 BlurBehind WPF 调用。
- [FluentWpfCore](https://github.com/TwilightLemon/FluentWpfCore)，MIT：原生 DWM 窗口动画样式。

完整候选比较见 [`login-window-oss-review.md`](./login-window-oss-review.md)。许可证原文位于 `src/Tpos.Desktop/THIRD-PARTY-NOTICES.txt`，并嵌入 EXE。未引入这些项目的完整 UI 包。

## 10. 修改文件清单

```text
.github/workflows/tpos-windows-tests.yml
scripts/windows-ui-smoke.ps1
scripts/windows-visual-performance.ps1
src/Tpos.Desktop/Assets/TposLogo.ico
src/Tpos.Desktop/Assets/TposLogo.png
src/Tpos.Desktop/Controls/AuroraBackground.xaml
src/Tpos.Desktop/Controls/AuroraBackground.xaml.cs
src/Tpos.Desktop/MainWindow.xaml
src/Tpos.Desktop/MainWindow.xaml.cs
src/Tpos.Desktop/Services/NativeWindowManager.cs
src/Tpos.Desktop/THIRD-PARTY-NOTICES.txt
src/Tpos.Desktop/Tpos.Desktop.csproj
src/Tpos.Desktop/Views/AuthenticationView.xaml
src/Tpos.Desktop/Views/AuthenticationView.xaml.cs
src/Tpos.Desktop/Views/LoginView.xaml
src/Tpos.Desktop/Views/LoginView.xaml.cs
docs/tpos/login-window-oss-review.md
docs/tpos/login-window-final-report.md
```

## 11. 建议的人工验收清单

在真实 Windows 10 和 Windows 11 电脑上分别执行：

1. 核对 EXE SHA-256 并启动，检查首次初始化和登录。
2. 在 100%、150%、200% DPI 下检查四角、阴影、文字清晰度和输入框。
3. 在桌面复杂背景上观察 Acrylic／BlurBehind，确认没有黑白方角。
4. 连续最小化、最大化、恢复，检查任务栏和工作区。
5. 在多显示器间拖动并再次最大化。
6. 静止 60 秒并用任务管理器记录 CPU、内存和 GPU。
7. 验证错误密码、正确密码、修改密码、退出及重启认证。

只有完成这些实机步骤后，才能把 Windows 10／11 桌面兼容和人工视觉验收标记为通过。
