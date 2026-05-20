# MyPersonalTool — 桌面电子宠物

一个基于 Avalonia 12 的 Windows 桌面宠物应用，集成 Petdex 精灵图动画、AI 助手活动监测、健康提醒等功能。

---

## 项目架构

```
MyPersonalTool.sln
├── MyPersonalTool.Core/          纯领域模型 + 接口
│   ├── Models/                   数据模型
│   ├── Interfaces/               服务接口
│   └── (无任何 UI 依赖)
├── MyPersonalTool.Services/      服务实现层
│   ├── ConfigService.cs          JSON 配置持久化
│   ├── PetdexService.cs          Petdex 宠物扫描/加载
│   ├── ActivityMonitor.cs        AI 助手事件监控
│   ├── HealthReminderService.cs  健康提醒（久坐/用眼/喝水）
│   └── AutoStartService.cs       开机自启动（注册表）
├── MyPersonalTool.ViewModels/    MVVM ViewModel 层
│   ├── MainViewModel.cs          主窗口逻辑
│   └── PetViewModel.cs           宠物逻辑 + 交互 + 监测
└── MyPersonalTool/               Avalonia 应用层（视图/窗口）
    ├── App.axaml/.cs             应用入口 + 依赖注入
    ├── MainWindow.axaml/.cs      设置窗口（竖屏）
    ├── PetWindow.axaml/.cs       宠物悬浮窗（透明置顶）
    ├── Controls/                 自定义控件
    │   └── SpritesheetView.cs    Petdex 精灵图逐帧动画
    ├── Views/                    视图页
    │   ├── HomePage.axaml/.cs    首页
    │   ├── SettingsPage.axaml/.cs 设置页
    │   └── PetdexDialog.axaml/.cs 宠物图鉴 + 安装
    └── Services/
        └── AvaloniaDispatcherService.cs
```

依赖链：`Core ← Services ← ViewModels ← App`

---

## 技术栈

| 组件 | 版本/说明 |
|------|----------|
| .NET | 9.0 |
| Avalonia | 12.0.3 (FluentTheme) |
| CommunityToolkit.Mvvm | 8.4.0 (源生成器) |
| SkiaSharp | 3.119.4-preview.1.1 |
| Microsoft.Win32.SystemEvents | 9.0.0 (锁屏检测) |
| 目标平台 | Windows (x64) |

---

## 功能清单

### 🐱 Petdex 宠物系统

| 特性 | 说明 |
|------|------|
| 精灵图动画 | 支持 WebP/PNG 格式的 8×9 帧 spritesheet，SkiaSharp 解码 |
| Petdex 集成 | 扫描 `~/.codex/pets/` + `~/.petdex/pets/` |
| 宠物图鉴 | 弹窗展示所有已安装宠物，支持点击切换 |
| 一键安装 | 在图鉴输入框输入 `npx petdex install <name>` 直接安装 |
| 缩略图缓存 | 首次加载后提取第一帧保存到 `~/.petdex/thumbs/`，下次直接读取 |
| 空白帧过滤 | 自动检测并跳过空白帧，每行只播放有效帧数 |
| 动画速度 | 可调 30~300ms/帧，设置页滑块调节，即时生效 |
| 动画行适配 | 自动适配宠物 spritesheet 的实际行数 |
| 自动随机动画 | 每 ~30 秒随机切换到非 idle 行，2 秒后恢复 |
| 交互反应 | 单击 → waving，双击 → 打开图鉴，右键 → 动作菜单 |

### 💬 AI 助手活动监测

| 特性 | 说明 |
|------|------|
| 事件文件监控 | 轮询 `~/.petdex/events/*.json` |
| 对话气泡 | 宠物上方弹出显示 AI 响应内容（前 120 字） |
| Claude Code Hook | 预置 `claude-hook.ps1`，配好 `.claude/settings.json` 即可 |
| 手动测试 | `pet-msg.bat "内容"` 发送测试消息 |

### 🧘 健康提醒

| 类型 | 默认间隔 | 提示语示例 |
|------|---------|-----------|
| 久坐 | 55 分钟 | "起来活动一下吧～坐太久尾巴要长在椅子上啦！🐱" |
| 用眼 | 25 分钟 | "看看窗外吧～一直盯着屏幕，眼睛会变成熊猫眼的🐼" |
| 喝水 | 40 分钟 | "喝水时间到！你的身体正在喊「我好渴啊～」💧" |

