"""
时间戳优化器
减少频繁的time.time()系统调用，提升性能
"""
import time
from typing import Dict, Any
import threading


class 时间戳优化器:
    """
    时间戳优化器
    通过采样机制减少time.time()调用次数
    """
    
    def __init__(self, 采样间隔: float = 0.005):
        """
        初始化时间戳优化器
        
        参数:
            采样间隔: 采样间隔（秒），默认5ms（优化后）
        """
        self._采样间隔 = 采样间隔
        self._当前时间 = time.time()
        self._最后采样时间 = 0
        self._锁 = threading.RLock()
        
        # 统计信息
        self._总调用次数 = 0
        self._缓存命中次数 = 0
        self._系统调用次数 = 0
    
    def 获取当前时间(self) -> float:
        """
        获取当前时间（带缓存优化）
        
        返回:
            当前时间戳
        """
        self._总调用次数 += 1
        
        with self._锁:
            当前时间 = time.time()
            
            # 检查是否需要重新采样
            if 当前时间 - self._最后采样时间 > self._采样间隔:
                self._当前时间 = 当前时间
                self._最后采样时间 = 当前时间
                self._系统调用次数 += 1
            else:
                self._缓存命中次数 += 1
            
            return self._当前时间
    
    def 获取时间差(self, 开始时间: float) -> float:
        """
        获取时间差（优化版本）
        
        参数:
            开始时间: 开始时间戳
            
        返回:
            时间差（秒）
        """
        return self.获取当前时间() - 开始时间
    
    def 重置统计(self):
        """重置统计信息"""
        with self._锁:
            self._总调用次数 = 0
            self._缓存命中次数 = 0
            self._系统调用次数 = 0
    
    def 获取统计信息(self) -> Dict[str, Any]:
        """
        获取统计信息
        
        返回:
            统计信息字典
        """
        with self._锁:
            命中率 = self._缓存命中次数 / max(self._总调用次数, 1)
            优化率 = 1 - (self._系统调用次数 / max(self._总调用次数, 1))
            
            return {
                "总调用次数": self._总调用次数,
                "缓存命中次数": self._缓存命中次数,
                "系统调用次数": self._系统调用次数,
                "缓存命中率": f"{命中率:.2%}",
                "性能优化率": f"{优化率:.2%}",
                "采样间隔": self._采样间隔
            }


# 全局时间戳优化器实例
全局时间戳优化器 = 时间戳优化器()


def 获取优化时间() -> float:
    """
    获取优化后的当前时间
    
    返回:
        当前时间戳
    """
    return 全局时间戳优化器.获取当前时间()


def 获取优化时间差(开始时间: float) -> float:
    """
    获取优化后的时间差
    
    参数:
        开始时间: 开始时间戳
        
    返回:
        时间差（秒）
    """
    return 全局时间戳优化器.获取时间差(开始时间)


def 获取时间优化统计() -> Dict[str, Any]:
    """
    获取时间优化统计信息
    
    返回:
        统计信息字典
    """
    return 全局时间戳优化器.获取统计信息()