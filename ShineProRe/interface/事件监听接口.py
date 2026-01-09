"""
事件监听接口定义
用于抽象化不同项目的热键监听实现
"""
from abc import ABC, abstractmethod
from typing import Callable


class 事件监听接口(ABC):
    """事件监听抽象接口"""
    
    @abstractmethod
    def 注册热键(self, 热键: str, 回调函数: Callable) -> bool:
        """
        注册热键监听
        
        参数:
            热键: 热键字符串，如 "ctrl+a"
            回调函数: 热键触发时的回调函数
            
        返回:
            bool: 注册是否成功
        """
        pass
    
    @abstractmethod
    def 开始监听(self):
        """开始监听事件"""
        pass
    
    @abstractmethod
    def 停止监听(self):
        """停止监听事件"""
        pass
    
    @abstractmethod
    def 注销热键(self, 热键: str) -> bool:
        """
        注销热键监听
        
        参数:
            热键: 热键字符串
            
        返回:
            bool: 注销是否成功
        """
        pass
    
    @abstractmethod
    def 开始监听(self):
        """开始监听事件"""
        pass
    
    @abstractmethod
    def 停止监听(self):
        """停止监听事件"""
        pass