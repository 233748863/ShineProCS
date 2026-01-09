"""
日志管理模块
提供分级日志记录和日志文件管理
"""
import time
import os
import sys
from typing import Optional, Dict, Any
from datetime import datetime
from enum import Enum


class 日志级别(Enum):
    """日志级别枚举"""
    调试 = 0
    信息 = 1
    警告 = 2
    错误 = 3
    致命 = 4


class 日志管理器:
    """
    日志管理器
    提供分级日志记录和管理功能
    """
    
    def __init__(self, 日志目录: str = "./logs/", 启用文件日志: bool = True):
        """
        初始化日志管理器
        
        参数:
            日志目录: 日志文件目录
            启用文件日志: 是否启用文件日志
        """
        self.日志目录 = 日志目录
        self.启用文件日志 = 启用文件日志
        self.当前日志文件 = None
        self.日志级别 = 日志级别.信息
        
        # 创建日志目录
        if self.启用文件日志:
            os.makedirs(self.日志目录, exist_ok=True)
            self._切换日志文件()
    
    def _切换日志文件(self):
        """切换日志文件（按日期）"""
        日期 = datetime.now().strftime("%Y-%m-%d")
        日志文件名 = f"技能循环引擎_{日期}.log"
        self.当前日志文件 = os.path.join(self.日志目录, 日志文件名)
    
    def 设置日志级别(self, 级别: 日志级别):
        """
        设置日志级别
        
        参数:
            级别: 日志级别
        """
        self.日志级别 = 级别
        self.记录日志(日志级别.信息, f"日志级别设置为: {级别.name}")
    
    def 记录日志(self, 级别: 日志级别, 消息: str, 额外数据: Dict[str, Any] = None):
        """
        记录日志
        
        参数:
            级别: 日志级别
            消息: 日志消息
            额外数据: 额外数据（可选）
        """
        if 级别.value < self.日志级别.value:
            return  # 低于当前级别的日志不记录
        
        # 检查是否需要切换日志文件
        if self.启用文件日志:
            当前日期 = datetime.now().strftime("%Y-%m-%d")
            if not self.当前日志文件 or 当前日期 not in self.当前日志文件:
                self._切换日志文件()
        
        # 格式化日志消息
        时间戳 = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")[:-3]
        级别名称 = 级别.name
        
        日志消息 = f"[{时间戳}] [{级别名称}] {消息}"
        
        if 额外数据:
            日志消息 += f" | 额外数据: {额外数据}"
        
        # 输出到控制台
        if 级别 in [日志级别.错误, 日志级别.致命]:
            print(日志消息, file=sys.stderr)
        else:
            print(日志消息)
        
        # 记录到文件
        if self.启用文件日志 and self.当前日志文件:
            try:
                with open(self.当前日志文件, "a", encoding="utf-8") as f:
                    f.write(日志消息 + "\n")
            except Exception as e:
                print(f"写入日志文件失败: {e}", file=sys.stderr)
    
    def 调试(self, 消息: str, 额外数据: Dict[str, Any] = None):
        """记录调试日志"""
        self.记录日志(日志级别.调试, 消息, 额外数据)
    
    def 信息(self, 消息: str, 额外数据: Dict[str, Any] = None):
        """记录信息日志"""
        self.记录日志(日志级别.信息, 消息, 额外数据)
    
    def 警告(self, 消息: str, 额外数据: Dict[str, Any] = None):
        """记录警告日志"""
        self.记录日志(日志级别.警告, 消息, 额外数据)
    
    def 错误(self, 消息: str, 额外数据: Dict[str, Any] = None):
        """记录错误日志"""
        self.记录日志(日志级别.错误, 消息, 额外数据)
    
    def 致命(self, 消息: str, 额外数据: Dict[str, Any] = None):
        """记录致命错误日志"""
        self.记录日志(日志级别.致命, 消息, 额外数据)
    
    def 获取日志文件列表(self) -> list:
        """
        获取日志文件列表
        
        返回:
            list: 日志文件列表
        """
        if not os.path.exists(self.日志目录):
            return []
        
        日志文件 = []
        for 文件名 in os.listdir(self.日志目录):
            if 文件名.endswith(".log"):
                文件路径 = os.path.join(self.日志目录, 文件名)
                文件信息 = {
                    "文件名": 文件名,
                    "文件路径": 文件路径,
                    "大小": os.path.getsize(文件路径),
                    "修改时间": datetime.fromtimestamp(os.path.getmtime(文件路径))
                }
                日志文件.append(文件信息)
        
        # 按修改时间排序（最新的在前）
        日志文件.sort(key=lambda x: x["修改时间"], reverse=True)
        return 日志文件
    
    def 清理旧日志(self, 保留天数: int = 7):
        """
        清理旧的日志文件
        
        参数:
            保留天数: 保留多少天内的日志文件
        """
        if not os.path.exists(self.日志目录):
            return
        
        当前时间 = datetime.now()
        删除文件数量 = 0
        
        for 文件信息 in self.获取日志文件列表():
            文件年龄 = (当前时间 - 文件信息["修改时间"]).days
            
            if 文件年龄 > 保留天数:
                try:
                    os.remove(文件信息["文件路径"])
                    删除文件数量 += 1
                    self.信息(f"清理旧日志文件: {文件信息['文件名']}")
                except Exception as e:
                    self.错误(f"删除日志文件失败: {文件信息['文件名']}, 错误: {e}")
        
        if 删除文件数量 > 0:
            self.信息(f"清理完成，共删除 {删除文件数量} 个旧日志文件")
    
    def 获取日志统计(self) -> Dict[str, Any]:
        """
        获取日志统计信息
        
        返回:
            dict: 日志统计信息
        """
        日志文件列表 = self.获取日志文件列表()
        总大小 = sum(文件["大小"] for 文件 in 日志文件列表)
        
        return {
            "日志级别": self.日志级别.name,
            "启用文件日志": self.启用文件日志,
            "日志文件数量": len(日志文件列表),
            "日志总大小": 总大小,
            "当前日志文件": self.当前日志文件,
            "最近日志文件": 日志文件列表[0] if 日志文件列表 else None
        }


# 全局日志管理器实例
全局日志管理器 = 日志管理器()


def 日志装饰器(级别: 日志级别 = 日志级别.信息):
    """
    日志装饰器
    自动记录函数调用信息
    
    参数:
        级别: 日志级别
    """
    def 装饰器(函数):
        def 包装器(*args, **kwargs):
            函数名 = 函数.__name__
            开始时间 = time.time()
            
            # 记录函数开始
            全局日志管理器.记录日志(级别, f"函数调用开始: {函数名}", {"参数": str(args), "关键字参数": str(kwargs)})
            
            try:
                结果 = 函数(*args, **kwargs)
                执行时间 = time.time() - 开始时间
                
                # 记录函数结束
                全局日志管理器.记录日志(级别, f"函数调用结束: {函数名}", {"执行时间": 执行时间, "结果": str(结果)})
                
                return 结果
            except Exception as e:
                # 记录错误
                全局日志管理器.错误(f"函数调用异常: {函数名}", {"异常": str(e), "参数": str(args)})
                raise
        
        return 包装器
    return 装饰器


def 快速记录调试(消息: str):
    """快速记录调试信息（简化调用）"""
    全局日志管理器.调试(消息)


def 快速记录信息(消息: str):
    """快速记录信息（简化调用）"""
    全局日志管理器.信息(消息)


def 快速记录错误(消息: str):
    """快速记录错误（简化调用）"""
    全局日志管理器.错误(消息)