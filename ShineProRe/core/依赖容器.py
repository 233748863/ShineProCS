"""
依赖注入容器
提供类型安全的依赖管理和自动注入功能
"""
from typing import Dict, Any, Type, Callable, Optional
from abc import ABC, abstractmethod


class 依赖容器:
    """简单的依赖注入容器"""
    
    def __init__(self):
        self._单例缓存: Dict[Type, Any] = {}
        self._工厂函数: Dict[Type, Callable] = {}
        self._接口映射: Dict[Type, Type] = {}
    
    def 注册单例(self, 接口类型: Type, 实现实例: Any):
        """注册单例实例"""
        self._单例缓存[接口类型] = 实现实例
    
    def 注册工厂(self, 接口类型: Type, 工厂函数: Callable):
        """注册工厂函数"""
        self._工厂函数[接口类型] = 工厂函数
    
    def 注册实现(self, 接口类型: Type, 实现类型: Type):
        """注册接口到实现的映射"""
        self._接口映射[接口类型] = 实现类型
    
    def 获取实例(self, 接口类型: Type) -> Any:
        """获取依赖实例"""
        # 1. 检查单例缓存
        if 接口类型 in self._单例缓存:
            return self._单例缓存[接口类型]
        
        # 2. 检查工厂函数
        if 接口类型 in self._工厂函数:
            实例 = self._工厂函数[接口类型]()
            self._单例缓存[接口类型] = 实例  # 缓存为单例
            return 实例
        
        # 3. 检查接口映射
        if 接口类型 in self._接口映射:
            实现类型 = self._接口映射[接口类型]
            实例 = 实现类型()
            self._单例缓存[接口类型] = 实例  # 缓存为单例
            return 实例
        
        # 4. 尝试直接实例化
        try:
            实例 = 接口类型()
            self._单例缓存[接口类型] = 实例
            return 实例
        except Exception as e:
            raise ValueError(f"无法解析依赖: {接口类型.__name__}, 错误: {e}")
    
    def 清除缓存(self):
        """清除所有缓存实例"""
        self._单例缓存.clear()


class 配置提供器接口(ABC):
    """配置提供器接口"""
    
    @abstractmethod
    def 获取配置路径(self) -> str:
        """获取配置路径"""
        pass
    
    @abstractmethod
    def 获取调试模式(self) -> bool:
        """获取调试模式设置"""
        pass


class 默认配置提供器(配置提供器接口):
    """默认配置提供器实现"""
    
    def __init__(self, 配置路径: str = "./config/", 调试模式: bool = False):
        self._配置路径 = 配置路径
        self._调试模式 = 调试模式
    
    def 获取配置路径(self) -> str:
        return self._配置路径
    
    def 获取调试模式(self) -> bool:
        return self._调试模式


# 预配置的容器工厂
class 容器工厂:
    """容器工厂，提供预配置的依赖容器"""
    
    @staticmethod
    def 创建默认容器(配置路径: str = "./config/", 调试模式: bool = False) -> 依赖容器:
        """创建默认配置的依赖容器"""
        容器 = 依赖容器()
        
        # 注册配置提供器
        配置提供器 = 默认配置提供器(配置路径, 调试模式)
        容器.注册单例(配置提供器接口, 配置提供器)
        
        return 容器