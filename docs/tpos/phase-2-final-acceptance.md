# TPOS^ — 第二阶段最终验收、修正与发布说明

> 日期：2026-11-03
> 最终产品提交：`2dc2ffe7f9defb5f714ac922d86263ff411ab18f`
> 最终运行：[GitHub Actions #41728045201](https://github.com/BobbigYa/CHINAMARKET/actions/runs/41728045201)
> 验收性质：**真实 Windows EXE 自动化验收，不是人工 Windows 11 验收**

## 1. 实际完成的工作

- 扫描源码、文档、测试和发布配置，旧品牌关键词残留为 **0 处**；当前统一使用 **TPOS^**。
- 未修改已批准的 Logo、配色、卡片、流光、浮动或按钮动画。
- 仅为真实 EXE 自动化验收增加 UI Automation 标识，不改变视觉外观。
- 修正数据库权限：不再只依赖 Windows 默认继承 ACL。
- 数据库目录和文件现在显式限制为：**当前 Windows 用户、SYSTEM、本机 Administrators**。
- 增加实际 ACL 单元测试和发布 EXE 数据库 ACL 检查。
- 发布方式改为 `win-x64 + self-contained + 单文件 EXE`，包含 .NET Runtime。
- 对真实发布 EXE 执行关闭、重启、错误密码、正确密码、Dashboard 和退出自动化测试。
- 直接检查真实 EXE 创建的数据库格式、生产参数、明文密码和日志。

## 2. EXE 发布位置与文件清单

### 2.1 下载位置

打开 [最终 Actions 页面](https://github.com/BobbigYa/CHINAMARKET/actions/runs/41728045201)，在 Artifacts 区域下载：

| Artifact | 内容 |
|---|---|
| `tpos-win-x64-self-contained` | Windows x64 自包含单文件 EXE |
| `tpos-acceptance-evidence` | UI 验收、数据库检查和发布文件 SHA-256 清单 |
| `tpos-windows-test-results` | 12 项自动化测试的 TRX 报告 |

**这些 Artifacts 当前保留至 2026-11-17。** EXE 不在当前 Linux 工作区内，而是在上述 GitHub 发布产物中。

### 2.2 主发布文件

| 文件 | 大小 | SHA-256 |
|---|---:|---|
| `Tpos.Desktop.exe` | 66,027,902 字节，约 63.0 MiB | `28AC7C2EBCA1438D2DCEABE13AD76F17C7A1112466BD77F1342E377CE6725581` |

完整清单位于 `tpos-acceptance-evidence/publish-manifest.json`。

发布参数包括：

```text
Runtime: win-x64
Self-contained: true
PublishSingleFile: true
IncludeNativeLibrariesForSelfExtract: true
EnableCompressionInSingleFile: true
```

目标电脑不需要单独安装 .NET Runtime。尚未在“从未安装过任何 .NET 的干净 Windows 11 电脑”上实测。

## 3. 启动说明

1. 下载并解压 `tpos-win-x64-self-contained`。
2. 可在 PowerShell 中核对文件：

   ```powershell
   Get-FileHash .\Tpos.Desktop.exe -Algorithm SHA256
   ```

   结果应与上表完全一致。
3. 双击 `Tpos.Desktop.exe`。
4. 第一次启动时，自行创建管理员密码并再次确认；没有预置密码。
5. 初始化后重新输入密码登录。
6. 请始终使用同一个 Windows 用户运行；数据库 ACL 不允许普通其他 Windows 用户读取。
7. 当前 EXE **未进行商业代码签名**，Windows 可能显示未知发布者或 SmartScreen 提示。不要使用绕过安全提示的脚本，应先核对 SHA-256。

## 4. 数据库位置与备份

### 4.1 实际位置

```text
%ProgramData%\Tpos\Security\tpos-auth.db
```

运行时还可能出现：

```text
tpos-auth.db-wal
tpos-auth.db-shm
```

数据库只保存 Argon2id 哈希、随机盐、算法参数、失败次数和锁定状态，不保存明文密码。当前目录和数据库文件已设置显式 ACL；备份文件也需要同等保护。

### 4.2 如何修改管理员密码

1. 打开登录页面的“修改密码”。
2. 输入当前密码。
3. 输入新密码并再次确认。
4. 修改成功后，旧密码失效，下次使用新密码登录。

当前没有忘记密码找回功能，也没有万能入口。请安全保管密码和数据库备份。

### 4.3 如何备份

1. 正常退出并完全关闭 TPOS^，确认 `Tpos.Desktop.exe` 进程已经结束。
2. 在应用关闭状态下，将 `%ProgramData%\Tpos\Security\` 整个目录复制到受保护的备份位置。
3. 如果只复制数据库，请同时复制 `.db` 以及当时仍存在的 `.db-wal`、`.db-shm` 文件。
4. 不要在应用运行或正在写入时直接复制数据库。
5. 备份包含密码哈希和认证状态，应加密并限制访问，不要上传到公共网盘或 Git 仓库。
6. 恢复时先关闭应用，再将备份放回同一路径；下次启动会再次应用数据库 ACL。

## 5. 实际运行的测试

### 5.1 单元与集成测试

最终汇总：**12 项执行，12 项通过，0 失败，0 跳过。**

覆盖：

- Argon2id 格式、随机盐、正确／错误密码和损坏哈希。
- 首次初始化、错误密码、5 次失败锁定和锁定到期。
- SQLite 重开后的密码、失败次数和锁定持久化。
- 修改密码后旧密码失效。
- Dashboard 导航和退出会话。
- 测试进程真实出站网络阻断后，本地登录成功。
- 数据库目录和文件 ACL 不含 Users/Everyone 权限。

### 5.2 真实发布 EXE 自动化验收

最终汇总：**11 项真实 EXE 检查全部通过。**

| 顺序 | 实际操作 | 结果 |
|---:|---|---|
| 1 | 启动 self-contained EXE，初始化管理员并返回登录页 | 通过 |
| 2 | 输入错误密码，检查错误提示且不能进入 Dashboard | 通过 |
| 3 | 输入正确密码，使用生产 Argon2id 参数进入 Dashboard | 通过 |
| 4 | 退出登录，确认回到密码输入页 | 通过 |
| 5 | 完全关闭发布的 EXE | 通过 |
| 6 | 再次启动同一 EXE，确认不自动进入 Dashboard 且必须输入密码 | 通过 |
| 7 | 重启后再次输入错误密码，确认不能进入 | 通过 |
| 8 | 重启后输入正确密码，确认进入 Dashboard | 通过 |
| 9 | 再次退出登录，确认必须重新输入密码 | 通过 |
| 10 | 第二次完全关闭发布的 EXE | 通过 |
| 11 | 检查真实数据库位置及显式 ACL | 通过 |

验收证据位于 `tpos-acceptance-evidence/ui-smoke-results.json`。

### 5.3 真实数据库检查

对发布 EXE 创建的数据库直接检查，结果通过：

- 算法：`argon2id`。
- 版本：`19`。
- 生产参数：`m=65536,t=3,p=1`，即 64 MiB、3 次迭代、并行度 1。
- 随机盐：16 字节。
- 哈希：32 字节。
- 成功登录后失败计数为 0，锁定状态为空。
- 数据库及 WAL/SHM 中未发现本次测试密码或错误密码明文。
- 未发现未预期的 `.log` 文件。
- 源码扫描未发现 HTTP 客户端、密码日志或明文默认密码。

证据位于 `tpos-acceptance-evidence/database-inspection.json`。

## 6. 尚未通过或未测试的项目

- **没有可人工操作的 Windows 桌面，因此未完成人工 Windows 11 验收。** 当前是 Windows Runner 上的真实 EXE 自动化验收。
- 未在门店 Windows 11 电脑上检查实际 DPI、触摸、键盘手感、配色和动画流畅度。
- 未在从未安装 .NET 的干净 Windows 11 虚拟机上验证自包含启动。
- 未实际篡改 Windows 系统时间；时间风险通过代码和受控时钟测试确认，见下节。
- 未进行独立渗透测试、恶意软件场景测试或商业代码签名验证。
- 未提供安装程序、自动更新或忘记密码恢复。

## 7. 已知安全风险与限制

1. **修改系统时间可以绕过锁定等待，但不能绕过密码验证。** 当前锁定通过本机 UTC 与 `LockoutUntilUtc` 比较；将时间向前调整会使锁定期提前到期。现有受控时钟测试也证明时间前进后锁定解除。完全离线时应用没有可信网络时间，无法彻底防止有权限者篡改时钟。应限制 Windows“更改系统时间”权限，不能宣称锁定对管理员不可绕过。
2. **Windows 高权限攻击者仍可替换程序或数据库。** ACL 和 Argon2id 不能防止管理员控制整台电脑。建议使用 BitLocker、标准用户、系统更新和最小权限。
3. **当前用户绑定会影响运维。** 数据库只允许创建它的 Windows 用户、SYSTEM 和 Administrators；更换普通 Windows 用户后可能无法启动，应有受控的管理员备份和迁移流程。
4. **弱密码仍可能被离线猜测。** 密码长度和基本弱密码检查不能替代用户选择强密码。
5. **密码会短暂存在于内存。** 不写日志、不绑定密码并尽快清空输入框，但 .NET 无法保证所有临时字符串立即擦除。
6. **没有密码找回。** 忘记密码后不能通过正常界面绕过；数据库备份不等于知道密码。
7. **发布文件尚未签名。** 下载后必须核对 SHA-256，并通过受控渠道分发。
8. **自动化通过不等于完整人工验收或绝对安全。** 门店上线前仍需进行人工 Windows 11 验收和独立安全审查。
