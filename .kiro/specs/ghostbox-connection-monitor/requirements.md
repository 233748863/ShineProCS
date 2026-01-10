# Requirements Document

## Introduction

本功能为 GhostBox 硬件设备添加实时连接状态监控能力。当前系统在设备意外断开（如 USB 松脱）时无法自动检测，导致 UI 状态不同步，且在运行时可能因设备不可用而报错。本功能将实现自动检测设备断开、更新 UI 状态、并在必要时优雅降级或暂停操作。

## Glossary

- **GhostBox_Device_Manager**: 管理 GhostBox 硬件设备连接的单例服务
- **Input_Driver_Manager**: 管理输入驱动切换和生命周期的服务
- **Connection_Monitor**: 定期检查设备连接状态的后台监控组件
- **Skill_Loop_Engine**: 执行技能循环的引擎，依赖输入驱动发送按键

## Requirements

### Requirement 1: 设备连接状态实时监控

**User Story:** As a user, I want the system to automatically detect when my GhostBox device is disconnected, so that I can be notified immediately and avoid runtime errors.

#### Acceptance Criteria

1. WHILE the GhostBox driver is active, THE Connection_Monitor SHALL check device connection status at a configurable interval (default 1000ms)
2. WHEN the Connection_Monitor detects device disconnection, THE System SHALL update the IsConnected property to false within 2 seconds
3. WHEN the device connection status changes, THE Connection_Monitor SHALL raise a ConnectionLost or ConnectionRestored event
4. WHEN the monitoring is no longer needed, THE Connection_Monitor SHALL stop polling and release resources

### Requirement 2: UI 状态自动同步

**User Story:** As a user, I want the UI to automatically reflect the current device connection status, so that I always know whether my device is connected.

#### Acceptance Criteria

1. WHEN the Connection_Monitor detects device disconnection, THE UI SHALL update the connection status display to "未连接" within 1 second
2. WHEN the Connection_Monitor detects device reconnection, THE UI SHALL update the connection status display to "已连接" within 1 second
3. WHEN the device is disconnected, THE UI SHALL display a visual indicator (e.g., color change) to alert the user
4. WHEN the device status changes, THE UI SHALL show a toast notification informing the user

### Requirement 3: 运行时错误优雅处理

**User Story:** As a user, I want the system to handle device disconnection gracefully during operation, so that the application doesn't crash and I can recover easily.

#### Acceptance Criteria

1. WHEN the Skill_Loop_Engine attempts to send input while device is disconnected, THE System SHALL catch the error and log it without crashing
2. WHEN the device disconnects during active skill loop, THE Skill_Loop_Engine SHALL pause execution and notify the user
3. WHEN the device reconnects after disconnection during skill loop, THE System SHALL allow the user to resume operation
4. IF the device remains disconnected for more than 5 seconds during skill loop, THEN THE System SHALL automatically switch to paused state

### Requirement 4: 自动重连机制

**User Story:** As a user, I want the system to automatically attempt to reconnect when my device is plugged back in, so that I don't have to manually click reconnect.

#### Acceptance Criteria

1. WHEN the device is detected as disconnected, THE Connection_Monitor SHALL continuously attempt automatic reconnection using exponential backoff (starting at 2 seconds, max 8 seconds)
2. WHEN automatic reconnection succeeds, THE System SHALL restore the GhostBox driver, reset the backoff interval, and notify the user
3. WHILE the device remains disconnected, THE Connection_Monitor SHALL continue reconnection attempts indefinitely until success or driver switch
4. WHEN the user manually triggers reconnection, THE System SHALL reset the backoff interval to the initial value
