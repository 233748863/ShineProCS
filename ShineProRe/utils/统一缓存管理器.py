"""
统一缓存管理器
统一管理所有缓存实例，提供智能缓存策略和自适应清理
"""
import time
import math
from typing import Dict, Any, List, Optional, Protocol
from enum import Enum
import threading
from dataclasses import dataclass
from collections import defaultdict, deque
from utils.自适应延迟 import 智能延迟


class 缓存类型(Enum):
    """缓存类型枚举"""
    图像缓存 = "image_cache"
    策略缓存 = "strategy_cache"
    性能缓存 = "performance_cache"
    配置缓存 = "config_cache"
    异步缓存 = "async_cache"


@dataclass
class 缓存统计:
    """缓存统计信息"""
    缓存类型: 缓存类型
    缓存大小: int
    命中次数: int
    未命中次数: int
    清理次数: int
    平均访问次数: float


class 缓存接口(Protocol):
    """缓存接口协议"""
    
    def 获取统计信息(self) -> Dict[str, Any]:
        """获取缓存统计信息"""
        ...
    
    def 清除缓存(self):
        """清除缓存"""
        ...


class 统一缓存管理器:
    """统一缓存管理器"""
    
    def __init__(self):
        """初始化统一缓存管理器"""
        self._缓存注册表: Dict[缓存类型, 缓存接口] = {}
        self._缓存策略: Dict[str, Any] = {
            "默认缓存时间": 0.1,  # 100ms
            "最大缓存大小": 500,  # 优化：降低到500条，减少内存压力
            "自动清理间隔": 30.0,  # 优化：缩短到30秒，更频繁清理
            "启用同步清理": True,
            "启用智能清理": True,
            "内存压力阈值": 0.7,  # 优化：降低到70%，提前预警
            "命中率目标": 0.85,
            "自适应调整": True,
            "LRU淘汰策略": True,   # 新增：启用LRU淘汰
            "最小缓存大小": 50     # 新增：最小缓存保证
        }
        self._锁 = threading.RLock()
        
        # 全局统计
        self._总命中次数 = 0
        self._总未命中次数 = 0
        self._最后同步时间 = time.time()
        
        # 新增：智能缓存统计
        self._命中率历史: deque = deque(maxlen=100)  # 最近100次命中率
        self._内存使用历史: deque = deque(maxlen=50)  # 最近50次内存使用
        self._清理效率统计: Dict[str, Any] = {
            "最近清理数量": 0,
            "清理节省内存": 0,
            "平均清理耗时": 0.0
        }
        
        # 新增：内存协同优化配置
        self._内存监控器 = None  # 延迟初始化
        self._协同优化启用 = True
        self._最后协同优化时间 = 0.0
        self._协同优化间隔 = 60.0  # 协同优化间隔（秒）
        
        # 启动智能清理线程
        if self._缓存策略["启用智能清理"]:
            self._智能清理线程 = threading.Thread(target=self._智能清理循环, daemon=True)
            self._智能清理线程.start()
    
    def 注册缓存(self, 缓存实例: 缓存接口, 缓存类型: 缓存类型):
        """
        注册缓存实例
        
        参数:
            缓存实例: 缓存实例
            缓存类型: 缓存类型
        """
        with self._锁:
            self._缓存注册表[缓存类型] = 缓存实例
    
    def 同步缓存策略(self):
        """同步所有缓存的缓存策略"""
        with self._锁:
            # 检查是否需要同步
            当前时间 = time.time()
            if 当前时间 - self._最后同步时间 < self._缓存策略["自动清理间隔"]:
                return
            
            # 执行同步操作
            for 缓存类型, 缓存实例 in self._缓存注册表.items():
                # 可以在这里添加特定类型的缓存策略同步
                if self._缓存策略["启用同步清理"]:
                    # 触发缓存清理（如果缓存实例支持）
                    if hasattr(缓存实例, '_清理过期缓存'):
                        缓存实例._清理过期缓存()
            
            self._最后同步时间 = 当前时间
    
    def 设置缓存策略(self, 策略名称: str, 策略值: Any):
        """
        设置缓存策略
        
        参数:
            策略名称: 策略名称
            策略值: 策略值
        """
        with self._锁:
            if 策略名称 in self._缓存策略:
                self._缓存策略[策略名称] = 策略值
    
    def 获取缓存统计(self, 缓存类型: Optional[缓存类型] = None) -> Dict[str, Any]:
        """
        获取缓存统计信息
        
        参数:
            缓存类型: 指定缓存类型，None表示所有缓存
            
        返回:
            统计信息字典
        """
        with self._锁:
            if 缓存类型:
                if 缓存类型 in self._缓存注册表:
                    缓存统计 = self._缓存注册表[缓存类型].获取统计信息()
                    缓存统计["缓存类型"] = 缓存类型.value
                    return 缓存统计
                else:
                    return {"错误": f"缓存类型 {缓存类型} 未注册"}
            else:
                # 返回所有缓存的统计
                所有统计 = {}
                for 缓存类型, 缓存实例 in self._缓存注册表.items():
                    缓存统计 = 缓存实例.获取统计信息()
                    所有统计[缓存类型.value] = 缓存统计
                
                # 计算总体统计
                总命中率 = self._总命中次数 / max(self._总命中次数 + self._总未命中次数, 1)
                所有统计["总体统计"] = {
                    "注册缓存数量": len(self._缓存注册表),
                    "总命中次数": self._总命中次数,
                    "总未命中次数": self._总未命中次数,
                    "总命中率": f"{总命中率:.2%}",
                    "缓存策略": self._缓存策略
                }
                
                return 所有统计
    
    def 清除所有缓存(self):
        """清除所有缓存"""
        with self._锁:
            for 缓存实例 in self._缓存注册表.values():
                缓存实例.清除缓存()
    
    def 更新命中统计(self, 命中: bool):
        """
        更新命中统计
        
        参数:
            命中: 是否命中
        """
        with self._锁:
            if 命中:
                self._总命中次数 += 1
            else:
                self._总未命中次数 += 1
    
    def 生成缓存报告(self) -> Dict[str, Any]:
        """
        生成完整的缓存报告
        
        返回:
            缓存报告字典
        """
        with self._锁:
            统计信息 = self.获取缓存统计()
            
            报告 = {
                "缓存概况": {
                    "注册缓存类型": [缓存类型.value for 缓存类型 in self._缓存注册表.keys()],
                    "缓存策略": self._缓存策略,
                    "最后同步时间": self._最后同步时间
                },
                "详细统计": 统计信息,
                "性能建议": self._生成性能建议()
            }
            
            return 报告
    
    def _生成性能建议(self) -> List[str]:
        """生成性能优化建议"""
        建议 = []
        
        with self._锁:
            总命中率 = self._总命中次数 / max(self._总命中次数 + self._总未命中次数, 1)
            
            if 总命中率 < 0.7:
                建议.append("缓存命中率较低，建议调整缓存策略")
            
            if len(self._缓存注册表) > 5:
                建议.append("注册缓存类型较多，建议合并相似缓存")
            
            if self._缓存策略["默认缓存时间"] > 0.5:
                建议.append("默认缓存时间较长，可能导致数据延迟")
            
            if self._缓存策略["最大缓存大小"] < 100:
                建议.append("最大缓存大小较小，可能导致频繁清理")
        
        return 建议
    
    def _智能清理循环(self):
        """智能清理循环"""
        while True:
            try:
                # 使用智能延迟替代硬编码sleep
                智能延迟(60)  # 每60秒检查一次，但根据系统性能自动调整
                self._智能清理检查()
            except Exception as e:
                print(f"智能清理循环错误: {e}")
    
    def _智能清理检查(self):
        """执行智能清理检查"""
        with self._锁:
            # 计算内存压力
            内存压力 = self._计算内存压力()
            
            # 如果内存压力超过阈值，执行清理
            if 内存压力 > self._缓存策略["内存压力阈值"]:
                self._执行智能清理(内存压力)
            
            # 检查命中率，如果过低则调整策略
            if self._命中率历史:
                平均命中率 = sum(self._命中率历史) / len(self._命中率历史)
                if 平均命中率 < self._缓存策略["命中率目标"]:
                    self._自适应调整策略()
            
            # 执行内存协同优化
            self._执行内存协同优化()
    
    def _计算内存压力(self) -> float:
        """计算当前内存压力（0-1之间）"""
        try:
            import psutil
            内存使用率 = psutil.virtual_memory().percent / 100.0
            self._内存使用历史.append(内存使用率)
            return 内存使用率
        except ImportError:
            # 如果没有psutil，使用简化的内存压力估算
            if len(self._缓存注册表) > 10:
                return 0.8  # 假设有内存压力
            return 0.3  # 假设内存压力较低
    
    def _执行智能清理(self, 内存压力: float):
        """执行智能清理"""
        import time
        开始时间 = time.time()
        
        # 清理策略：根据内存压力决定清理强度
        清理强度 = min(1.0, 内存压力 / self._缓存策略["内存压力阈值"])
        清理数量 = 0
        
        # 对每个注册的缓存执行清理
        for 缓存类型, 缓存实例 in self._缓存注册表.items():
            if hasattr(缓存实例, '_清理过期缓存'):
                清理数量 += 缓存实例._清理过期缓存(清理强度)
        
        # 记录清理效率
        清理耗时 = time.time() - 开始时间
        self.清理效率统计["最近清理数量"] = 清理数量
        self.清理效率统计["清理节省内存"] = int(清理数量 * 100)  # 假设每个缓存项节省100字节
        self.清理效率统计["平均清理耗时"] = (
            self.清理效率统计["平均清理耗时"] * 0.9 + 清理耗时 * 0.1
        )
    
    def _自适应调整策略(self):
        """自适应调整缓存策略"""
        if not self._缓存策略["自适应调整"]:
            return
        
        # 根据命中率历史调整策略
        if self._命中率历史:
            平均命中率 = sum(self._命中率历史) / len(self._命中率历史)
            
            if 平均命中率 < 0.7:
                # 命中率过低，增加缓存时间
                self._缓存策略["默认缓存时间"] = min(
                    self._缓存策略["默认缓存时间"] * 1.1, 1.0
                )
            elif 平均命中率 > 0.9:
                # 命中率过高，减少缓存时间以提高新鲜度
                self._缓存策略["默认缓存时间"] = max(
                    self._缓存策略["默认缓存时间"] * 0.9, 0.05
                )
    
    def _执行内存协同优化(self):
        """执行内存与缓存协同优化"""
        import time
        当前时间 = time.time()
        
        # 检查协同优化间隔
        if 当前时间 - self._最后协同优化时间 < self._协同优化间隔:
            return
        
        if not self._协同优化启用:
            return
        
        # 延迟初始化内存监控器
        if self._内存监控器 is None:
            try:
                from .内存管理 import 全局内存监控器
                self._内存监控器 = 全局内存监控器
            except ImportError:
                self._协同优化启用 = False
                return
        
        # 获取内存压力信息
        try:
            内存统计 = self._内存监控器.获取内存统计()
            内存使用率 = float(内存统计["当前内存使用率"].strip("%")) / 100.0
            
            # 根据内存压力调整缓存策略
            self._根据内存压力调整缓存(内存使用率)
            
            self._最后协同优化时间 = 当前时间
            
        except Exception as e:
            print(f"内存协同优化错误: {e}")
    
    def _根据内存压力调整缓存(self, 内存使用率: float):
        """根据内存压力调整缓存策略"""
        with self._锁:
            if 内存使用率 > 0.8:
                # 内存紧张：减少缓存，增加清理频率
                self._缓存策略["最大缓存大小"] = max(100, self._缓存策略["最大缓存大小"] * 0.7)
                self._缓存策略["默认缓存时间"] = max(0.02, self._缓存策略["默认缓存时间"] * 0.8)
                self._缓存策略["自动清理间隔"] = max(10.0, self._缓存策略["自动清理间隔"] * 0.6)
                print(f"内存紧张({内存使用率:.1%})：调整为保守缓存策略")
                
            elif 内存使用率 < 0.4:
                # 内存充足：增加缓存，提高命中率
                self._缓存策略["最大缓存大小"] = min(1000, self._缓存策略["最大缓存大小"] * 1.3)
                self._缓存策略["默认缓存时间"] = min(0.5, self._缓存策略["默认缓存时间"] * 1.2)
                self._缓存策略["自动清理间隔"] = min(120.0, self._缓存策略["自动清理间隔"] * 1.4)
                print(f"内存充足({内存使用率:.1%})：调整为激进缓存策略")
                
            else:
                # 内存适中：保持平衡策略
                print(f"内存适中({内存使用率:.1%})：保持平衡缓存策略")


# 全局统一缓存管理器实例
全局缓存管理器 = 统一缓存管理器()


def 注册全局缓存(缓存实例: 缓存接口, 缓存类型: 缓存类型):
    """
    注册全局缓存
    
    参数:
        缓存实例: 缓存实例
        缓存类型: 缓存类型
    """
    全局缓存管理器.注册缓存(缓存实例, 缓存类型)


def 获取全局缓存统计(缓存类型: Optional[缓存类型] = None) -> Dict[str, Any]:
    """
    获取全局缓存统计
    
    参数:
        缓存类型: 指定缓存类型，None表示所有缓存
        
    返回:
        统计信息字典
    """
    return 全局缓存管理器.获取缓存统计(缓存类型)


def 同步全局缓存策略():
    """同步全局缓存策略"""
    全局缓存管理器.同步缓存策略()


def 生成全局缓存报告() -> Dict[str, Any]:
    """生成全局缓存报告"""
    return 全局缓存管理器.生成缓存报告()