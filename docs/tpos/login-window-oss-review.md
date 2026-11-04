# TPOS^ 登录窗口：GitHub 开源方案检索与复用记录

> 检索与复核日期：2026-11-04
> 目标：保留 C#、WPF 和现有认证，不为小登录窗口引入大型 UI 框架。

## 检索范围

通过 GitHub Repository/Code API 检索并交叉查看了以下方向，而不是只使用一个搜索结果：

- WPF Acrylic、Mica、Desktop Acrylic、Glassmorphism、DWM。
- Frameless Window、WindowChrome、Rounded Corners、Transparent Window。
- Minimize／Maximize Animation、原生 DWM 窗口动画。
- Lightweight WPF UI、WPF Performance、Animation Performance、Memory Leak Prevention。
- Windows 10 旧版 BlurBehind 与 Windows 11 System Backdrop 的兼容路径。

## 候选项目评估

| 项目 | 许可证／维护情况 | 可借鉴内容 | TPOS^ 决策 |
|---|---|---|---|
| [Kinnara/ModernWpf](https://github.com/Kinnara/ModernWpf) | MIT；活跃；成熟 WPF 控件库 | `WindowBackdrop.cs` 对 Windows 11 Build 22621+、DWM System Backdrop 和 `DwmExtendFrameIntoClientArea` 的处理 | **复用少量原生调用模式，不引入完整包** |
| [picoe/Eto](https://github.com/picoe/Eto) | BSD-3-Clause；成熟跨平台 UI 项目 | `src/Eto.Wpf/CustomControls/GlassHelper.cs` 的公开 `DwmEnableBlurBehindWindow` 旧系统路径 | **仅参考并适配 GlassHelper，不引入 Eto** |
| [TwilightLemon/FluentWpfCore](https://github.com/TwilightLemon/FluentWpfCore) | MIT；小型项目 | `DwmAnimation.cs` 保留原生 DWM 最小化／最大化动画的窗口样式处理 | **只复用 DWM 动画思路；未采用其私有 Accent 兼容代码** |
| [lepoco/wpfui](https://github.com/lepoco/wpfui) | MIT；活跃；大型 Fluent UI 套件 | System Backdrop 与 WPF 窗口集成 | 不引入：当前只需要一个窗口，完整主题、控件和资源会增加依赖与内存 |
| [MaterialDesignInXAML/MaterialDesignInXamlToolkit](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit) | MIT；活跃；大型控件库 | 成熟 WPF 控件和样式 | 不引入：不是本次 DWM 玻璃的必要依赖，且会改变已批准视觉体系 |
| [TGSAN/CloudMoe-UI-Framework](https://github.com/TGSAN/CloudMoe-UI-Framework) | Apache-2.0；最近主要实现较早 | Mica／Acrylic 示例 | 不引入：完整框架超出范围，维护与兼容性需要额外验证 |
| [DinoChan/WindowChromeApplyRoundedCorners](https://github.com/DinoChan/WindowChromeApplyRoundedCorners) | 未识别到明确开源许可证 | Windows 11 Corner Preference 示例 | **没有复制代码**；仅确认公开 DWM 属性方向 |
| [selastingeorge/Win32-Acrylic-Effect](https://github.com/selastingeorge/Win32-Acrylic-Effect) | MIT；C++ Win32 示例 | Acrylic 技术研究 | 不采用：依赖私有 DWM／DirectComposition 路径，不符合本次稳定性限制 |
| [me4ik/Glassifyer](https://github.com/me4ik/Glassifyer) | 未识别到明确开源许可证 | 小型玻璃窗口思路 | 不采用：许可证和成熟度不足 |

## 实际复用位置

实现集中在：

```text
src/Tpos.Desktop/Services/NativeWindowManager.cs
```

实际复用／适配内容：

1. **ModernWpf（MIT）**：Windows 11 Build 22621+ 的 `DWMWA_SYSTEMBACKDROP_TYPE`、Desktop Acrylic 和完整 Glass Frame 调用顺序。
2. **Eto GlassHelper（BSD-3-Clause）**：旧系统的 `DWM_BLURBEHIND` 与 `DwmEnableBlurBehindWindow` 声明和透明 CompositionTarget 处理。
3. **FluentWpfCore（MIT）**：自定义标题栏下保留 `WS_CAPTION`、`WS_THICKFRAME`、最小化和最大化样式，从而继续使用原生 DWM 动画。

许可证原文和版权声明确认保存在：

```text
src/Tpos.Desktop/THIRD-PARTY-NOTICES.txt
```

该文件同时作为 EmbeddedResource 包含在 EXE 中。

## 未采用的技术

- 没有使用 `SetWindowCompositionAttribute` 或 Accent 私有接口。
- 没有注入 DWM、修改其他进程或要求管理员权限。
- 没有使用 `AllowsTransparency=True` 的整窗 WPF 分层透明方案；密码和 Logo 不随窗口整体透明。
- 没有高频截图、每帧重新生成 Bitmap、逐帧定时器或自制最小化动画。
- 没有新增 NuGet UI 包、WebView2、WinUI 3、SkiaSharp 或大型字体资源。

## 新增依赖

**新增 NuGet／运行时 UI 依赖：0。**

最终 Logo 只进行了离线裁切、白色背景透明化和 PNG／ICO 压缩；这些处理不包含在应用运行时代码中，也没有新增图像处理库依赖。
