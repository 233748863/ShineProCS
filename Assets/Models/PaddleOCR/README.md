# PaddleOCR ONNX 模型

本目录用于存放 PaddleOCR 的 ONNX 模型文件。

## 目录结构

```
PaddleOCR/
├── Det/
│   ├── V4/
│   │   └── PP-OCRv4_mobile_det_infer/
│   │       └── slim.onnx              # V4 文本检测模型
│   └── V5/
│       └── PP-OCRv5_mobile_det_infer/
│           └── slim.onnx              # V5 文本检测模型
├── Rec/
│   ├── V4/
│   │   ├── PP-OCRv4_mobile_rec_infer/
│   │   │   ├── slim.onnx              # V4 中文识别模型
│   │   │   └── inference.yml          # 模型配置（含字符字典）
│   │   └── en_PP-OCRv4_mobile_rec_infer/
│   │       ├── slim.onnx              # V4 英文识别模型
│   │       └── inference.yml
│   └── V5/
│       ├── PP-OCRv5_mobile_rec_infer/
│       │   ├── slim.onnx              # V5 中文识别模型
│       │   └── inference.yml
│       ├── latin_PP-OCRv5_mobile_rec_infer/
│       │   ├── slim.onnx              # V5 拉丁文识别模型
│       │   └── inference.yml
│       ├── eslav_PP-OCRv5_mobile_rec_infer/
│       │   ├── slim.onnx              # V5 斯拉夫文识别模型
│       │   └── inference.yml
│       └── korean_PP-OCRv5_mobile_rec_infer/
│           ├── slim.onnx              # V5 韩文识别模型
│           └── inference.yml
├── test_pp_ocr.png                    # 预热测试图片
└── test_pp_ocr_number.png             # 数字预热测试图片
```

## 模型下载

### 方法一：从 BetterGenshinImpact NuGet 包获取（推荐）

1. 下载 BetterGI.Assets.Model NuGet 包：
   ```powershell
   nuget install BetterGI.Assets.Model -OutputDirectory ./temp_nuget
   ```

2. 从解压的包中复制模型文件到对应目录

### 方法二：从 BetterGenshinImpact 发布版获取

1. 下载 [BetterGenshinImpact 最新发布版](https://github.com/babalae/better-genshin-impact/releases)
2. 解压后找到 `Assets/Model/PaddleOCR/` 目录
3. 复制所有文件到本项目的 `Assets/Models/PaddleOCR/` 目录

### 方法三：从 PaddleOCR 官方下载并转换

1. 访问 [PaddleOCR 模型列表](https://github.com/PaddlePaddle/PaddleOCR/blob/release/2.7/doc/doc_ch/models_list.md)
2. 下载对应版本的检测模型和识别模型
3. 使用 Paddle2ONNX 转换为 ONNX 格式：
   ```bash
   pip install paddle2onnx
   paddle2onnx --model_dir ./inference --model_filename inference.pdmodel \
               --params_filename inference.pdiparams --save_file slim.onnx
   ```

## inference.yml 配置文件格式

每个识别模型目录下需要包含 `inference.yml` 文件，格式如下：

```yaml
PostProcess:
  character_dict:
    - "字"
    - "符"
    - "列"
    - "表"
    # ... 更多字符
```

## GPU 加速支持

本项目支持多种 GPU 加速方式：
- **DirectML**: Windows 通用 GPU 加速（默认）
- **TensorRT**: NVIDIA GPU 高性能推理（需要安装 TensorRT）
- **CUDA**: NVIDIA GPU 通用加速（需要安装 CUDA）
- **OpenVINO**: Intel GPU/CPU 加速（需要安装 OpenVINO）

在 `HardwareAccelerationConfig` 中配置推理设备类型。

## 注意事项

- 模型文件较大（约 100MB+），不包含在 Git 仓库中
- 首次运行前请确保模型文件已正确放置
- TensorRT 首次运行会生成缓存文件（约 50MB），后续启动更快
- 缓存文件存放在 `Cache/{版本}/Model/` 目录下
