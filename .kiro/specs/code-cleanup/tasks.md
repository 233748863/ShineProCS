# Implementation Plan: Code Cleanup

## Overview

本任务列表分为两个阶段：即时清理（确定性操作）和代码分析（需要审查）。按顺序执行以确保项目在清理过程中保持可编译状态。

## Tasks

### Phase 1: 即时清理

- [x] 1. 清理编译产物和残留文件夹
  - [x] 1.1 执行 `dotnet clean` 清理编译产物
    - 运行命令清理 bin/obj 中的编译输出
    - _Requirements: 9.1_
  - [x] 1.2 删除 Tests 文件夹残留
    - 删除 Tests/bin 和 Tests/obj 文件夹
    - 如果 Tests 文件夹为空则删除整个文件夹
    - _Requirements: 11.1, 11.2, 11.3_
  - [x] 1.3 删除 ShineProRe Python 项目
    - 确认 ShineProRe 不被 C# 项目引用
    - 删除整个 ShineProRe 文件夹
    - _Requirements: 10.1, 10.2, 10.3_

- [x] 2. Checkpoint - 验证即时清理结果
  - 确认 Tests 和 ShineProRe 文件夹已删除
  - 确认项目仍可编译 (`dotnet build`)

### Phase 2: 代码分析与清理

- [x] 3. 分析未使用的 using 语句
  - [x] 3.1 扫描所有 .cs 文件的 using 语句
    - 使用 getDiagnostics 工具检测未使用的 using
    - 记录所有 CS8019 警告（未使用的 using 指令）
    - _Requirements: 1.1, 1.2_
  - [x] 3.2 移除未使用的 using 语句
    - 根据诊断结果移除冗余 using
    - _Requirements: 1.3_

- [x] 4. 分析未使用的私有成员
  - [x] 4.1 扫描所有类的私有成员
    - 使用 getDiagnostics 检测未使用的私有字段和方法
    - 记录所有 CS0169（未使用字段）和 IDE0051（未使用私有成员）警告
    - _Requirements: 2.1, 2.2_
  - [x] 4.2 移除未使用的私有成员
    - 审查并移除确认不需要的私有成员
    - _Requirements: 2.2_

- [x] 5. 分析未使用的公共类和接口
  - [x] 5.1 扫描公共类和接口的使用情况
    - 使用 grepSearch 搜索每个公共类/接口的引用
    - 排除入口点类（App、MainWindow）
    - _Requirements: 3.1, 3.2, 3.3_
  - [x] 5.2 标记或移除未使用的公共类
    - 生成未使用公共类的列表
    - 人工审查后决定是否移除
    - _Requirements: 3.2_

- [x] 6. 分析 NuGet 包引用
  - [x] 6.1 检查 PackageReference 使用情况
    - 分析每个 NuGet 包的命名空间是否被使用
    - 检查：CommunityToolkit.Mvvm, Microsoft.Xaml.Behaviors.Wpf, OpenCvSharp4, WPF-UI
    - _Requirements: 4.1, 4.2_
  - [x] 6.2 移除未使用的 NuGet 包
    - 从 .csproj 中移除确认不需要的包引用
    - _Requirements: 4.2_

- [x] 7. Checkpoint - 验证代码分析清理结果
  - 确认项目编译成功 (`dotnet build`)
  - 确认无新增编译错误
  - 如有问题请告知

### Phase 3: 配置和资源清理

- [x] 8. 分析配置文件使用情况
  - [x] 8.1 检查 config 文件夹中的配置引用
    - 分析 appsettings.json 和 skills.json 的键值使用情况
    - 检查 presets 文件夹中的预设是否被引用
    - _Requirements: 6.1, 6.2_
  - [x] 8.2 清理未使用的配置项
    - 移除确认不需要的配置键值
    - _Requirements: 6.2_

- [x] 9. 检查 XAML 资源引用
  - [x] 9.1 验证 XAML 资源引用完整性
    - 检查 Views 中的 XAML 文件资源引用
    - 确认所有引用的资源存在
    - _Requirements: 6.3_

- [x] 10. 生成清理报告
  - [x] 10.1 汇总所有清理操作
    - 记录已删除的文件和代码
    - 记录已移除的引用和配置
    - _Requirements: 7.1, 7.2, 7.3, 7.4_

- [x] 11. Final Checkpoint - 最终验证
  - 执行 `dotnet build` 确认编译成功
  - 执行 `dotnet run` 确认应用可启动
  - 确认所有清理操作完成

## Notes

- 每个 Phase 完成后都有 Checkpoint 验证，确保项目状态正常
- Phase 1 是确定性操作，可以直接执行
- Phase 2 和 Phase 3 需要人工审查，避免误删重要代码
- 建议在执行前提交当前代码到 Git，以便回滚
