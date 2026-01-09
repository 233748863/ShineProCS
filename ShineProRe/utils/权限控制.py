"""
权限控制模块
提供运行时权限检查和操作频率限制
"""
import time
import os
import sys
import ctypes
from typing import Dict, Callable, Optional
from functools import wraps
from collections import defaultdict


class 权限检查器:
    """
    权限检查器
    检查运行时权限和系统要求
    """
    
    @staticmethod
    def 检查管理员权限() -> bool:
        """
        检查是否具有管理员权限
        
        返回:
            bool: 是否具有管理员权限
        """
        try:
            if os.name == 'nt':  # Windows
                return ctypes.windll.shell32.IsUserAnAdmin() != 0
            else:  # Linux/Mac
                return os.getuid() == 0
        except Exception:
            return False
    
    @staticmethod
    def 检查屏幕访问权限() -> bool:
        """
        检查是否具有屏幕访问权限
        
        返回:
            bool: 是否具有屏幕访问权限
        """
        try:
            # 尝试获取屏幕尺寸
            if os.name == 'nt':  # Windows
                import win32gui
                import win32con
                桌面句柄 = win32gui.GetDesktopWindow()
                return 桌面句柄 is not None
            else:  # Linux/Mac
                import tkinter as tk
                根窗口 = tk.Tk()
                根窗口.destroy()
                return True
        except Exception:
            return False
    
    @staticmethod
    def 检查输入设备权限() -> bool:
        """
        检查是否具有输入设备权限
        
        返回:
            bool: 是否具有输入设备权限
        """
        try:
            # 尝试模拟按键（最小化测试）
            if os.name == 'nt':  # Windows
                import win32api
                import win32con
                # 测试虚拟按键
                win32api.keybd_event(win32con.VK_CAPITAL, 0, 0, 0)
                win32api.keybd_event(win32con.VK_CAPITAL, 0, win32con.KEYEVENTF_KEYUP, 0)
                return True
            else:  # Linux/Mac
                # 简化测试，实际可能需要更复杂的检查
                return True
        except Exception:
            return False
    
    @staticmethod
    def 获取系统权限状态() -> Dict[str, bool]:
        """
        获取完整的系统权限状态
        
        返回:
            dict: 权限状态字典
        """
        return {
            "管理员权限": 权限检查器.检查管理员权限(),
            "屏幕访问权限": 权限检查器.检查屏幕访问权限(),
            "输入设备权限": 权限检查器.检查输入设备权限(),
            "文件读写权限": True,  # 通常都有
            "网络访问权限": True   # 通常都有
        }


