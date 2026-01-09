"""
调试工具模块
提供可视化调试和实时监控功能
"""
import time
import threading
import json
from typing import Dict, Any, List, Optional
from dataclasses import dataclass
from collections import deque


@dataclass
class 调试事件:
    """调试事件数据类"""
    时间戳: float
    类型: str  # "检测", "决策", "执行", "错误"
    数据: Dict[str, Any]
    截图数据: Optional[bytes] = None


class 可视化调试器:
    """
    可视化调试器
    实时显示检测区域和匹配结果
    """
    
    def __init__(self, 历史记录数量: int = 100):
        """
        初始化调试器
        
        参数:
            历史记录数量: 保留的历史记录数量
        """
        self.历史记录 = deque(maxlen=历史记录数量)
        self.实时数据 = {
            "当前检测区域": None,
            "匹配结果": [],
            "技能状态": {},
            "性能指标": {}
        }
        self.回调函数 = []
        self.锁 = threading.Lock()
        self.启用 = False
    
    def 启用调试(self):
        """启用调试功能"""
        self.启用 = True
        print("可视化调试器已启用")
    
    def 禁用调试(self):
        """禁用调试功能"""
        self.启用 = False
        print("可视化调试器已禁用")
    
    def 记录检测事件(self, 检测区域: Dict, 匹配结果: List[Dict], 截图数据: bytes = None):
        """
        记录检测事件
        
        参数:
            检测区域: 检测区域信息
            匹配结果: 匹配结果列表
            截图数据: 截图数据（可选）
        """
        if not self.启用:
            return
        
        事件 = 调试事件(
            时间戳=time.time(),
            类型="检测",
            数据={
                "检测区域": 检测区域,
                "匹配结果": 匹配结果,
                "匹配数量": len(匹配结果)
            },
            截图数据=截图数据
        )
        
        with self.锁:
            self.历史记录.append(事件)
            self.实时数据["当前检测区域"] = 检测区域
            self.实时数据["匹配结果"] = 匹配结果
            
            # 通知回调函数
            for 回调 in self.回调函数:
                try:
                    回调("检测", 事件)
                except Exception as e:
                    print(f"调试回调执行失败: {e}")
    
    def 记录决策事件(self, 技能键值: int, 决策依据: Dict):
        """
        记录决策事件
        
        参数:
            技能键值: 选择的技能键值
            决策依据: 决策依据信息
        """
        if not self.启用:
            return
        
        事件 = 调试事件(
            时间戳=time.time(),
            类型="决策",
            数据={
                "技能键值": 技能键值,
                "决策依据": 决策依据
            }
        )
        
        with self.锁:
            self.历史记录.append(事件)
            
            # 通知回调函数
            for 回调 in self.回调函数:
                try:
                    回调("决策", 事件)
                except Exception as e:
                    print(f"调试回调执行失败: {e}")
    
    def 记录执行事件(self, 技能键值: int, 成功: bool, 执行时间: float):
        """
        记录执行事件
        
        参数:
            技能键值: 执行的技能键值
            成功: 是否执行成功
            执行时间: 执行耗时
        """
        if not self.启用:
            return
        
        事件 = 调试事件(
            时间戳=time.time(),
            类型="执行",
            数据={
                "技能键值": 技能键值,
                "成功": 成功,
                "执行时间": 执行时间
            }
        )
        
        with self.锁:
            self.历史记录.append(事件)
            
            # 通知回调函数
            for 回调 in self.回调函数:
                try:
                    回调("执行", 事件)
                except Exception as e:
                    print(f"调试回调执行失败: {e}")
    
    def 记录错误事件(self, 错误类型: str, 错误信息: str, 上下文: Dict = None):
        """
        记录错误事件
        
        参数:
            错误类型: 错误类型
            错误信息: 错误信息
            上下文: 错误上下文（可选）
        """
        事件 = 调试事件(
            时间戳=time.time(),
            类型="错误",
            数据={
                "错误类型": 错误类型,
                "错误信息": 错误信息,
                "上下文": 上下文 or {}
            }
        )
        
        with self.锁:
            self.历史记录.append(事件)
            
            # 通知回调函数（即使调试禁用也记录错误）
            for 回调 in self.回调函数:
                try:
                    回调("错误", 事件)
                except Exception as e:
                    print(f"调试回调执行失败: {e}")
    
    def 注册回调函数(self, 回调函数):
        """
        注册回调函数
        
        参数:
            回调函数: 回调函数
        """
        with self.锁:
            self.回调函数.append(回调函数)
    
    def 获取实时数据(self) -> Dict[str, Any]:
        """
        获取实时调试数据
        
        返回:
            dict: 实时数据
        """
        with self.锁:
            return self.实时数据.copy()
    
    def 获取历史记录(self, 数量: int = 10) -> List[调试事件]:
        """
        获取历史记录
        
        参数:
            数量: 获取的记录数量
        
        返回:
            list: 调试事件列表
        """
        with self.锁:
            return list(self.历史记录)[-数量:]
    
    def 生成调试报告(self) -> Dict[str, Any]:
        """
        生成调试报告
        
        返回:
            dict: 调试报告
        """
        with self.锁:
            历史记录 = list(self.历史记录)
            
            # 统计不同类型的事件
            事件统计 = {}
            for 事件 in 历史记录:
                事件统计[事件.类型] = 事件统计.get(事件.类型, 0) + 1
            
            # 分析最近的执行事件
            最近执行事件 = [事件 for 事件 in 历史记录 if 事件.类型 == "执行"][-10:]
            执行成功率 = sum(1 for 事件 in 最近执行事件 if 事件.数据["成功"]) / len(最近执行事件) if 最近执行事件 else 0
            
            return {
                "启用状态": self.启用,
                "事件统计": 事件统计,
                "最近执行成功率": 执行成功率,
                "实时数据": self.实时数据,
                "历史记录数量": len(历史记录)
            }
    
    def 清空历史记录(self):
        """清空历史记录"""
        with self.锁:
            self.历史记录.clear()
            self.实时数据 = {
                "当前检测区域": None,
                "匹配结果": [],
                "技能状态": {},
                "性能指标": {}
            }


