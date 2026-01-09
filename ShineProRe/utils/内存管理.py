"""
内存管理优化模块
提供智能内存监控、引用清理和内存泄漏检测功能
支持智能GC调度、精确内存估算和缓存-内存协同优化
"""
import gc
import time
import threading
from typing import Dict, Any, List, Optional
from collections import defaultdict, deque
import weakref
import math
from utils.自适应延迟 import 智能延迟


class 内存监控器:
    """内存使用监控器"""
    
    def __init__(self, 监控间隔: float = 5.0, 内存阈值: float = 0.8):
        """
        初始化内存监控器
        
        参数:
            监控间隔: 监控间隔（秒）
            内存阈值: 内存使用率阈值（0-1之间）
        """
        self._监控间隔 = 监控间隔
        self._内存阈值 = 内存阈值
        self._锁 = threading.RLock()
        
        # 内存使用历史
        self._内存使用历史: deque = deque(maxlen=100)
        self._内存峰值 = 0.0
        
        # 对象引用跟踪
        self._对象引用跟踪: Dict[str, List[weakref.ref]] = {}
        self._最后清理时间 = time.time()
        
        # 统计信息
        self._内存告警次数 = 0
        self._清理对象数量 = 0
        self._总监控时间 = 0.0
        self._最后GC时间 = 0.0
        self._GC调用次数 = 0
        self._内存使用历史精确 = deque(maxlen=50)
        
        # 智能GC调度配置
        self._GC最小间隔 = 30.0  # 最小GC间隔（秒）
        self._GC最大间隔 = 300.0  # 最大GC间隔（秒）
        self._内存压力阈值 = 0.7  # 触发GC的内存压力阈值
        
        # 安全控制机制
        self._清理时间窗口 = 0.1  # 清理操作最大耗时限制（秒）
        self._最后清理耗时 = 0.0
        self._清理优先级 = "低"  # 可选：低、中、高
        self._启用安全模式 = True  # 启用安全模式，避免影响主循环
        
        # 启动监控线程
        self._监控线程 = threading.Thread(target=self._监控循环, daemon=True)
        self._监控线程.start()
    
    def _监控循环(self):
        """内存监控循环"""
        while True:
            try:
                智能延迟(self._监控间隔)  # 使用智能延迟替代硬编码sleep
                self._检查内存使用()
                self._清理无效引用()
            except Exception as e:
                print(f"内存监控错误: {e}")
    
    def _检查内存使用(self):
        """检查内存使用情况"""
        try:
            import psutil
            
            # 获取精确内存使用率
            内存使用率 = self._精确内存估算()
            
            with self._锁:
                # 记录精确内存使用历史
                self._内存使用历史.append(内存使用率)
                self._内存使用历史精确.append(内存使用率)
                
                # 更新内存峰值
                if 内存使用率 > self._内存峰值:
                    self._内存峰值 = 内存使用率
                
                # 智能GC调度
                self._智能垃圾回收(内存使用率 > self._内存压力阈值)
                
                # 检查是否超过阈值
                if 内存使用率 > self._内存阈值:
                    self._内存告警次数 += 1
                    self._执行内存优化()
                
                self._总监控时间 += self._监控间隔
                
        except ImportError:
            # 如果没有psutil，使用改进的估算版本
            with self._锁:
                # 使用改进的估算算法
                内存使用率 = self._精确内存估算()
                
                # 记录内存使用历史
                self._内存使用历史.append(内存使用率)
                self._内存使用历史精确.append(内存使用率)
                
                # 智能GC调度
                self._智能垃圾回收(内存使用率 > self._内存压力阈值)
                
                if 内存使用率 > self._内存阈值:
                    self._内存告警次数 += 1
                    self._执行内存优化()
    
    def _执行内存优化(self):
        """执行内存优化措施（安全版本）"""
        if not self._启用安全模式:
            self._执行完整内存优化()
            return
        
        # 安全模式：分阶段执行，控制执行时间
        print(f"内存使用超过阈值，执行安全优化措施...")
        
        # 阶段1：快速清理（耗时 < 0.05秒）
        开始时间 = time.time()
        self._执行快速清理()
        阶段1耗时 = time.time() - 开始时间
        
        # 阶段2：如果时间允许，执行中等清理
        if 阶段1耗时 < self._清理时间窗口 * 0.5:
            self._执行中等清理()
        阶段2耗时 = time.time() - 开始时间 - 阶段1耗时
        
        # 阶段3：仅在优先级高且时间充足时执行完整清理
        if (self._清理优先级 == "高" and 
            阶段2耗时 < self._清理时间窗口 * 0.3):
            self._执行完整清理()
        
        self._最后清理耗时 = time.time() - 开始时间
        print(f"安全优化完成，总耗时: {self._最后清理耗时:.3f}秒")
    
    def _执行快速清理(self):
        """执行快速清理（不影响主循环）"""
        try:
            # 仅清理最明显的无效引用
            with self._锁:
                清理数量 = 0
                for 对象标识, 引用列表 in list(self._对象引用跟踪.items()):
                    # 快速过滤：只清理明显无效的引用
                    有效引用 = [引用 for 引用 in 引用列表 if 引用() is not None]
                    
                    if not 有效引用:
                        del self._对象引用跟踪[对象标识]
                        清理数量 += len(引用列表)
                    elif len(有效引用) < len(引用列表) * 0.5:
                        # 超过一半无效时才清理
                        self._对象引用跟踪[对象标识] = 有效引用
                        清理数量 += len(引用列表) - len(有效引用)
                
                if 清理数量 > 0:
                    self._清理对象数量 += 清理数量
                    print(f"快速清理: {清理数量} 个无效引用")
        except Exception as e:
            print(f"快速清理异常: {e}")
    
    def _执行中等清理(self):
        """执行中等清理（适度影响）"""
        try:
            # 执行轻量级垃圾回收
            gc.collect(0)  # 仅清理0代垃圾
            
            # 更彻底的无效引用清理
            清理数量 = self._清理所有无效引用()
            if 清理数量 > 0:
                print(f"中等清理: {清理数量} 个无效引用")
        except Exception as e:
            print(f"中等清理异常: {e}")
    
    def _执行完整清理(self):
        """执行完整清理（可能影响性能）"""
        try:
            # 完整的垃圾回收
            gc.collect()
            
            # 清理所有无效引用
            清理数量 = self._清理所有无效引用()
            
            # 清理循环引用
            self._清理循环引用()
            
            print(f"完整清理完成: {清理数量} 个无效引用")
        except Exception as e:
            print(f"完整清理异常: {e}")
    
    def _智能垃圾回收(self, 强制回收: bool = False):
        """智能垃圾回收调度，避免频繁GC影响性能"""
        当前时间 = time.time()
        
        # 计算自上次GC的时间间隔
        时间间隔 = 当前时间 - self._最后GC时间
        
        # 限制GC频率：至少间隔30秒，最大300秒
        if not 强制回收 and 时间间隔 < self._GC最小间隔:
            return
        
        # 根据内存压力选择GC策略
        if 强制回收 or 时间间隔 > self._GC最大间隔:
            # 内存压力大或超时：完整回收
            gc.collect()
        else:
            # 内存压力适中：轻量级回收
            gc.collect(0)  # 仅回收0代
        
        self._最后GC时间 = 当前时间
        self._GC调用次数 += 1
        
        if self._GC调用次数 % 10 == 0:  # 每10次GC打印统计
            print(f"智能GC调度：已调用{self._GC调用次数}次，间隔{时间间隔:.1f}秒")
    
    def _精确内存估算(self) -> float:
        """更精确的内存使用估算算法"""
        try:
            import psutil
            # 使用psutil获取精确内存使用率
            return psutil.virtual_memory().percent / 100.0
        except ImportError:
            # 改进的估算算法：基于GC统计和对象大小估算
            gc.collect(0)  # 轻量级回收
            对象数量 = len(gc.get_objects())
            
            # 更精确的对象大小估算（平均对象大小256字节）
            对象大小估算 = 对象数量 * 256  # 平均对象大小256字节
            总内存估算 = 对象大小估算 / (1024 * 1024)  # 转换为MB
            
            # 假设系统内存为4GB，可动态调整
            系统内存MB = 4000
            内存使用率 = min(总内存估算 / 系统内存MB, 0.95)  # 限制最大95%
            
            # 添加趋势平滑
            if self._内存使用历史精确:
                历史平均 = sum(self._内存使用历史精确) / len(self._内存使用历史精确)
                内存使用率 = 0.7 * 内存使用率 + 0.3 * 历史平均  # 平滑处理
            
            return max(0.1, min(0.95, 内存使用率))  # 安全范围限制
    
    def _执行完整内存优化(self):
        """执行完整内存优化（非安全模式）"""
        print(f"内存使用超过阈值，执行完整优化措施...")
        
        # 强制垃圾回收
        gc.collect()
        
        # 清理所有缓存的无效引用
        self._清理所有无效引用()
        
        # 清理循环引用
        self._清理循环引用()
        
        print("内存优化完成")
    
    def 跟踪对象引用(self, 对象标识: str, 对象):
        """跟踪对象引用，用于检测内存泄漏"""
        with self._锁:
            if 对象标识 not in self._对象引用跟踪:
                self._对象引用跟踪[对象标识] = []
            
            try:
                # 使用弱引用跟踪对象（仅对支持弱引用的对象）
                弱引用 = weakref.ref(对象)
                self._对象引用跟踪[对象标识].append(弱引用)
            except TypeError:
                # 对于不支持弱引用的对象，使用其他跟踪方式
                # 这里我们使用简单的引用计数，避免弱引用错误
                self._对象引用跟踪[对象标识].append(lambda: 对象)  # 使用lambda包装对象
    
    def _清理无效引用(self):
        """清理无效的对象引用"""
        当前时间 = time.time()
        
        # 每30秒清理一次
        if 当前时间 - self._最后清理时间 < 30:
            return
        
        清理数量 = 0
        with self._锁:
            for 对象标识, 引用列表 in list(self._对象引用跟踪.items()):
                # 过滤掉无效引用
                有效引用 = [引用 for 引用 in 引用列表 if 引用() is not None]
                
                if not 有效引用:
                    # 删除没有有效引用的跟踪项
                    del self._对象引用跟踪[对象标识]
                    清理数量 += len(引用列表)
                else:
                    self._对象引用跟踪[对象标识] = 有效引用
                    
        self._清理对象数量 += 清理数量
        self._最后清理时间 = 当前时间
        
        if 清理数量 > 0:
            print(f"清理无效引用: {清理数量} 个对象")
    
    def _清理所有无效引用(self) -> int:
        """强制清理所有无效引用"""
        with self._锁:
            清理数量 = 0
            for 对象标识, 引用列表 in list(self._对象引用跟踪.items()):
                有效引用 = [引用 for 引用 in 引用列表 if 引用() is not None]
                
                if not 有效引用:
                    del self._对象引用跟踪[对象标识]
                    清理数量 += len(引用列表)
                else:
                    self._对象引用跟踪[对象标识] = 有效引用
            
            self._清理对象数量 += 清理数量
            return 清理数量
    
    def _清理循环引用(self):
        """清理循环引用"""
        # 收集并显示循环引用信息
        try:
            垃圾 = gc.garbage
            if 垃圾:
                print(f"检测到 {len(垃圾)} 个无法回收的对象")
                # 尝试清理
                gc.collect()
        except Exception as e:
            print(f"清理循环引用错误: {e}")
    
    def 获取内存统计(self) -> Dict[str, Any]:
        """获取内存统计信息"""
        with self._锁:
            if self._内存使用历史:
                平均使用率 = sum(self._内存使用历史) / len(self._内存使用历史)
                当前使用率 = self._内存使用历史[-1] if self._内存使用历史 else 0.0
            else:
                平均使用率 = 0.0
                当前使用率 = 0.0
            
            return {
                "当前内存使用率": f"{当前使用率:.2%}",
                "平均内存使用率": f"{平均使用率:.2%}",
                "内存峰值": f"{self._内存峰值:.2%}",
                "内存告警次数": self._内存告警次数,
                "清理对象数量": self._清理对象数量,
                "跟踪对象数量": sum(len(引用列表) for 引用列表 in self._对象引用跟踪.values()),
                "总监控时间": f"{self._总监控时间:.1f}秒",
                "监控间隔": f"{self._监控间隔}秒"
            }
    
    def 设置监控配置(self, 监控间隔: float = None, 内存阈值: float = None):
        """设置监控配置"""
        with self._锁:
            if 监控间隔 is not None:
                self._监控间隔 = 监控间隔
            if 内存阈值 is not None:
                self._内存阈值 = 内存阈值
    
    def 设置安全配置(self, 清理时间窗口: float = None, 清理优先级: str = None, 启用安全模式: bool = None):
        """设置安全控制配置"""
        with self._锁:
            if 清理时间窗口 is not None:
                self._清理时间窗口 = 清理时间窗口
            if 清理优先级 is not None and 清理优先级 in ["低", "中", "高"]:
                self._清理优先级 = 清理优先级
            if 启用安全模式 is not None:
                self._启用安全模式 = 启用安全模式
    
    def 强制内存优化(self):
        """强制执行内存优化"""
        if self._启用安全模式:
            self._执行内存优化()
        else:
            self._执行完整内存优化()
    
    def 获取安全配置(self) -> Dict[str, Any]:
        """获取当前安全配置"""
        with self._锁:
            return {
                "清理时间窗口": f"{self._清理时间窗口}秒",
                "清理优先级": self._清理优先级,
                "启用安全模式": self._启用安全模式,
                "最后清理耗时": f"{self._最后清理耗时:.3f}秒"
            }