- 锁屏自动重置计时器（`SystemEvents.SessionSwitch`）
- 跨天自动重置
- 间隔通过设置页滑块自定义（15~120 分钟）

### 🪟 窗口特性

| 特性 | 实现 |
|------|------|
| 置顶悬浮 | `Topmost=True` + 每 3 秒重设防沉底 |
| 透明背景 | `TransparencyLevelHint="Transparent"` |
| 无边框 | `WindowDecorations="None"` |
| 位置记忆 | `~/.petdex/config.json` 持久化，关闭时保存 |
| 开机自启 | Windows 注册表 `HKCU\...\Run` |
| 宠物窗为主窗口 | 启动仅显示宠物，设置窗按需打开 |

### ⚙️ 设置页功能

| 模块 | 内容 |
|------|------|
| 🎨 主题设置 | 深色模式开关 |
| 🚀 启动设置 | 开机自启动 |
| 📐 窗口设置 | 宽度/高度 |
| 🧘 健康提醒 | 启用/关闭 + 三个间隔滑块 |
| 🐱 宠物设置 | 名字 + 动画速度滑块 |
| 🎮 宠物动作 | 动作列表展示 |

---

## 配置文件

**应用配置** — `%APPDATA%\MyPersonalTool\config.json`

```json
{
  "WindowWidth": 420,
  "WindowHeight": 768,
  "PetName": "小宠",
  "CurrentPetId": "petdex:kirby",
  "PetWindowX": 1200,
  "PetWindowY": 100,
  "IsDarkTheme": true,
  "EnableAutoStart": false,
  "AnimFrameDurationMs": 100.0,
  "HealthReminder": {
    "Enabled": true,
    "SitIntervalMinutes": 55,
    "EyeIntervalMinutes": 25,
    "DrinkIntervalMinutes": 40
  },
  "PetActions": [...]
}
```

**其他路径：**

| 路径 | 用途 |
|------|------|
| `~/.codex/pets/<slug>/` | Petdex 宠物安装目录（Codex） |
| `~/.petdex/pets/<slug>/` | Petdex 宠物安装目录（Petdex CLI） |
| `~/.petdex/events/` | AI 助手事件文件 |
| `~/.petdex/thumbs/` | 宠物缩略图缓存 |
| `~/.petdex/telemetry.json` | Petdex 遥测 |

---

## 快捷键与交互

| 操作 | 效果 |
|------|------|
| 单击宠物 | 宠物挥手回应，切换到 waving 动画行 |
| 双击宠物 | 打开宠物图鉴 |
| 右键宠物 | 打开动作菜单（切换宠物 / 喂食/玩耍/摸摸 / 打开设置 / 退出） |
| 拖动宠物 | 任意位置拖动宠物窗口 |
| Enter (图鉴输入框) | 执行 `npx petdex install` |
| 🔄 (图鉴) | 刷新宠物列表 |

---

## 构建与运行

```bash
# 构建
cd D:\A_MyFile\Backend\Avalonia
dotnet build MyPersonalTool.sln

# 运行
dotnet run --project MyPersonalTool\MyPersonalTool.csproj

# 或双击
start.bat
```

---

## Claude Code Hook 配置

在 `~/.claude/settings.json` 中添加：

```json
{
  "hooks": {
    "onResponse": "D:\\A_MyFile\\Backend\\Avalonia\\claude-hook.ps1"
  }
}
```

每次 Claude Code 响应后，宠物自动弹出对话气泡显示回答内容。

---

## Petdex 宠物安装

```bash
# 查看可用宠物
# 访问 https://petdex.crafter.run

# 安装宠物
npx petdex install kirby
npx petdex install boba

# 或在宠物图鉴的输入框中直接输入安装命令
```

---

## Spritesheet 规格

| 属性 | 标准值 |
|------|--------|
| 总尺寸 | 1536 × 1872 px |
| 网格 | 8 列 × 9 行 |
| 单帧尺寸 | 192 × 208 px |
| 格式 | WebP 或 PNG |
| 空白帧 | 尾部空白自动跳过 |

**动画行对照：**

| 行 | 状态 | 说明 |
|-----|------|------|
| 0 | idle | 待机/呼吸 |
| 1 | running-right | 向右跑 |
| 2 | running-left | 向左跑 |
| 3 | waving | 挥手 |
| 4 | jumping | 跳跃 |
| 5 | failed | 失败/沮丧 |
| 6 | waiting | 等待 |
| 7 | running | 忙碌工作 |
| 8 | review | 审查代码 |
