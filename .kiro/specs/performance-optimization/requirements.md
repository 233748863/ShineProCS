# 需求文档

## 简介

本文档定义了 ShineProCS 项目的性能优化需求，涵盖图像检测算法优化、Buff 检测优化、血条/蓝条检测优化、对象池优化、GPU/CPU 灵活切换以及 GhostBox 输入优化六个方面。

## 术语表

- **StateDetector**: 游戏状态检测器，负责检测 HP/MP、技能状态、Buff 状态等
- **MatPool**: OpenCV Mat 对象池，用于复用 Mat 对象减少 GC 压力
- **GhostBox**: 硬件级键盘鼠标模拟设备
- **WGC**: Windows Graphics Capture，Windows 图形捕获 API
- **ROI**: Region of Interest，感兴趣区域
- **HSV**: Hue-Saturation-Value 颜色空间
- **DirectML**: DirectX Machine Learning，微软的 GPU 加速机器学习 API（Windows 通用，无需额外驱动）

## 需求

### 需求 1: 图像检测算法优化

**用户故事:** 作为用户，我希望图像检测更快更高效，以便技能循环引擎能够更快响应且 CPU 占用更低。

#### 验收标准

1. WHEN 检测帧变化时, THE StateDetector SHALL 使用可配置的采样步长减少计算量
2. WHEN 需要多个检测区域时, THE StateDetector SHALL 截取单个边界框大图并从中提取 ROI
3. WHEN 技能处于冷却中（剩余 CD > 0.5秒）时, THE StateDetector SHALL 跳过该技能的视觉检测
4. WHEN 执行模板匹配时, THE StateDetector SHALL 可选地将图像缩放到 50% 以加速匹配
5. THE StateDetector SHALL 维护帧差分缓存以避免重复处理未变化的帧

### 需求 2: Buff 检测优化

**用户故事:** 作为用户，我希望 Buff 检测更准确高效，以便基于 Buff 的技能条件能可靠触发。

#### 验收标准

1. WHEN 检查 Buff 是否存在时, THE StateDetector SHALL 首先尝试从缓存帧中获取区域
2. WHEN 加载 Buff 模板时, THE TemplateCache SHALL 使用 LRU 淘汰策略存储
3. WHEN 执行 Buff 模板匹配时, THE StateDetector SHALL 支持多尺度匹配以适应不同 UI 缩放
4. THE StateDetector SHALL 同时支持模板匹配和亮度检测两种 Buff 检测方式
5. WHEN Buff 检测失败时, THE StateDetector SHALL 记录失败日志并返回安全的默认值

### 需求 3: 血条/蓝条检测优化

**用户故事:** 作为用户，我希望 HP/MP 条检测更快更准确，以便基于血量的技能条件能正确触发。

#### 验收标准

1. WHEN 检测 HP/MP 百分比时, THE StateDetector SHALL 使用沿条中线的采样点代替全区域 HSV 扫描
2. WHEN HP/MP 检测连续失败时, THE StateDetector SHALL 返回上次有效的缓存值
3. THE StateDetector SHALL 支持可配置的 HSV 阈值以适应不同游戏 UI 主题
4. WHEN 检测血条时, THE StateDetector SHALL 同时识别红色（受伤）和绿色（治疗）配色方案
5. THE StateDetector SHALL 将连续检测失败次数限制在可配置的最大值，超过后使用缓存值

### 需求 4: 对象池优化

**用户故事:** 作为开发者，我希望最小化频繁对象分配带来的 GC 压力，以便应用程序运行流畅无卡顿。

#### 验收标准

1. THE MatPool SHALL 提供 Rent/Return 方法用于 Mat 对象复用
2. THE MatPool SHALL 在池容量超出时自动释放多余对象
3. THE ObjectPool SHALL 支持泛型类型，可配置工厂函数和重置函数
4. THE ByteArrayPool SHALL 为小型固定大小字节数组提供专用池化
5. WHEN 归还对象到池时, THE Pool SHALL 在接受前验证对象状态
6. THE MatPool SHALL 追踪统计信息，包括创建数、复用数和当前池大小

### 需求 5: GPU/CPU 灵活切换

**用户故事:** 作为用户，我希望能在 GPU 和 CPU 处理之间选择，以便根据我的硬件优化性能。

#### 验收标准

1. THE HardwareAccelerationConfig SHALL 支持两种推理设备类型: CPU 和 DirectML (GPU)
2. WHEN GPU 推理失败时, THE BgiOnnxFactory SHALL 自动回退到 CPU
3. THE BgiOnnxFactory SHALL 支持运行时在 GPU 和 CPU 模式之间切换
4. WHEN 使用 DirectML 时, THE SessionOptions SHALL 禁用内存模式并使用顺序执行
5. THE HardwareAccelerationConfig SHALL 允许强制 OCR 使用 CPU，即使其他任务启用了 GPU

### 需求 6: GhostBox 输入优化

**用户故事:** 作为用户，我希望 GhostBox 输入更可靠且更像人类操作，以便输入模拟更稳定且不易被检测。

#### 验收标准

1. THE GhostBoxDeviceManager SHALL 支持可配置的输入延迟随机化
2. WHEN 执行按键时, THE GhostBoxKeyboardInterface SHALL 在配置范围内添加随机延迟
3. THE GhostBoxMouseInterface SHALL 支持贝塞尔曲线鼠标移动以模拟人类动作
4. WHEN 设备在操作期间断开时, THE GhostBoxDeviceManager SHALL 优雅处理错误并通知引擎
5. THE GhostBoxDeviceManager SHALL 支持自动重连尝试，重试间隔可配置
6. WHEN 执行快速按键序列时, THE GhostBoxKeyboardInterface SHALL 强制执行最小按键间隔

