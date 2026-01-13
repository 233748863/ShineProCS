# ShineProCS

基于 WPF 的游戏自动化引擎，使用计算机视觉技术实现技能自动释放。采用 BetterGI 风格的现代化架构设计。

## 功能特性

- **视觉检测**：基于 OpenCV 模板匹配检测技能图标状态
- **智能施法**：支持瞬发、正读条、引导（倒读条）三种施法类型
- **视觉结束检测**：通过点色或模板匹配检测读条/引导结束，替代固定时间等待
- **Buff库**：统一管理所有Buff/Debuff检测配置，支持技能联动
- **技能连招**：支持 Buff 依赖和前置技能联动配置
- **高效截图**：支持 Windows Graphics Capture (WGC) 高性能截图
- **悬浮窗**：游戏内实时状态显示，支持快捷操作
- **系统托盘**：支持最小化到托盘后台运行
- **全局热键**：Ctrl+F7/F8 快速控制引擎
- **多输入驱动**：支持 Win32 API 和 GhostBox 硬件驱动
- **配置管理**：支持多配置方案、导入导出、自动备份

## 系统要求

- Windows 10 1803+ (WGC 截图需要)
- .NET 9.0 Runtime
- 管理员权限 (键盘模拟需要)

## 快速开始

### 方式一：直接下载

从 [Releases](https://github.com/233748863/ShineProCS/releases) 下载最新版本的可执行文件。

### 方式二：从源码构建

```bash
git clone https://github.com/233748863/ShineProCS.git
cd ShineProCS
dotnet build
```

### 使用步骤

1. 运行程序，在「启动」页面选择目标游戏窗口
2. 在「技能配置」中添加技能，使用「一键配置」框选技能图标
3. （可选）在「Buff库」中配置需要检测的Buff
4. 点击「启动引擎」开始自动化

## 快捷键

| 快捷键 | 功能 |
|--------|------|
| Ctrl+F7 | 启动/停止引擎 |
| Ctrl+F8 | 暂停/恢复 |

## 项目结构

```
ShineProCS/
├── Core/
│   ├── Config/          # 全局配置
│   ├── Engine/          # 技能循环引擎、任务调度器
│   ├── GameTask/        # 游戏任务（自动拾取、自动跳过等）
│   ├── Interfaces/      # 抽象接口（服务、任务、触发器）
│   ├── Pathing/         # 路径系统
│   ├── Recognition/     # 识别服务（OCR、YOLO、模板匹配）
│   ├── Services/        # 核心服务实现
│   ├── Strategies/      # 策略模式实现
│   └── View/            # 视觉绘制
├── Infrastructure/      # 基础设施（截图、键盘、图像处理）
├── Models/              # 数据模型
├── ViewModels/          # MVVM ViewModel
├── Views/
│   ├── Controls/        # 自定义控件
│   ├── Pages/           # 导航页面
│   └── Windows/         # 窗口
├── Utils/               # 工具类
├── Resources/           # 资源文件
└── config/              # 配置文件
```

## 架构设计

项目采用 BetterGI 风格的现代化架构：

- **依赖注入**：使用 Microsoft.Extensions.DependencyInjection
- **MVVM 模式**：使用 CommunityToolkit.Mvvm
- **导航系统**：基于 WPF UI 的 NavigationView
- **任务系统**：支持独立任务（ISoloTask）和触发器（ITaskTrigger）
- **服务抽象**：所有核心功能通过接口抽象，便于测试和扩展

## 核心功能说明

### 施法类型

| 类型 | 说明 | 检测方式 |
|------|------|----------|
| ⚡ 瞬发 | 按下即释放 | 无需等待 |
| 📖 正读条 | 读条完成后释放 | 视觉检测或超时 |
| 🔄 引导 | 持续引导，可打断 | 视觉检测或固定时间 |

### 输入驱动

| 驱动 | 说明 | 适用场景 |
|------|------|----------|
| Win32 | Windows API 模拟 | 大部分游戏 |
| GhostBox | 硬件设备模拟 | 有反作弊的游戏 |

### 视觉检测

- **模板匹配**：检测技能图标是否可用（冷却完成）
- **点色检测**：检测读条条消失、引导进度等
- **超时保护**：配置的时间作为最大等待时间，防止卡死

## 技术栈

- WPF + WPF UI (Fluent Design)
- CommunityToolkit.Mvvm
- Microsoft.Extensions.DependencyInjection
- OpenCvSharp4
- Windows Graphics Capture
- GongSolutions.WPF.DragDrop

## 许可证

MIT License
