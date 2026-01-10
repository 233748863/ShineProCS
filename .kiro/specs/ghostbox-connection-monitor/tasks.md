# Implementation Plan: GhostBox Connection Monitor

## Overview

实现 GhostBox 硬件设备的实时连接状态监控功能，包括后台轮询、UI 自动更新、运行时错误处理和自动重连机制。

## Tasks

- [x] 1. 创建 ConnectionMonitor 核心组件
  - [x] 1.1 创建 ConnectionStateChangedEventArgs 事件参数类
    - 定义 IsConnected、Message、Timestamp 属性
    - _Requirements: 1.3_
  - [x] 1.2 创建 ConnectionMonitor 类基础结构
    - 定义配置属性：CheckIntervalMs、InitialReconnectIntervalMs、MaxReconnectIntervalMs、BackoffMultiplier
    - 定义状态属性：IsMonitoring、IsConnected、CurrentReconnectIntervalMs
    - 定义事件：ConnectionLost、ConnectionRestored、ReconnectAttempted
    - _Requirements: 1.1, 1.2, 1.3, 1.4_
  - [x] 1.3 实现定时检查逻辑
    - 使用 System.Timers.Timer 进行后台轮询
    - 调用 GhostBoxDeviceManager.RefreshConnectionStatus() 检查状态
    - 检测状态变化并触发相应事件
    - _Requirements: 1.1, 1.2, 1.3_
  - [x] 1.4 实现自动重连逻辑（指数退避）
    - 断开后使用指数退避策略尝试重连（2s → 4s → 8s max）
    - 重连成功后重置间隔到初始值
    - 持续重连直到成功或监控停止
    - _Requirements: 4.1, 4.2, 4.3, 4.4_
  - [x] 1.5 实现 IDisposable 资源清理
    - 停止定时器
    - 清理事件订阅
    - _Requirements: 1.4_

- [x] 2. 修改 GhostBoxDeviceManager
  - [x] 2.1 使用现有 RefreshConnectionStatus 方法
    - 调用 NativeIsConnected() 返回实时状态并更新内部属性
    - _Requirements: 1.1, 1.2_

- [x] 3. 修改 InputDriverManager 集成监控器
  - [x] 3.1 添加 ConnectionMonitor 属性和事件
    - 添加 ConnectionMonitor? 属性
    - 添加 DeviceConnectionChanged 事件
    - _Requirements: 1.3, 2.1, 2.2_
  - [x] 3.2 修改 SwitchToGhostBox 方法
    - 切换到 GhostBox 时创建并启动 ConnectionMonitor
    - 订阅监控器事件并转发
    - _Requirements: 1.1_
  - [x] 3.3 修改 SwitchToWin32 方法
    - 切换到 Win32 时停止并释放 ConnectionMonitor
    - _Requirements: 1.4_
  - [x] 3.4 修改 Dispose 方法
    - 确保释放 ConnectionMonitor
    - _Requirements: 1.4_

- [x] 4. 修改 SkillLoopEngine 添加断开保护
  - [x] 4.1 添加设备断开状态字段
    - 添加 _deviceDisconnected 和 _disconnectTime 字段
    - 定义 AutoPauseTimeoutMs = 5000
    - _Requirements: 3.1, 3.2_
  - [x] 4.2 添加设备状态处理方法
    - 实现 OnDeviceDisconnected() 方法
    - 实现 OnDeviceReconnected() 方法
    - _Requirements: 3.2, 3.3_
  - [x] 4.3 修改 MainLoop 添加断开检查
    - 在循环开始检查设备状态
    - 断开超过5秒自动暂停
    - _Requirements: 3.4_
  - [x] 4.4 修改键盘操作添加错误处理
    - 在 ExecuteInstantSkill、ExecuteCastTimeSkill、ExecuteChanneledSkill 中捕获设备断开异常
    - 记录日志并优雅处理
    - _Requirements: 3.1_

