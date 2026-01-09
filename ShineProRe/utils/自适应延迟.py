"""
自适应延迟工具类
提供智能延迟调度，基于系统性能自动调整延迟时间
"""
import time
import threading
from typing import Dict, Any, Optional
from collections import deque
import math


class 自适应延迟器:
    """自适应延迟调度器"""
    
    def __init__(self, 基础延迟: float = 0.1, 最大延迟: float = 0.5, 最小延迟: float = 0.01):
        """
        初始化自适应延迟器
        
        参数:
            基础延迟: 基础延迟时间（秒）
            最大延迟: 最大允许延迟（秒）
            最小延迟: 最小允许延迟（秒）
        """
        self._基础延迟 = 基础延迟
        self._最大延迟 = 最大延迟
        self._最小延迟 = 最小延迟
        
        # 性能历史记录
        self._响应时间历史: deque = deque(maxlen=50)
        self._延迟调整历史: deque = deque(maxlen=20)
        
        # 统计信息
        self._总调用次数 = 0
        self._总延迟时间 = 0.0
        self._最后响应时间 = 0.0
        
        # 自适应参数
        self._调整因子 = 0.5  # 延迟调整因子（0-1）
        self._稳定性阈值 = 0.1  # 稳定性阈值（标准差）
        self._趋势权重 = 0.3  # 趋势预测权重
        
        # 锁保护
        self._锁 = threading.RLock()
    
    def 智能延迟(self, 预期响应时间: Optional[float] = None) -> float:
        """
        执行智能延迟
        
        参数:
            预期响应时间: 预期响应时间（秒），None表示使用自适应计算
            
        返回:
            实际延迟时间（秒）
        """
        开始时间 = time.time()
        
        # 计算最佳延迟时间
        if 预期响应时间 is None:
            延迟时间 = self._计算最佳延迟()
        else:
            延迟时间 = self._根据响应时间计算延迟(预期响应时间)
        
        # 执行延迟
        time.sleep(延迟时间)
        
        # 记录性能数据
        实际响应时间 = time.time() - 开始时间
        self._记录性能数据(延迟时间, 实际响应时间)
        
        return 延迟时间
    
    def _计算最佳延迟(self) -> float:
        """计算最佳延迟时间"""
        with self._锁:
            if not self._响应时间历史:
                # 无历史数据，使用基础延迟
                return self._基础延迟
            
            # 计算平均响应时间
            平均响应时间 = sum(self._响应时间历史) / len(self._响应时间历史)
            
            # 计算响应时间稳定性
            if len(self._响应时间历史) >= 5:
                标准差 = self._计算标准差(list(self._响应时间历史)[-5:])
            else:
                标准差 = 0.1
            
            # 根据稳定性调整延迟
            if 标准差 < self._稳定性阈值:
                # 系统稳定，减少延迟
                延迟 = max(self._最小延迟, 平均响应时间 * self._调整因子 * 0.8)
            else:
                # 系统不稳定，增加延迟
                延迟 = min(self._最大延迟, 平均响应时间 * self._调整因子 * 1.2)
            
            # 添加趋势预测
            if len(self._响应时间历史) >= 3:
                趋势 = self._分析趋势()
                if 趋势 > 0:
                    # 响应时间增加，适当增加延迟
                    延迟 *= (1 + self._趋势权重 * 趋势)
                else:
                    # 响应时间减少，适当减少延迟
                    延迟 *= (1 + self._趋势权重 * 趋势)
            
            return max(self._最小延迟, min(self._最大延迟, 延迟))
    
    def _根据响应时间计算延迟(self, 预期响应时间: float) -> float:
        """根据预期响应时间计算延迟"""
        with self._锁:
            # 基于预期响应时间的自适应计算
            延迟 = 预期响应时间 * self._调整因子
            
            # 考虑历史性能
            if self._响应时间历史:
                历史平均 = sum(self._响应时间历史) / len(self._响应时间历史)
                延迟 = 0.7 * 延迟 + 0.3 * 历史平均
            
            return max(self._最小延迟, min(self._最大延迟, 延迟))
    
    def _记录性能数据(self, 延迟时间: float, 实际响应时间: float):
        """记录性能数据"""
        with self._锁:
            self._响应时间历史.append(实际响应时间)
            self._延迟调整历史.append(延迟时间)
            self._总调用次数 += 1
            self._总延迟时间 += 延迟时间
            self._最后响应时间 = 实际响应时间
    
    def _计算标准差(self, 数据: list) -> float:
        """计算标准差"""
        if len(数据) < 2:
            return 0.0
        
        均值 = sum(数据) / len(数据)
        方差 = sum((x - 均值) ** 2 for x in 数据) / len(数据)
        return math.sqrt(方差)
    
    def _分析趋势(self) -> float:
        """分析响应时间趋势"""
        if len(self._响应时间历史) < 3:
            return 0.0
        
        # 使用线性回归分析趋势
        recent_data = list(self._响应时间历史)[-3:]
        x = list(range(len(recent_data)))
        y = recent_data
        
        # 简单线性回归
        n = len(x)
        sum_x = sum(x)
        sum_y = sum(y)
        sum_xy = sum(x[i] * y[i] for i in range(n))
        sum_x2 = sum(x_i * x_i for x_i in x)
        
        # 计算斜率
        denominator = n * sum_x2 - sum_x * sum_x
        if denominator == 0:
            return 0.0
        
        斜率 = (n * sum_xy - sum_x * sum_y) / denominator
        
        # 归一化到[-1, 1]范围
        return max(-1.0, min(1.0, 斜率 * 10))
    
    def 设置参数(self, 基础延迟: Optional[float] = None, 最大延迟: Optional[float] = None, 
               最小延迟: Optional[float] = None, 调整因子: Optional[float] = None):
        """设置自适应参数"""
        with self._锁:
            if 基础延迟 is not None:
                self._基础延迟 = 基础延迟
            if 最大延迟 is not None:
                self._最大延迟 = 最大延迟
            if 最小延迟 is not None:
                self._最小延迟 = 最小延迟
            if 调整因子 is not None:
                self._调整因子 = max(0.1, min(1.0, 调整因子))
    
    def 获取统计信息(self) -> Dict[str, Any]:
        """获取延迟统计信息"""
        with self._锁:
            if not self._响应时间历史:
                平均响应时间 = 0.0
                平均延迟 = 0.0
            else:
                平均响应时间 = sum(self._响应时间历史) / len(self._响应时间历史)
                平均延迟 = sum(self._延迟调整历史) / len(self._延迟调整历史)
            
            return {
                "总调用次数": self._总调用次数,
                "总延迟时间": f"{self._总延迟时间:.3f}秒",
                "平均响应时间": f"{平均响应时间:.3f}秒",
                "平均延迟": f"{平均延迟:.3f}秒",
                "最后响应时间": f"{self._最后响应时间:.3f}秒",
                "当前参数": {
                    "基础延迟": f"{self._基础延迟:.3f}秒",
                    "最大延迟": f"{self._最大延迟:.3f}秒",
                    "最小延迟": f"{self._最小延迟:.3f}秒",
                    "调整因子": self._调整因子
                }
            }
    
    def 重置统计(self):
        """重置统计信息"""
        with self._锁:
            self._响应时间历史.clear()
            self._延迟调整历史.clear()
            self._总调用次数 = 0
            self._总延迟时间 = 0.0
            self._最后响应时间 = 0.0


