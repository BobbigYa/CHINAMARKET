# TPOS^ — 第二阶段真实密码登录：实现与测试报告

> 报告日期：2026-11-03
> 产品代码测试提交：`c61375f4e8ff3759b7a7a1d3b6a80ef1af421585`
> Windows 验收运行：[GitHub Actions #41723788329](https://github.com/BobbigYa/CHINAMARKET/actions/runs/41723788329)
> 当前结论：**真实认证代码、Windows Release 构建及 11 项自动化测试已完成；人工 Windows 界面验收尚未完成。**

## 1. 已实现功能

- 首次启动检查本地管理员账户；没有账户时显示初始化页面。
- 用户自行输入两次管理员密码，密码至少 12 个字符，没有默认密码。
- 使用 Argon2id、16 字节随机盐和 32 字节哈希保存凭据。
- 标准哈希记录包含 `argon2id`、版本 19、内存、迭代、并行度、盐和结果。
- 生产默认参数为 64 MiB、3 次迭代、并行度 1；哈希计算在后台线程执行。
- 正确密码进入仅含导航、Logo、欢迎信息、语言切换和退出按钮的 Dashboard。
- 错误密码显示剩余次数；连续失败 5 次锁定 15 分钟并显示倒计时。
- 失败次数、锁定截止时间和密码哈希保存在 SQLite，重开数据库不会重置锁定。
- 修改密码必须验证当前密码，新密码不能与当前密码相同；修改后旧密码失效。
- 会话只保存在内存，退出或关闭程序后必须重新输入密码。
- 中文和西班牙语界面、密码显隐、动态流光、Logo 浮动、卡片进入和按钮动画。
- 不包含邮箱、验证码、网络账户、销售、商品、库存或支付功能。

## 2. 主要修改文件

| 文件或目录 | 内容 |
|---|---|
| `Tpos.sln`、`Directory.Build.props`、`.gitignore` | .NET 10 解决方案、统一编译设置、数据库与构建产物忽略规则 |
| `src/Tpos.Core/Authentication/` | 认证服务、密码策略、会话状态、结果模型和存储接口 |
| `src/Tpos.Infrastructure/Security/Argon2PasswordHasher.cs` | Argon2id 哈希、参数解析、固定时间比较、异常格式防护 |
| `src/Tpos.Infrastructure/Persistence/SqliteCredentialStore.cs` | SQLite 表结构、参数化 SQL、失败计数事务和密码更新 |
| `src/Tpos.Desktop/Views/` | 登录、首次初始化、修改密码、认证外壳和 Dashboard |
| `src/Tpos.Desktop/ViewModels/` | 页面状态、加载、错误、锁定倒计时和退出逻辑 |
| `src/Tpos.Desktop/Controls/` | 动态背景、双语切换控件 |
| `src/Tpos.Desktop/Themes/Controls.xaml` | 保留原视觉的按钮、输入框、卡片及动画样式 |
| `src/Tpos.Desktop/Services/LocalizationService.cs` | 简体中文和西班牙语文本 |
| `tests/Tpos.Tests/` | Argon2id、SQLite、认证流程、离线、导航和会话测试 |
| `.github/workflows/tpos-windows-tests.yml` | Windows 编译、出站断网测试、逐项结果和测试构建发布 |

原有 `README.md` 和商城 `index.html` 未修改。

## 3. 实际构建结果

最终 Windows Runner 实际完成并通过：

1. 安装 .NET 10 SDK。
2. NuGet 依赖还原。
3. `Tpos.sln` Release 编译。
4. 测试进程出站网络阻断及 11 项测试。
5. 逐项测试结果与 TRX 报告上传。
6. `win-x64`、框架依赖型发布构建。
7. Windows 测试构建包上传。

发布包名：`tpos-win-x64-test-build`。可在上述 GitHub Actions 页面的 Artifacts 区域获取。该包不是安装程序，需要 Windows x64 和 .NET 10 Desktop Runtime。

## 4. 实际测试结果

**汇总：11 项执行，11 项 Passed，0 项失败，0 项跳过。**

| 实际测试方法 | 结果 | 验证内容 |
|---|---|---|
| `HashPassword_UsesArgon2idRandomSaltAndNeverStoresPlaintext` | Passed | Argon2id 格式、随机盐、无明文、正确/错误密码校验 |
| `VerifyPassword_MalformedHash_ThrowsControlledInvalidDataException` | Passed | 损坏哈希不会成为有效凭据 |
| `FirstRun_CreatesAdministrator_ThenUsesNormalLogin` | Passed | 首次检查、密码确认、初始化、防重复初始化及正常登录 |
| `Authenticate_WrongPassword_DeniesAccessAndDecrementsAttempts` | Passed | 错误密码拒绝进入并减少剩余次数 |
| `FiveWrongPasswords_LockForFifteenMinutes_EvenWithCorrectPassword` | Passed | 第 5 次失败锁定；锁定期间正确密码也拒绝；到期恢复 |
| `ReopenDatabase_PersistsPasswordFailuresAndLockout` | Passed | 重建数据库连接和服务后，密码、失败次数和锁定仍有效 |
| `ChangePassword_RequiresCurrentPassword_OldPasswordStopsWorking` | Passed | 当前密码验证、拒绝同密码、修改后旧密码失效、新密码有效 |
| `DamagedCredentialHash_ReturnsControlledError_InsteadOfSuccessfulLogin` | Passed | 数据损坏返回受控错误，不绕过登录 |
| `FirstRun_LoginDashboardAndLogout_UseExpectedPagesAndSessionState` | Passed | 初始化页 → 登录页 → 错误停留 → Dashboard → 退出回登录页 |
| `Session_StartsSignedOut_LogoutClearsIt_AndNewSessionRequiresPassword` | Passed | 会话不持久化，退出和新实例均为未认证 |
| `OutboundNetworkBlocked_LocalSqliteLoginStillSucceeds` | Passed | 测试进程外网确实被阻断后，本地 SQLite 登录仍成功 |

最终运行中的部分实测耗时：

- 离线专项登录：约 0.295 秒。
- 首次初始化到 Dashboard 和退出的完整 ViewModel 联动：约 1.082 秒。
- 5 次失败及锁定到期：约 0.195 秒（测试使用受控时钟，没有真实等待 15 分钟）。
- 数据库重开与锁定持久化：约 0.350 秒。

### 4.1 与用户八项验收的对应关系

| 要求 | 实际覆盖 | 边界 |
|---|---|---|
| 首次启动管理员初始化 | 自动化通过 | 未人工操作完整窗口 |
| 正确密码进入 Dashboard | ViewModel 联动通过 | 未人工检查 Dashboard 像素和动画 |
| 错误密码无法进入 | 自动化通过 | 已检查仍停留登录页 |
| 连续错误 5 次锁定 | 自动化通过 | 使用受控时钟验证到期，不实际等待 15 分钟 |
| 重启后密码仍有效 | 数据库和服务重建测试通过 | **未人工关闭并重开完整 EXE** |
| 断网后登录 | Windows 防火墙真实阻断测试通过 | 未在目标收银电脑拔除网线实测 |
| 修改密码后旧密码失效 | 自动化通过 | 未人工操作修改密码对话框 |
| 退出后重新输入密码 | 会话和 ViewModel 退出命令通过 | 未人工点击退出按钮 |

### 4.2 测试参数说明

为缩短 CI 时间，集成测试使用 8 MiB、1 次迭代的 Argon2id 测试参数；生产代码默认使用 64 MiB、3 次迭代。算法和存储格式经过测试，但**生产参数在目标收银电脑上的响应时间尚未实测**。

## 5. 在 Windows 11 上运行

开发运行：

```powershell
dotnet restore Tpos.sln
dotnet run --project src/Tpos.Desktop/Tpos.Desktop.csproj
```

发布：

```powershell
dotnet publish src/Tpos.Desktop/Tpos.Desktop.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained false `
  --output artifacts/Tpos-win-x64
```

首次启动设置密码后，本地数据库位于：

```text
%ProgramData%\Tpos\Security\tpos-auth.db
```

数据库保存哈希和安全参数，不保存明文密码。没有 `admin123`、`123456` 或其他预置密码，也没有万能入口。

## 6. 尚未完成

- 人工 Windows 11 界面、动画、DPI、窗口缩放和中西文长文本验收。
- 在目标收银电脑上测试生产 Argon2id 参数的响应时间和内存占用。
- 人工关闭/重开完整应用、点击修改密码和退出按钮的最终交互验收。
- 安装程序、代码签名、自动更新和正式文件 ACL 配置。
- 忘记密码恢复；当前没有邮件找回、恢复码或万能密码。
- 销售、商品、库存、采购、支付、税务和发票功能，均按要求未开发。

## 7. 安全风险与限制

1. **本机高权限攻击者仍是主要风险。** Windows 管理员可能修改系统时间、数据库或替换程序，Argon2id 无法防止这些行为。
2. **离线锁定依赖本机时间。** 修改系统时钟可能尝试绕过锁定；应限制 Windows 管理权限并保护系统时间。
3. **数据库文件需要操作系统保护。** 建议使用 BitLocker、Windows 标准用户和受限文件 ACL。当前交付没有安装程序来统一配置 ACL。
4. **弱密码仍会增加离线猜测风险。** 当前策略检查长度、纯数字、空白和重复字符，但没有连接密码泄露数据库；这是有意的完全离线设计。
5. **密码会短暂存在于内存。** 页面不绑定密码、不写日志并尽快清空输入框，但 .NET 字符串不能保证所有临时副本立即擦除。
6. **第三方依赖需要持续维护。** 当前固定使用 `Konscious.Security.Cryptography.Argon2 1.3.1` 和 `Microsoft.Data.Sqlite 10.0.0`，上线前仍应做漏洞扫描和更新评估。
7. **测试通过不等于绝对安全。** 本阶段未进行独立渗透测试、恶意软件场景测试或代码签名验证。
