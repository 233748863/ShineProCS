# Requirements Document

## Introduction

本功能将改进窗口捕获设置，将原有的手动输入窗口标题方式改为下拉选择进程/窗口的方式。用户可以通过刷新按钮获取当前运行的窗口列表，然后从下拉框中选择目标窗口，提升用户体验和操作便捷性。

## Glossary

- **Window_Selector**: 窗口选择器组件，用于显示和选择目标窗口
- **Process_List**: 进程列表，包含当前系统中运行的所有可见窗口进程
- **Window_Info**: 窗口信息，包含窗口句柄、标题、进程名等信息
- **Refresh_Button**: 刷新按钮，用于重新获取当前窗口列表

## Requirements

### Requirement 1: 窗口列表获取

**User Story:** As a user, I want to get a list of all visible windows, so that I can select the target window from a dropdown.

#### Acceptance Criteria

1. WHEN the user clicks the Refresh_Button, THE Window_Selector SHALL retrieve all visible windows with non-empty titles
2. WHEN retrieving windows, THE Window_Selector SHALL include window handle, window title, and process name for each window
3. WHEN a window has no title or is not visible, THE Window_Selector SHALL exclude it from the list
4. IF an error occurs during window enumeration, THEN THE Window_Selector SHALL display an error message and maintain the previous list

### Requirement 2: 窗口下拉选择

**User Story:** As a user, I want to select a target window from a dropdown list, so that I can easily configure window capture without typing.

#### Acceptance Criteria

1. THE Window_Selector SHALL display windows in a ComboBox with format "[进程名] 窗口标题"
2. WHEN the user selects a window from the dropdown, THE Window_Selector SHALL update the target window configuration
3. WHEN the dropdown is opened, THE Window_Selector SHALL show the currently selected window as highlighted
4. WHEN no window is selected, THE Window_Selector SHALL display a placeholder text "请选择目标窗口"

### Requirement 3: 窗口信息持久化

**User Story:** As a user, I want my window selection to be saved, so that I don't need to reselect it every time I start the application.

#### Acceptance Criteria

1. WHEN a window is selected, THE Window_Selector SHALL save the window title to the configuration
2. WHEN the application starts, THE Window_Selector SHALL attempt to match the saved window title with running windows
3. IF the saved window is found, THEN THE Window_Selector SHALL automatically select it in the dropdown
4. IF the saved window is not found, THEN THE Window_Selector SHALL clear the selection and show the placeholder

### Requirement 4: 刷新按钮交互

**User Story:** As a user, I want a refresh button next to the dropdown, so that I can update the window list when new applications are opened.

#### Acceptance Criteria

1. THE Refresh_Button SHALL be positioned next to the window dropdown
2. WHEN the Refresh_Button is clicked, THE Window_Selector SHALL update the window list immediately
3. WHILE the window list is being refreshed, THE Refresh_Button SHALL show a loading indicator
4. WHEN the refresh completes, THE Window_Selector SHALL preserve the current selection if the window still exists

### Requirement 5: 窗口有效性验证

**User Story:** As a user, I want the system to validate if my selected window is still valid, so that I know when to reselect.

#### Acceptance Criteria

1. WHEN the engine starts, THE Window_Selector SHALL verify the selected window still exists
2. IF the selected window no longer exists, THEN THE Window_Selector SHALL display a warning message
3. WHEN a window becomes invalid during operation, THE Window_Selector SHALL notify the user
