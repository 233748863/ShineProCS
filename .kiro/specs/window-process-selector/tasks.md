# Implementation Plan: Window Process Selector

## Overview

本实现计划将窗口捕获设置从手动输入窗口标题改为下拉选择进程/窗口的方式，包括创建窗口枚举服务、更新 ViewModel 和 UI。

## Tasks

- [x] 1. 创建 WindowInfo 模型和窗口枚举服务
  - [x] 1.1 创建 WindowInfo 模型类
    - 在 Models 目录创建 WindowInfo.cs
    - 包含 Handle, Title, ProcessName, ProcessId, DisplayText 属性
    - _Requirements: 1.2, 2.1_

  - [x] 1.2 创建 IWindowEnumerationService 接口
    - 在 Core/Interfaces 目录创建接口
    - 定义 GetVisibleWindows, IsWindowValid, FindWindowByTitle 方法
    - _Requirements: 1.1, 5.1_

  - [x] 1.3 实现 WindowEnumerationService
    - 在 Core/Services 目录创建实现类
    - 使用 Win32 API (EnumWindows, IsWindowVisible, GetWindowText, GetWindowThreadProcessId)
    - 实现窗口过滤逻辑（排除无标题和不可见窗口）
    - _Requirements: 1.1, 1.2, 1.3, 5.1_

  - [x] 1.4 编写属性测试：窗口列表只包含有效窗口
    - **Property 1: Window List Contains Only Visible Windows with Titles**
    - **Validates: Requirements 1.1, 1.2, 1.3**

  - [x] 1.5 编写属性测试：窗口显示格式一致性
    - **Property 2: Window Display Format Consistency**
    - **Validates: Requirements 2.1**

- [x] 2. 更新 MainViewModel
  - [x] 2.1 添加窗口列表相关属性
    - 添加 WindowList (ObservableCollection<WindowInfo>)
    - 添加 SelectedWindow (WindowInfo?)
    - 添加 IsRefreshingWindows (bool)
    - _Requirements: 2.2, 2.4_

  - [x] 2.2 实现刷新窗口列表命令
    - 创建 RefreshWindowListCommand
    - 异步获取窗口列表
    - 保持当前选择（如果窗口仍存在）
    - _Requirements: 4.2, 4.4_

  - [x] 2.3 实现窗口选择变化处理
    - 在 OnSelectedWindowChanged 中更新 AppSettings.GameWindowTitle
    - _Requirements: 2.2, 3.1_

  - [x] 2.4 实现启动时窗口匹配
    - 在初始化时根据保存的标题查找并选择窗口
    - _Requirements: 3.2, 3.3, 3.4_

  - [x] 2.5 编写属性测试：选择更新配置
    - **Property 3: Selection Updates Configuration**
    - **Validates: Requirements 2.2, 3.1**

  - [x] 2.6 编写属性测试：启动窗口匹配
    - **Property 4: Startup Window Matching**
    - **Validates: Requirements 3.2, 3.3, 3.4**

  - [x] 2.7 编写属性测试：刷新时选择保持
    - **Property 5: Selection Preservation on Refresh**
    - **Validates: Requirements 4.4**

- [x] 3. 更新 UI
  - [x] 3.1 修改 MainWindow.xaml 窗口捕获设置区域
    - 将 TextBox 替换为 ComboBox
    - 添加刷新按钮
    - 设置 ComboBox 的 ItemsSource, SelectedItem, DisplayMemberPath
    - 添加占位符文本
    - _Requirements: 2.1, 2.3, 2.4, 4.1_

  - [x] 3.2 添加刷新按钮加载状态
    - 绑定 IsRefreshingWindows 属性
    - 显示加载指示器
    - _Requirements: 4.3_

- [x] 4. 窗口有效性验证
  - [x] 4.1 实现引擎启动时的窗口验证
    - 在引擎启动前验证选中窗口是否存在
    - 如果不存在，显示警告消息
    - _Requirements: 5.1, 5.2_

  - [x] 4.2 编写属性测试：窗口有效性检查
    - **Property 6: Window Validity Check**
    - **Validates: Requirements 5.1**

- [x] 5. Checkpoint - 确保所有测试通过
  - 确保所有测试通过，如有问题请询问用户

## Notes

- All tasks are required for complete implementation
- Each task references specific requirements for traceability
- Property tests validate universal correctness properties
- Unit tests validate specific examples and edge cases