class 技能状态图表:
    """
    技能状态变化历史图表
    """
    
    def __init__(self, 历史记录数量: int = 100):
        """初始化图表"""
        self.技能状态历史 = deque(maxlen=历史记录数量)
        self.时间窗口 = 60  # 60秒时间窗口
    
    def 记录技能状态(self, 技能名称: str, 可用状态: bool, 时间戳: float = None):
        """
        记录技能状态变化
        
        参数:
            技能名称: 技能名称
            可用状态: 是否可用
            时间戳: 时间戳（可选，默认当前时间）
        """
        if 时间戳 is None:
            时间戳 = time.time()
        
        self.技能状态历史.append({
            "技能名称": 技能名称,
            "可用状态": 可用状态,
            "时间戳": 时间戳
        })
    
    def 获取技能状态趋势(self, 技能名称: str, 时间窗口: float = None) -> Dict[str, Any]:
        """
        获取技能状态趋势
        
        参数:
            技能名称: 技能名称
            时间窗口: 时间窗口长度（秒）
        
        返回:
            dict: 技能状态趋势
        """
        if 时间窗口 is None:
            时间窗口 = self.时间窗口
        
        当前时间 = time.time()
        开始时间 = 当前时间 - 时间窗口
        
        # 筛选指定时间窗口内的记录
        相关记录 = [
            记录 for 记录 in self.技能状态历史
            if 记录["技能名称"] == 技能名称 and 记录["时间戳"] >= 开始时间
        ]
        
        if not 相关记录:
            return {"技能名称": 技能名称, "可用率": 0.0, "状态变化次数": 0}
        
        # 计算可用率
        可用次数 = sum(1 for 记录 in 相关记录 if 记录["可用状态"])
        可用率 = 可用次数 / len(相关记录)
        
        # 计算状态变化次数
        状态变化次数 = 0
        上一个状态 = None
        for 记录 in 相关记录:
            if 上一个状态 is not None and 记录["可用状态"] != 上一个状态:
                状态变化次数 += 1
            上一个状态 = 记录["可用状态"]
        
        return {
            "技能名称": 技能名称,
            "可用率": 可用率,
            "状态变化次数": 状态变化次数,
            "样本数量": len(相关记录)
        }
    
    def 生成图表数据(self, 技能列表: List[str]) -> Dict[str, Any]:
        """
        生成图表所需的数据
        
        参数:
            技能列表: 技能名称列表
        
        返回:
            dict: 图表数据
        """
        当前时间 = time.time()
        开始时间 = 当前时间 - self.时间窗口
        
        图表数据 = {}
        for 技能名称 in 技能列表:
            # 获取技能状态序列
            状态序列 = [
                {"时间": 记录["时间戳"], "状态": 1 if 记录["可用状态"] else 0}
                for 记录 in self.技能状态历史
                if 记录["技能名称"] == 技能名称 and 记录["时间戳"] >= 开始时间
            ]
            
            图表数据[技能名称] = {
                "状态序列": 状态序列,
                "趋势分析": self.获取技能状态趋势(技能名称)
            }
        
        return 图表数据


# 全局调试器实例
全局调试器 = 可视化调试器()
全局技能状态图表 = 技能状态图表()


def 调试模式(函数):
    """
    调试模式装饰器
    自动记录函数执行信息
    """
    def 包装器(*args, **kwargs):
        开始时间 = time.time()
        
        try:
            结果 = 函数(*args, **kwargs)
            
            # 记录执行事件
            全局调试器.记录执行事件(
                技能键值=kwargs.get('技能键值', 0),
                成功=True,
                执行时间=time.time() - 开始时间
            )
            
            return 结果
        except Exception as e:
            # 记录错误事件
            全局调试器.记录错误事件(
                错误类型=type(e).__name__,
                错误信息=str(e),
                上下文={"函数名": 函数.__name__, "参数": str(args)}
            )
            raise
    
    return 包装器