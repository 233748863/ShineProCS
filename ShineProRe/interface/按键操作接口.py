"""
按键操作接口定义
用于抽象化不同项目的按键实现
"""
from abc import ABC, abstractmethod


class 按键操作接口(ABC):
    """按键操作抽象接口"""
    
    @abstractmethod
    def 按下按键(self, 键值: int) -> bool:
        """
        按下指定键值的按键
        
        参数:
            键值: 按键的键值代码
            
        返回:
            bool: 操作是否成功
        """
        pass
    
    @abstractmethod
    def 释放按键(self, 键值: int) -> bool:
        """
        释放指定键值的按键
        
        参数:
            键值: 按键的键值代码
            
        返回:
            bool: 操作是否成功
        """
        pass
    
    @abstractmethod
    def 按下并释放(self, 键值: int) -> bool:
        """
        按下并立即释放指定键值的按键
        
        参数:
            键值: 按键的键值代码
            
        返回:
            bool: 操作是否成功
        """
        pass
    
    @abstractmethod
    def 释放所有按键(self) -> bool:
        """
        释放所有当前按下的按键
        
        返回:
            bool: 操作是否成功
        """
        pass