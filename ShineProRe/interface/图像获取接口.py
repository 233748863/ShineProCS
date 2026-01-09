"""
图像获取接口定义
用于抽象化不同项目的截图实现
"""
from abc import ABC, abstractmethod
from typing import Tuple


class 图像获取接口(ABC):
    """图像获取抽象接口"""
    
    @abstractmethod
    def 获取屏幕区域(self, 区域: Tuple[int, int, int, int]):
        """
        获取指定屏幕区域的图像
        
        参数:
            区域: (x, y, width, height) 屏幕区域坐标
            
        返回:
            Image: 图像对象，具体类型由实现决定
        """
        pass