class 引用计数管理器:
    """引用计数管理器，帮助检测内存泄漏"""
    
    def __init__(self):
        self._对象引用计数 = defaultdict(int)
        self._锁 = threading.RLock()
    
    def 增加引用(self, 对象标识: str):
        """增加对象引用计数"""
        with self._锁:
            self._对象引用计数[对象标识] += 1
    
    def 减少引用(self, 对象标识: str):
        """减少对象引用计数"""
        with self._lock:
            if 对象标识 in self._对象引用计数:
                self._对象引用计数[对象标识] -= 1
                
                # 如果引用计数为0，删除记录
                if self._对象引用计数[对象标识] <= 0:
                    del self._对象引用计数[对象标识]
    
    def 获取引用统计(self) -> Dict[str, int]:
        """获取引用统计信息"""
        with self._锁:
            return dict(self._对象引用计数)
    
    def 检测潜在泄漏(self) -> List[str]:
        """检测潜在的内存泄漏"""
        with self._锁:
            潜在泄漏 = []
            for 对象标识, 引用计数 in self._对象引用计数.items():
                if 引用计数 > 10:  # 引用计数过高可能表示泄漏
                    潜在泄漏.append(f"{对象标识}: {引用计数}次引用")
            return 潜在泄漏


# 全局内存监控器实例
全局内存监控器 = 内存监控器()


