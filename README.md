# ShineProCS

基于 WPF 的游戏自动化引擎，使用计算机视觉技术实现技能自动释放。

## 功能特性

- **视觉检测**：基于 OpenCV 模板匹配检测技能图标状态
- **技能连招**：支持 Buff 依赖和前置技能联动配置
- **高效截图**：支持 Windows Graphics Capture (WGC) 高性能截图
- **悬浮窗**：游戏内实时状态显示，支持快捷操作
- **全局热键**：Ctrl+Alt+F1/F2/F3 快速控制引擎
- **配置管理**：支持多配置方案、导入导出、自动备份

## 系统要求

- Windows 10 1803+ (WGC 截图需要)
- .NET 9.0 Runtime
- 管理员权限 (键盘模拟需要)

## 快速开始

1. 克隆仓库并构建
```bash
git clone https://github.com/233748863/ShineProCS.git
cd ShineProCS
dotnet build
```

2. 运行程序，在「全局设置」中框选检测区域

3. 在「技能配置」中设置技能按键和模板图片

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
│   ├── Engine/          # 技能循环引擎
│   ├── Interfaces/      # 抽象接口
│   ├── Services/        # 核心服务
│   └── Strategies/      # 策略模式实现
├── Infrastructure/      # 基础设施实现
├── Models/              # 数据模型
├── ViewModels/          # MVVM ViewModel
├── Views/               # 窗口视图
├── Utils/               # 工具类
└── config/              # 配置文件
```

## 技术栈

- WPF + ModernWpf UI
- CommunityToolkit.Mvvm
- OpenCvSharp4
- Windows Graphics Capture

## 许可证

MIT License
