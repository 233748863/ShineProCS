# 实现计划: 输入驱动选择

## 概述

本计划将输入驱动选择功能分解为可执行的编码任务，按照 SDK 迁移 → 核心实现 → UI 集成的顺序进行。

## 任务列表

- [x] 1. GhostBox SDK 文件迁移
  - [x] 1.1 创建 `libs/ghostbox/` 目录结构
    - 在项目根目录创建 `libs/ghostbox/` 文件夹
    - 从 `gbild_demo_csharp` 或外部 SDK 复制 `gbild.dll` 和 `gbild64.dll`
    - _需求: 1.1, 1.2_
  - [x] 1.2 配置项目文件自动复制 DLL
    - 修改 `ShineProCS.csproj` 添加 DLL 复制配置
    - 确保编译时 DLL 自动复制到输出目录
    - _需求: 1.3_

- [x] 2. 核心接口和枚举定义
  - [x] 2.1 创建 InputDriverType 枚举
    - 在 `Models/` 目录创建 `InputDriverType.cs`
    - 定义 Win32 和 GhostBox 两种驱动类型
    - _需求: 2.1_
  - [x] 2.2 创建 IMouseInterface 鼠标接口
    - 在 `Core/Interfaces/` 目录创建 `IMouseInterface.cs`
    - 定义 MoveTo、PressButton、ReleaseButton、Click 方法
    - _需求: 4.1_

- [x] 3. GhostBox 设备管理器实现
  - [x] 3.1 创建 GhostBoxDeviceManager 类
    - 在 `Infrastructure/` 目录创建 `GhostBoxDeviceManager.cs`
    - 实现 P/Invoke 声明（设备操作、键盘操作、鼠标操作）
    - 实现单例模式和设备连接管理
    - 实现 DLL 可用性检测
    - _需求: 1.4, 3.2, 3.5_
  - [x] 3.2 编写 GhostBoxDeviceManager 单元测试
    - 测试 DLL 不存在时的行为
    - 测试连接状态属性
    - _需求: 1.4_

- [x] 4. GhostBox 驱动实现
  - [x] 4.1 创建 GhostBoxKeyboardInterface 类
    - 在 `Infrastructure/` 目录创建 `GhostBoxKeyboardInterface.cs`
    - 实现 IKeyboardInterface 接口
    - 使用 GhostBoxDeviceManager 进行设备通信
    - _需求: 3.1, 3.4_
  - [x] 4.2 创建 GhostBoxMouseInterface 类
    - 在 `Infrastructure/` 目录创建 `GhostBoxMouseInterface.cs`
    - 实现 IMouseInterface 接口
    - 与键盘驱动共享 GhostBoxDeviceManager
    - _需求: 4.2, 4.3, 4.4_
  - [x] 4.3 编写属性测试 - 设备连接共享
    - **属性 3: 设备连接共享**
    - **验证: 需求 4.4**

- [x] 5. 驱动管理器实现
  - [x] 5.1 创建 InputDriverManager 类
    - 在 `Core/Services/` 目录创建 `InputDriverManager.cs`
    - 实现驱动创建和切换逻辑
    - 实现切换失败回退机制
    - 实现事件通知
    - _需求: 6.1, 6.2, 6.3_
  - [x] 5.2 编写属性测试 - 驱动切换有效性
    - **属性 2: 驱动切换有效性**
    - **验证: 需求 6.1, 6.2**
  - [x] 5.3 编写属性测试 - 切换失败回退
    - **属性 5: 切换失败回退**
    - **验证: 需求 3.3, 6.3**
  - [x] 5.4 编写属性测试 - 连接状态文本
    - **属性 4: 连接状态文本正确性**
    - **验证: 需求 5.2**

- [x] 6. 配置持久化
  - [x] 6.1 扩展配置模型
    - 在 `Models/AppSettings.cs` 添加 InputDriverType 属性
    - 更新 ConfigManager 支持驱动类型配置
    - _需求: 7.1_
  - [x] 6.2 编写属性测试 - 配置往返一致性
    - **属性 1: 配置往返一致性**
    - **验证: 需求 2.2, 2.3, 7.1**
  - [x] 6.3 编写单元测试 - 默认驱动
    - **属性 6: 默认驱动**
    - 测试配置缺失时使用 Win32 默认值
    - **验证: 需求 2.4, 7.2**

- [x] 7. 检查点 - 核心功能验证
  - 确保所有测试通过
  - 验证 GhostBox DLL 加载逻辑
  - 如有问题请询问用户

- [x] 8. UI 集成
  - [x] 8.1 更新设置页面 XAML
    - 在 `MainWindow.xaml` 的设置页面添加驱动选择下拉框
    - 添加 GhostBox 连接状态显示区域
    - 添加重新连接按钮
    - _需求: 2.1, 5.1, 5.3_
  - [x] 8.2 更新 ViewModel 绑定
    - 在 `MainViewModel` 添加驱动选择相关属性和命令
    - 实现驱动切换命令
    - 实现重新连接命令
    - _需求: 5.4, 6.1_
  - [x] 8.3 集成 InputDriverManager 到引擎
    - 更新 `MainViewModel` 使用 InputDriverManager
    - 确保引擎使用选中的驱动
    - _需求: 6.2_

- [x] 9. 最终检查点
  - 确保所有测试通过
  - 验证 UI 驱动切换功能
  - 验证配置持久化
  - 如有问题请询问用户

## 备注

- 所有任务均为必做任务
- 每个任务引用了对应的需求编号以便追溯
- 属性测试验证设计文档中定义的正确性属性