- [x] 5. 修改 MainViewModel 更新 UI 绑定
  - [x] 5.1 添加连接状态颜色属性
    - 添加 GhostBoxConnectionStatusColor 属性（使用 [ObservableProperty]）
    - _Requirements: 2.3_
  - [x] 5.2 订阅设备连接变化事件
    - 在构造函数中订阅 DeviceConnectionChanged 事件
    - _Requirements: 2.1, 2.2_
  - [x] 5.3 实现 OnDeviceConnectionChanged 事件处理方法
    - 更新 UI 绑定属性（IsGhostBoxConnected、GhostBoxConnectionStatus、GhostBoxDeviceInfo）
    - 更新 GhostBoxConnectionStatusColor（Green/Red）
    - 通知引擎设备状态变化
    - 显示 Toast 通知
    - _Requirements: 2.1, 2.2, 2.4_
  - [x] 5.4 修改 Dispose 取消订阅
    - 取消订阅 DeviceConnectionChanged 事件
    - _Requirements: 1.4_

- [x] 6. 修改 MainWindow.xaml 添加状态指示器
  - [x] 6.1 添加连接状态颜色绑定
    - 使用 DataTrigger 绑定 IsGhostBoxConnected 控制状态文本颜色
    - 已连接显示绿色 (#4CAF50)，断开显示红色 (#F44336)
    - _Requirements: 2.3_

- [x] 7. Checkpoint - 确保所有代码编译通过
  - 运行 dotnet build 验证编译
  - 确保无编译错误

- [x] 8. 编写单元测试
  - [x] 8.1 ConnectionMonitor 单元测试
    - 测试启动/停止监控（StartMonitoring_ShouldSetIsMonitoringToTrue、StopMonitoring_ShouldSetIsMonitoringToFalse）
    - 测试重置退避间隔（ResetBackoffInterval_ShouldResetToInitialValue）
    - 测试 Dispose 后停止监控（Dispose_ShouldStopMonitoring）
    - 测试默认配置值（DefaultConfig_ShouldHaveCorrectValues）
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 4.1, 4.3, 4.4_
  - [x] 8.2 Write property test for connection state synchronization
    - Property_InitialConnectionState_ShouldSyncWithDeviceManager
    - Property_BackoffInterval_ShouldStayWithinBounds
    - Property_MonitoringState_ShouldBeConsistent
    - Property_DisposedMonitor_ShouldBeSafeToOperate
    - **Property 1: Connection State Synchronization**
    - **Validates: Requirements 1.1, 1.2**

- [x] 9. Final Checkpoint - 功能验证
  - 确保所有代码编译通过
  - 所有 11 个单元测试通过
  - 如有问题请询问用户

## Notes

- 所有任务都已完成
- 本功能主要涉及后台监控和事件驱动，需要注意线程安全
- UI 更新必须在 Dispatcher 线程执行
- 测试时可以通过拔插 USB 设备验证功能

## Implementation Summary

### 已实现的核心组件

1. **ConnectionMonitor** (`Core/Services/ConnectionMonitor.cs`)
   - 完整实现了连接状态监控、指数退避重连、事件触发机制
   - 支持配置检查间隔、重连间隔、退避倍数

2. **InputDriverManager** (`Core/Services/InputDriverManager.cs`)
   - 集成了 ConnectionMonitor，在切换到 GhostBox 时自动启动监控
   - 转发连接状态变化事件到 UI 层

3. **SkillLoopEngine** (`Core/Engine/SkillLoopEngine.cs`)
   - 添加了设备断开保护，断开超过5秒自动暂停
   - 在技能执行方法中捕获设备断开异常

4. **MainViewModel** (`ViewModels/MainViewModel.cs`)
   - 订阅设备连接变化事件，更新 UI 状态
   - 显示 Toast 通知，通知引擎设备状态变化

5. **MainWindow.xaml**
   - 使用 DataTrigger 实现连接状态颜色指示

6. **单元测试** (`Tests/ConnectionMonitorTests.cs`)
   - 11 个测试用例覆盖核心功能和属性测试