# 全局自适应延迟器实例
全局延迟器 = 自适应延迟器()


def 智能延迟(预期响应时间: Optional[float] = None) -> float:
    """
    全局智能延迟函数
    
    参数:
        预期响应时间: 预期响应时间（秒）
        
    返回:
        实际延迟时间（秒）
    """
    return 全局延迟器.智能延迟(预期响应时间)


def 设置延迟参数(基础延迟: Optional[float] = None, 最大延迟: Optional[float] = None, 
               最小延迟: Optional[float] = None, 调整因子: Optional[float] = None):
    """设置全局延迟参数"""
    全局延迟器.设置参数(基础延迟, 最大延迟, 最小延迟, 调整因子)


def 获取延迟统计() -> Dict[str, Any]:
    """获取全局延迟统计"""
    return 全局延迟器.获取统计信息()


# 使用示例
if __name__ == "__main__":
    # 创建自适应延迟器
    延迟器 = 自适应延迟器(基础延迟=0.1, 最大延迟=0.3, 最小延迟=0.01)
    
    # 测试不同场景下的延迟
    print("测试自适应延迟...")
    
    for i in range(10):
        延迟时间 = 延迟器.智能延迟()
        print(f"第{i+1}次延迟: {延迟时间:.3f}秒")
    
    # 获取统计信息
    统计 = 延迟器.获取统计信息()
    print(f"\n延迟统计: {统计}")