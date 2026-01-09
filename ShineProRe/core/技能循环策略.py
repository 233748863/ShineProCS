"""
技能循环策略接口
定义不同技能循环模式的策略接口
"""
from abc import ABC, abstractmethod
from typing import Dict, Any


class 技能循环策略(ABC):
    """技能循环策略抽象接口"""
    
    @abstractmethod
    def 推算技能(self, 技能状态检测器, 图像获取接口, 技能字典: Dict[str, Any], 
                气劲字典: Dict[str, Any], 蓝条配置: Dict[str, Any], 
                检测区域: tuple, 七情和合状态: int) -> int:
        """
        根据当前状态推算下一个要释放的技能
        
        参数:
            技能状态检测器: 技能状态检测器实例
            图像获取接口: 图像获取接口实例
            技能字典: 技能配置字典
            气劲字典: 气劲配置字典
            蓝条配置: 蓝条监控配置
            检测区域: 屏幕检测区域 (x, y, width, height)
            七情和合状态: 七情和合状态标记
            
        返回:
            int: 技能键值（可释放时）或 0（无技能可释放）
        """
        pass