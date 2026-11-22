# 系统声音按设备配置

所有播放设备默认恢复“系统声音”50%；可在 `config.json` 中为每个设备设置不同值。支持蓝牙、USB、3.5mm、HDMI 等 Windows 播放端点。继续使用 EarTrumpet 即可。

## 日常使用

- 双击 `Start.cmd`：检查配置并在后台启动。重复启动只有一个实例。
- 双击 `Stop.cmd`：停止当前后台程序。
- 编辑 `config.json` 并保存：后台程序通常在 1 秒左右重新应用配置。
- 程序在启动、播放设备接入/重新启用、默认播放设备切换、系统声音会话出现时应用音量。设备/会话初始化可能带来额外延迟。
- 临时在 EarTrumpet 中调整后会保持，直到下一次上述触发。不自动记录手动调整。
- 新设备或没有单独配置的设备采用 `defaultVolumePercent`。
- 每次后台程序实际启动后，扫描 Windows 已登记的输出设备并增量补入配置（包含离线设备）。已有设备的音量和备注保持不变，新项使用全局默认音量；重复启动不会产生重复条目。

只调整混音器内的“系统声音”项目。设备总音量、其他应用、静音状态和默认设备不变。后台程序不会主动播放测试声音。

## 配置

首次通过管理脚本启动/检查时会生成 `config.json`；之后启动时只追加新设备，保留已有条目顺序、音量、备注及扩展字段，不移除离线设备。`name` 仅供人阅读，匹配使用完整 `id`。

没有新增设备时不改写配置；追加设备时先将完整原文件备份到 `logs\config-before-sync-*.json`，再原子替换。无效配置不会被自动重建或覆盖。Windows 可能保留历史设备，因此清单中可能出现已不再使用的端点。

```json
{
  "defaultVolumePercent": 50,
  "devices": [
    {
      "id": "{0.0.0.00000000}.{3011b17f-6c2f-486f-b900-69cffcc2fd69}",
      "name": "耳机 (OPPO Enco Air4 Pro)",
      "volumePercent": 50
    }
  ]
}
```

`volumePercent` 可以是 0–100 的数字。把某台设备的值改成 30，该设备以后就恢复 30%；其他设备仍按各自配置或全局 50%。`devices` 可使用空数组 `[]`。

运行以下命令查看完整设备 ID（也会写入 `devices.json`）：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Control.ps1 -Action Devices
```

通常只需在启动扫描补入的条目中修改 `volumePercent`。运行期间接入的新设备先使用全局默认值，下次启动时补入配置；也可使用 `SyncDevices` 命令立即补入。不要直接编辑自动生成的 `devices.json`；其中的音量只是生成清单当时的快照。设备重配对、驱动重装可能产生新的 ID；新 ID 将使用全局默认值，旧 ID 的自定义值不会自动迁移。相同名称的多个 ID 代表不同端点，各自可独立配置。

配置格式错误、重复 ID、缺少音量或数值越界时：启动检查会报错；运行中的程序保留最后一次有效配置，并写入日志。修正保存后自动恢复。

## 管理及登录自启动

在本目录运行，通常无需管理员：

```powershell
# 当前状态和最近日志
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Control.ps1 -Action Status
# 立即应用一次
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Control.ps1 -Action Apply
# 扫描并增量补充设备配置（保留已有设置）
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Control.ps1 -Action SyncDevices
# 添加当前用户的登录启动快捷方式（不改变全局执行策略）
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Control.ps1 -Action InstallStartup
# 删除本工具的登录启动快捷方式；当前实例用 Stop.cmd 停止
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Control.ps1 -Action RemoveStartup
```

启动项是当前用户 Startup 文件夹中的 `SystemSoundsKeeper.lnk`，通过系统 PowerShell 隐藏启动程序。启用后请保持项目路径不变。没有 Windows 服务、第三方依赖或计划任务。

日志：`logs\watcher.log`，超过约 512 KB 保留一个旧文件。程序使用 Windows 音频设备事件并每秒补充检查一次，处理延迟创建的会话。音频服务临时不可用时自动重连。

EarTrumpet 没有显示“系统声音”不一定代表后台没有会话；只要后台会话存在，程序即可通过接口设置，无需打开混音器。如果 Windows 尚未创建会话，程序会等待它出现。因此首次提示音可能在程序调整之前响起；本工具不能保证拦截第一次提示音。它不会用设备总音量替代会话音量。

## 构建与验证

需要 Windows x64 自带的 .NET Framework 4.x。无需 .NET SDK、NuGet 或联网下载。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1 -Test
```

策略测试覆盖设备覆盖规则、错误配置、新会话、临时手动调整、重连、配置重载及增量设备同步；同步测试核对已有音量/备注/扩展字段保留、离线设备保留、重复扫描、原文备份和无效配置保护。真实设备拔插、睡眠唤醒及重启登录需要在实际使用中验证。

若要重新编译，先运行 `Stop.cmd`，编译后重新启动。停用本工具不会自动还原已经应用的音量，可以在 EarTrumpet 中手动调回。