# 内存管理工具函数
def 启用内存监控(监控间隔: float = 5.0, 内存阈值: float = 0.8):
    """启用全局内存监控"""
    全局内存监控器.设置监控配置(监控间隔, 内存阈值)


def 设置内存安全配置(清理时间窗口: float = 0.1, 清理优先级: str = "低", 启用安全模式: bool = True):
    """设置内存安全配置"""
    全局内存监控器.设置安全配置(清理时间窗口, 清理优先级, 启用安全模式)


def 获取内存统计() -> Dict[str, Any]:
    """获取内存统计信息"""
    return 全局内存监控器.获取内存统计()


def 获取安全配置() -> Dict[str, Any]:
    """获取当前安全配置"""
    return 全局内存监控器.获取安全配置()


def 跟踪对象(对象标识: str, 对象):
    """跟踪对象引用"""
    全局内存监控器.跟踪对象引用(对象标识, 对象)


def 强制内存清理():
    """强制执行内存清理"""
    全局内存监控器.强制内存优化()


# 使用示例
if __name__ == "__main__":
    # 创建内存监控器
    内存监控器 = 内存监控器()
    
    # 测试内存监控
    print("内存监控器已启动")
    
    # 模拟一些对象创建
    测试对象 = []
    for i in range(1000):
        测试对象.append([i] * 100)
    
    # 获取内存统计
    统计 = 内存监控器.获取内存统计()
    print(f"内存统计: {统计}")
    
    # 强制内存优化
    内存监控器.强制内存优化()
    
    # 再次获取统计
    统计 = 内存监控器.获取内存统计()
    print(f"优化后内存统计: {统计}")