class 频率限制器:
    """
    频率限制器
    限制操作的执行频率
    """
    
    def __init__(self):
        """初始化频率限制器"""
        self.操作历史 = defaultdict(list)
        self.限制规则 = {
            "按键操作": {"间隔": 0.1, "时间窗口": 10, "最大次数": 50},  # 每100ms，10秒内最多50次
            "技能释放": {"间隔": 0.5, "时间窗口": 30, "最大次数": 100},  # 每500ms，30秒内最多100次
            "图像识别": {"间隔": 0.2, "时间窗口": 5, "最大次数": 25},   # 每200ms，5秒内最多25次
            "配置重载": {"间隔": 1.0, "时间窗口": 60, "最大次数": 10}  # 每1秒，60秒内最多10次
        }
    
    def 检查操作频率(self, 操作类型: str) -> tuple[bool, str]:
        """
        检查操作频率是否超出限制
        
        参数:
            操作类型: 操作类型名称
        
        返回:
            tuple: (是否允许, 错误信息)
        """
        当前时间 = time.time()
        规则 = self.限制规则.get(操作类型)
        
        if not 规则:
            return True, ""  # 无限制
        
        # 清理过时记录
        时间窗口开始 = 当前时间 - 规则["时间窗口"]
        self.操作历史[操作类型] = [
            时间 for 时间 in self.操作历史[操作类型] 
            if 时间 > 时间窗口开始
        ]
        
        # 检查间隔限制
        if self.操作历史[操作类型]:
            最后操作时间 = self.操作历史[操作类型][-1]
            if 当前时间 - 最后操作时间 < 规则["间隔"]:
                return False, f"操作间隔过短，需要等待 {规则['间隔']} 秒"
        
        # 检查时间窗口限制
        if len(self.操作历史[操作类型]) >= 规则["最大次数"]:
            return False, f"时间窗口内操作次数超过限制 ({规则['最大次数']} 次)"
        
        # 记录本次操作
        self.操作历史[操作类型].append(当前时间)
        return True, ""
    
    def 设置限制规则(self, 操作类型: str, 间隔: float, 时间窗口: float, 最大次数: int):
        """
        设置操作频率限制规则
        
        参数:
            操作类型: 操作类型名称
            间隔: 最小间隔时间（秒）
            时间窗口: 时间窗口长度（秒）
            最大次数: 时间窗口内最大操作次数
        """
        self.限制规则[操作类型] = {
            "间隔": 间隔,
            "时间窗口": 时间窗口,
            "最大次数": 最大次数
        }
    
    def 获取频率统计(self, 操作类型: str = None) -> Dict:
        """
        获取操作频率统计
        
        参数:
            操作类型: 特定操作类型，None表示所有
        
        返回:
            dict: 频率统计信息
        """
        当前时间 = time.time()
        
        if 操作类型:
            if 操作类型 not in self.操作历史:
                return {"操作类型": 操作类型, "最近操作次数": 0, "频率": 0.0}
            
            最近操作 = [时间 for 时间 in self.操作历史[操作类型] if 时间 > 当前时间 - 60]
            频率 = len(最近操作) / 60.0 if 最近操作 else 0.0
            
            return {
                "操作类型": 操作类型,
                "最近操作次数": len(最近操作),
                "频率": 频率,
                "规则": self.限制规则.get(操作类型, {})
            }
        else:
            # 返回所有操作类型的统计
            统计信息 = {}
            for 操作类型 in self.限制规则.keys():
                统计信息[操作类型] = self.获取频率统计(操作类型)
            return 统计信息


class 权限控制器:
    """
    权限控制器
    综合权限检查和频率限制
    """
    
    def __init__(self):
        """初始化权限控制器"""
        self.权限检查器 = 权限检查器()
        self.频率限制器 = 频率限制器()
    
    def 检查操作权限(self, 操作类型: str) -> tuple[bool, str]:
        """
        检查操作权限和频率限制
        
        参数:
            操作类型: 操作类型名称
        
        返回:
            tuple: (是否允许, 错误信息)
        """
        # 检查系统权限
        权限状态 = self.权限检查器.获取系统权限状态()
        
        if 操作类型 in ["按键操作", "技能释放"]:
            if not 权限状态["输入设备权限"]:
                return False, "缺少输入设备权限"
        
        if 操作类型 in ["图像识别"]:
            if not 权限状态["屏幕访问权限"]:
                return False, "缺少屏幕访问权限"
        
        # 检查频率限制
        return self.频率限制器.检查操作频率(操作类型)
    
    def 获取权限状态报告(self) -> Dict:
        """
        获取完整的权限状态报告
        
        返回:
            dict: 权限状态报告
        """
        return {
            "系统权限": self.权限检查器.获取系统权限状态(),
            "频率限制": self.频率限制器.获取频率统计(),
            "时间戳": time.time()
        }


# 全局权限控制器实例
全局权限控制器 = 权限控制器()


def 需要权限(操作类型: str):
    """
    装饰器：为函数添加权限检查
    
    参数:
        操作类型: 操作类型名称
    """
    def 装饰器(函数: Callable) -> Callable:
        @wraps(函数)
        def 包装器(*args, **kwargs) -> Optional[Any]:
            允许, 错误信息 = 全局权限控制器.检查操作权限(操作类型)
            
            if not 允许:
                print(f"权限检查失败 ({操作类型}): {错误信息}")
                return None
            
            return 函数(*args, **kwargs)
        return 包装器
    return 装饰器


def 需要管理员权限():
    """装饰器：检查管理员权限"""
    def 装饰器(函数: Callable) -> Callable:
        @wraps(函数)
        def 包装器(*args, **kwargs) -> Optional[Any]:
            if not 权限检查器.检查管理员权限():
                print("需要管理员权限才能执行此操作")
                return None
            
            return 函数(*args, **kwargs)
        return 包装器
    return 装饰器