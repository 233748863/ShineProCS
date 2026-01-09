"""
增强版配置管理器
支持环境特定配置、配置验证、默认值和配置变更回调
"""
import json
import os
import copy
from typing import Dict, Any, List, Optional, Callable
from pathlib import Path
from core.依赖容器 import 配置提供器接口


class 配置验证错误(Exception):
    """配置验证错误异常"""
    pass


class 配置管理器:
    """增强版配置管理器类，支持环境特定配置和验证"""
    
    _instance = None
    _配置缓存 = {}
    
    def __new__(cls, 配置路径: str = None):
        if cls._instance is None:
            cls._instance = super(配置管理器, cls).__new__(cls)
        return cls._instance
    
    def __init__(self, 配置路径: str = None, 环境: str = None):
        if not hasattr(self, '_初始化完成'):
            self._配置路径 = 配置路径 or "./config/"
            self._环境 = 环境 or os.environ.get("APP_ENV", "development")
            self._基本字典 = None
            self._技能字典 = None
            self._气劲字典 = None
            self._环境配置 = None
            self._初始化完成 = True
            
            # 配置变更回调列表
            self._变更回调列表: List[Callable] = []
            
            # 默认值配置
            self._默认配置 = self._获取默认配置()
            
            # 性能优化：配置验证缓存
            self._验证缓存 = {}
            self._最后验证时间 = 0
            self._验证缓存有效期 = 5.0  # 5秒缓存
            
            # 批量处理标志
            self._批量处理中 = False
            self._待处理变更 = []
            
            if self._配置路径:
                self.读取所有配置()
    
    def _获取默认配置(self) -> Dict[str, Any]:
        """获取默认配置"""
        return {
            "基本配置": {
                "技能检测区域": {
                    "坐标": [100, 100],
                    "宽高": [200, 200]
                },
                "蓝条监控": {
                    "坐标": [50, 50],
                    "宽高": [150, 20],
                    "阈值": 0.3
                },
                "自动选择最低血量": {
                    "key": 0,
                    "启用": False
                }
            },
            "技能配置": {},
            "气劲配置": {}
        }
    
    def 读取所有配置(self):
        """读取所有配置文件"""
        self.读取基本配置文件()
        self.读取技能配置文件()
        self.读取气劲配置文件()
        self.读取环境配置文件()
        
        # 验证配置
        self._验证配置()
    
    def 读取基本配置文件(self):
        """读取基本配置文件"""
        file_path = os.path.join(self._配置路径, '基本配置.json')
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                self._基本字典 = json.load(f)
        except FileNotFoundError:
            raise FileNotFoundError(f"基本配置文件不存在: {file_path}")
        except json.JSONDecodeError:
            raise ValueError(f"基本配置文件格式错误: {file_path}")
    
    def 读取技能配置文件(self):
        """读取技能配置文件"""
        file_path = os.path.join(self._配置路径, '技能配置.json')
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                self._技能字典 = json.load(f)
        except FileNotFoundError:
            raise FileNotFoundError(f"技能配置文件不存在: {file_path}")
        except json.JSONDecodeError:
            raise ValueError(f"技能配置文件格式错误: {file_path}")
    
    def 读取气劲配置文件(self):
        """读取气劲配置文件"""
        file_path = os.path.join(self._配置路径, '气劲配置.json')
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                self._气劲字典 = json.load(f)
        except FileNotFoundError:
            raise FileNotFoundError(f"气劲配置文件不存在: {file_path}")
        except json.JSONDecodeError:
            raise ValueError(f"气劲配置文件格式错误: {file_path}")
    
    def 读取环境配置文件(self):
        """读取环境特定配置文件"""
        环境文件 = os.path.join(self._配置路径, f'配置.{self._环境}.json')
        
        if os.path.exists(环境文件):
            try:
                with open(环境文件, 'r', encoding='utf-8') as f:
                    self._环境配置 = json.load(f)
                    
                # 合并环境配置到主配置
                self._合并环境配置()
            except Exception as e:
                print(f"警告: 无法读取环境配置文件 {环境文件}: {e}")
    
    def _合并环境配置(self):
        """合并环境配置到主配置"""
        if not self._环境配置:
            return
            
        def 深度合并(主配置, 环境配置):
            """深度合并两个配置字典"""
            for 键, 值 in 环境配置.items():
                if 键 in 主配置 and isinstance(主配置[键], dict) and isinstance(值, dict):
                    深度合并(主配置[键], 值)
                else:
                    主配置[键] = 值
        
        if self._基本字典 and self._环境配置.get("基本配置"):
            深度合并(self._基本字典, self._环境配置["基本配置"])
        if self._技能字典 and self._环境配置.get("技能配置"):
            深度合并(self._技能字典, self._环境配置["技能配置"])
        if self._气劲字典 and self._环境配置.get("气劲配置"):
            深度合并(self._气劲字典, self._环境配置["气劲配置"])
    
    def _验证配置(self):
        """验证配置完整性（带缓存优化）"""
        import time
        
        # 检查缓存
        当前时间 = time.time()
        缓存键 = f"{self._环境}_{hash(str(self._基本字典))}_{hash(str(self._技能字典))}_{hash(str(self._气劲字典))}"
        
        if 缓存键 in self._验证缓存:
            缓存结果, 缓存时间 = self._验证缓存[缓存键]
            if 当前时间 - 缓存时间 < self._验证缓存有效期:
                return 缓存结果
        
        # 执行验证
        try:
            # 验证基本配置
            if not self._基本字典:
                raise 配置验证错误("基本配置缺失")
            
            # 验证技能检测区域
            技能检测区域 = self._基本字典.get("技能检测区域")
            if not 技能检测区域:
                raise 配置验证错误("技能检测区域配置缺失")
            
            if not 技能检测区域.get("坐标") or not 技能检测区域.get("宽高"):
                raise 配置验证错误("技能检测区域配置不完整")
            
            # 验证蓝条监控
            蓝条配置 = self._基本字典.get("蓝条监控")
            if not 蓝条配置:
                raise 配置验证错误("蓝条监控配置缺失")
            
            if not 蓝条配置.get("坐标") or not 蓝条配置.get("宽高"):
                raise 配置验证错误("蓝条监控配置不完整")
            
            # 验证技能配置
            if not self._技能字典:
                raise 配置验证错误("技能配置缺失")
            
            # 验证气劲配置
            if not self._气劲字典:
                raise 配置验证错误("气劲配置缺失")
            
            # 缓存验证结果
            self._验证缓存[缓存键] = (True, 当前时间)
            
        except 配置验证错误 as e:
            # 缓存失败结果（但时间较短）
            self._验证缓存[缓存键] = (False, 当前时间)
            raise e
    
    def 添加配置变更回调(self, 回调函数: Callable):
        """
        添加配置变更回调函数
        
        参数:
            回调函数: 配置变更时调用的函数
        """
        self._变更回调列表.append(回调函数)
    
    def 移除配置变更回调(self, 回调函数: Callable):
        """
        移除配置变更回调函数
        
        参数:
            回调函数: 要移除的回调函数
        """
        if 回调函数 in self._变更回调列表:
            self._变更回调列表.remove(回调函数)
    
    def _触发配置变更回调(self):
        """触发所有配置变更回调"""
        for 回调函数 in self._变更回调列表:
            try:
                回调函数(self)
            except Exception as e:
                print(f"配置变更回调执行失败: {e}")
    
    @property
    def 基本字典(self) -> Dict[str, Any]:
        """获取基本配置"""
        if self._基本字典 is None:
            self.读取基本配置文件()
        return self._基本字典
    
    @property
    def 技能字典(self) -> Dict[str, Any]:
        """获取技能配置"""
        if self._技能字典 is None:
            self.读取技能配置文件()
        return self._技能字典
    
    @property
    def 气劲字典(self) -> Dict[str, Any]:
        """获取气劲配置"""
        if self._气劲字典 is None:
            self.读取气劲配置文件()
        return self._气劲字典
    
    def 重新加载配置(self):
        """重新加载所有配置文件（支持批量处理）"""
        # 如果正在批量处理中，推迟处理
        if self._批量处理中:
            self._待处理变更.append("重新加载")
            return
        
        self._基本字典 = None
        self._技能字典 = None
        self._气劲字典 = None
        self._环境配置 = None
        self.读取所有配置()
        
        # 触发配置变更回调
        self._触发配置变更回调()
    
    def 开始批量处理(self):
        """开始批量处理模式，延迟回调触发"""
        self._批量处理中 = True
        self._待处理变更 = []
    
    def 结束批量处理(self):
        """结束批量处理模式，触发积压的回调"""
        self._批量处理中 = False
        
        if self._待处理变更:
            # 合并相同的变更请求
            if "重新加载" in self._待处理变更:
                self._基本字典 = None
                self._技能字典 = None
                self._气劲字典 = None
                self._环境配置 = None
                self.读取所有配置()
                self._触发配置变更回调()
            
            # 清空待处理队列
            self._待处理变更 = []
    
    def 设置环境(self, 环境: str):
        """
        设置当前环境
        
        参数:
            环境: 环境名称（如：development, production, test）
        """
        self._环境 = 环境
        self.重新加载配置()
    
    def 获取当前环境(self) -> str:
        """获取当前环境"""
        return self._环境
    
    def 获取配置路径(self) -> str:
        """获取配置路径"""
        return self._配置路径
    
    def 验证技能配置(self, 技能名称: str) -> bool:
        """
        验证特定技能配置是否完整
        
        参数:
            技能名称: 技能名称
        
        返回:
            bool: 配置是否完整
        """
        if not self._技能字典 or 技能名称 not in self._技能字典:
            return False
        
        技能配置 = self._技能字典[技能名称]
        return all(键 in 技能配置 for 键 in ["键值", "坐标", "颜色"])
    
    def 获取配置备份(self) -> Dict[str, Any]:
        """获取当前配置的备份"""
        return {
            "基本配置": copy.deepcopy(self._基本字典) if self._基本字典 else {},
            "技能配置": copy.deepcopy(self._技能字典) if self._技能字典 else {},
            "气劲配置": copy.deepcopy(self._气劲字典) if self._气劲字典 else {},
            "环境": self._环境
        }
    
    def 恢复配置备份(self, 备份: Dict[str, Any]):
        """
        从备份恢复配置
        
        参数:
            备份: 配置备份字典
        """
        self._基本字典 = 备份.get("基本配置", {})
        self._技能字典 = 备份.get("技能配置", {})
        self._气劲字典 = 备份.get("气劲配置", {})
        self._环境 = 备份.get("环境", "development")
        
        # 触发配置变更回调
        self._触发配置变更回调()
    
    def 获取检测区域(self) -> tuple:
        """获取技能检测区域"""
        技能检测区域 = self.基本字典.get("技能检测区域")
        if not 技能检测区域:
            raise ValueError("配置文件中缺少技能检测区域配置")
        
        坐标 = 技能检测区域.get("坐标")
        宽高 = 技能检测区域.get("宽高")
        if not 坐标 or not 宽高:
            raise ValueError("技能检测区域配置不完整")
        
        return (坐标[0], 坐标[1], 宽高[0], 宽高[1])
    
    def 获取蓝条配置(self) -> Dict[str, Any]:
        """获取蓝条监控配置"""
        蓝条配置 = self.基本字典.get("蓝条监控")
        if not 蓝条配置:
            raise ValueError("配置文件中缺少蓝条监控配置")
        return 蓝条配置
    
    def 获取自动选人键值(self) -> int:
        """获取自动选择最低血量键值"""
        自动选择配置 = self.基本字典.get("自动选择最低血量")
        if not 自动选择配置:
            return 0
        return 自动选择配置.get("key", 0)