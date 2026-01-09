"""
异步处理模块
提供异步图像处理和技能检测功能
"""
import asyncio
import time
from typing import Dict, Any, Callable, Optional, List
from concurrent.futures import ThreadPoolExecutor
import threading
from dataclasses import dataclass
from abc import ABC, abstractmethod
from collections import deque
from utils.自适应延迟 import 智能延迟


@dataclass
class 异步任务结果:
    """异步任务结果"""
    成功: bool
    数据: Any
    错误信息: Optional[str] = None
    执行时间: float = 0.0


class 异步任务管理器:
    """异步任务管理器"""
    
    def __init__(self, 最大线程数: int = None, 自适应线程池: bool = True):
        """
        初始化异步任务管理器
        
        参数:
            最大线程数: 线程池最大线程数，None则根据CPU核心数自动设置
            自适应线程池: 是否启用自适应线程池调整
        """
        import os
        
        # 自动设置线程数（CPU核心数 * 2，但不超过16，优化性能）
        if 最大线程数 is None:
            cpu_cores = os.cpu_count() or 1
            最大线程数 = min(cpu_cores * 2, 16)  # 优化：提高上限到16
        
        self._最大线程数 = 最大线程数
        self._自适应线程池 = 自适应线程池
        self._线程池 = ThreadPoolExecutor(max_workers=最大线程数)
        self._事件循环 = asyncio.new_event_loop()
        self._任务队列: List[asyncio.Task] = []
        self._锁 = threading.RLock()
        
        # 统计信息
        self._已完成任务数 = 0
        self._失败任务数 = 0
        self._总执行时间 = 0.0
        self._峰值任务数 = 0
        self._最后调整时间 = time.time()
        
        # 性能监控
        self._最近任务执行时间: deque = deque(maxlen=100)
        self._任务排队时间: deque = deque(maxlen=100)
        
        # 启动自适应调整线程（如果需要）
        if 自适应线程池:
            self._自适应线程 = threading.Thread(target=self._自适应调整循环, daemon=True)
            self._自适应线程.start()
    
    async def 执行异步任务(self, 任务函数: Callable, *参数, **关键字参数) -> 异步任务结果:
        """
        异步执行任务
        
        参数:
            任务函数: 要执行的函数
            *参数: 函数参数
            **关键字参数: 函数关键字参数
            
        返回:
            异步任务结果
        """
        开始时间 = time.time()
        结果 = 异步任务结果(成功=False, 数据=None)
        
        try:
            # 在线程池中执行阻塞操作
            数据 = await self._事件循环.run_in_executor(
                self._线程池, 任务函数, *参数, **关键字参数
            )
            
            结果.成功 = True
            结果.数据 = 数据
            
        except Exception as e:
            结果.错误信息 = str(e)
            self._失败任务数 += 1
        
        finally:
            执行时间 = time.time() - 开始时间
            结果.执行时间 = 执行时间
            
            with self._锁:
                self._已完成任务数 += 1
                self._总执行时间 += 执行时间
        
        return 结果
    
    def _自适应调整循环(self):
        """自适应调整线程池的循环"""
        while True:
            try:
                # 使用智能延迟替代硬编码sleep
                智能延迟(30)  # 每30秒检查一次，但根据系统性能自动调整
                self._检查并调整线程池()
            except Exception as e:
                print(f"自适应调整错误: {e}")
    
    def _检查并调整线程池(self):
        """检查性能指标并调整线程池"""
        with self._锁:
            当前时间 = time.time()
            
            # 避免频繁调整
            if 当前时间 - self._最后调整时间 < 30:
                return
            
            # 计算性能指标
            平均执行时间 = sum(self._最近任务执行时间) / len(self._最近任务执行时间) if self._最近任务执行时间 else 0
            平均排队时间 = sum(self._任务排队时间) / len(self._任务排队时间) if self._任务排队时间 else 0
            活跃任务数 = len(self._任务队列)
            
            # 优化调整策略：更灵敏的动态调整
            if 平均排队时间 > 0.05 and 平均执行时间 < 0.03:  # 优化：降低阈值，更敏感
                # 排队时间长但执行快，增加线程数
                if self._最大线程数 < 16 and 活跃任务数 > self._最大线程数 * 0.8:
                    self._调整线程池(self._最大线程数 + 1)
            elif 平均排队时间 < 0.005 and 活跃任务数 < self._最大线程数 * 0.3:  # 优化：更严格的条件
                # 排队时间短且任务少，减少线程数
                if self._最大线程数 > 2:
                    self._调整线程池(self._最大线程数 - 1)
            
            self._最后调整时间 = 当前时间
    
    def _调整线程池(self, 新线程数: int):
        """调整线程池大小"""
        if 新线程数 == self._最大线程数:
            return
        
        print(f"调整线程池大小: {self._最大线程数} -> {新线程数}")
        
        # 创建新的线程池
        新线程池 = ThreadPoolExecutor(max_workers=新线程数)
        旧线程池 = self._线程池
        
        # 切换线程池
        self._线程池 = 新线程池
        self._最大线程数 = 新线程数
        
        # 优雅关闭旧线程池
        def 关闭旧线程池():
            try:
                旧线程池.shutdown(wait=True)
            except Exception as e:
                print(f"关闭旧线程池错误: {e}")
        
        threading.Thread(target=关闭旧线程池, daemon=True).start()
    
    def 批量执行任务(self, 任务列表: List[Callable], 参数列表: List[tuple] = None) -> List[异步任务结果]:
        """
        批量执行异步任务
        
        参数:
            任务列表: 任务函数列表
            参数列表: 参数列表，None表示无参数
            
        返回:
            任务结果列表
        """
        if 参数列表 is None:
            参数列表 = [()] * len(任务列表)
        
        async def 执行所有任务():
            任务协程 = []
            
            for 任务, 参数 in zip(任务列表, 参数列表):
                if isinstance(参数, tuple):
                    协程 = self.执行异步任务(任务, *参数)
                else:
                    协程 = self.执行异步任务(任务, 参数)
                
                任务协程.append(协程)
            
            return await asyncio.gather(*任务协程)
        
        # 在当前事件循环中执行
        return self._事件循环.run_until_complete(执行所有任务())
    
    def 获取统计信息(self) -> Dict[str, Any]:
        """获取任务统计信息"""
        with self._锁:
            平均执行时间 = self._总执行时间 / max(self._已完成任务数, 1)
            成功率 = (self._已完成任务数 - self._失败任务数) / max(self._已完成任务数, 1)
            
            return {
                "已完成任务数": self._已完成任务数,
                "失败任务数": self._失败任务数,
                "成功率": f"{成功率:.2%}",
                "总执行时间": self._总执行时间,
                "平均执行时间": f"{平均执行时间:.3f}秒",
                "活跃任务数": len(self._任务队列)
            }
    
    def 关闭(self):
        """关闭任务管理器"""
        self._线程池.shutdown(wait=True)
        self._事件循环.close()


class 异步图像处理接口(ABC):
    """异步图像处理接口"""
    
    @abstractmethod
    async def 异步获取屏幕图像(self, 区域: tuple) -> Any:
        """异步获取屏幕图像"""
        pass
    
    @abstractmethod
    async def 异步技能检测(self, 图像: Any, 技能配置: Dict[str, Any]) -> int:
        """异步技能检测"""
        pass


class 异步技能检测器:
    """异步技能检测器"""
    
    def __init__(self, 图像处理接口: 异步图像处理接口, 任务管理器: 异步任务管理器 = None):
        """
        初始化异步技能检测器
        
        参数:
            图像处理接口: 异步图像处理接口
            任务管理器: 异步任务管理器，None则自动创建
        """
        self._图像处理接口 = 图像处理接口
        self._任务管理器 = 任务管理器 or 异步任务管理器()
        
        # 缓存最近检测结果
        self._检测结果缓存: Dict[str, Any] = {}
        self._缓存有效期 = 0.05  # 50毫秒
    
    async def 异步检测技能状态(self, 技能配置: Dict[str, Any], 检测区域: tuple) -> int:
        """
        异步检测技能状态
        
        参数:
            技能配置: 技能配置字典
            检测区域: 检测区域
            
        返回:
            技能键值
        """
        技能名称 = 技能配置.get("技能名称", "unknown")
        缓存键 = f"{技能名称}_{检测区域}"
        
        # 检查缓存
        当前时间 = time.time()
        缓存条目 = self._检测结果缓存.get(缓存键)
        
        if 缓存条目 and (当前时间 - 缓存条目.get("时间", 0)) < self._缓存有效期:
            return 缓存条目.get("键值", 0)
        
        # 异步获取图像
        图像结果 = await self._任务管理器.执行异步任务(
            self._图像处理接口.异步获取屏幕图像, 检测区域
        )
        
        if not 图像结果.成功:
            # 图像获取失败
            self._检测结果缓存[缓存键] = {
                "时间": 当前时间,
                "键值": 0
            }
            return 0
        
        # 异步进行技能检测
        检测结果 = await self._任务管理器.执行异步任务(
            self._图像处理接口.异步技能检测, 图像结果.数据, 技能配置
        )
        
        if 检测结果.成功:
            键值 = 检测结果.数据
        else:
            键值 = 0
        
        # 更新缓存
        self._检测结果缓存[缓存键] = {
            "时间": 当前时间,
            "键值": 键值
        }
        
        return 键值
    
    async def 异步批量检测技能(self, 技能配置列表: List[Dict[str, Any]], 检测区域: tuple) -> Dict[str, int]:
        """
        异步批量检测技能状态
        
        参数:
            技能配置列表: 技能配置字典列表
            检测区域: 检测区域
            
        返回:
            {技能名称: 键值} 可释放的技能
        """
        # 创建任务列表
        任务列表 = []
        参数列表 = []
        
        for 技能配置 in 技能配置列表:
            任务列表.append(self.异步检测技能状态)
            参数列表.append((技能配置, 检测区域))
        
        # 批量执行任务
        结果列表 = self._任务管理器.批量执行任务(任务列表, 参数列表)
        
        # 处理结果
        可用技能 = {}
        for 技能配置, 结果 in zip(技能配置列表, 结果列表):
            if 结果.成功 and 结果.数据 > 0:
                技能名称 = 技能配置.get("技能名称")
                可用技能[技能名称] = 结果.数据
        
        return 可用技能
    
    def 获取统计信息(self) -> Dict[str, Any]:
        """获取统计信息"""
        基础统计 = self._任务管理器.获取统计信息()
        基础统计["缓存大小"] = len(self._检测结果缓存)
        return 基础统计
    
    def 清除缓存(self):
        """清除检测缓存"""
        self._检测结果缓存.clear()


class 异步技能循环引擎:
    """异步技能循环引擎"""
    
    def __init__(self, 异步检测器: 异步技能检测器, 按键接口, 配置管理器):
        """
        初始化异步技能循环引擎
        
        参数:
            异步检测器: 异步技能检测器
            按键接口: 按键操作接口
            配置管理器: 配置管理器
        """
        self._异步检测器 = 异步检测器
        self._按键接口 = 按键接口
        self._配置管理器 = 配置管理器
        
        self._当前模式 = 0
        self._七情和合状态 = 0
        self._执行次数 = 0
        
        # 性能监控
        self._平均响应时间 = 0.0
        self._总响应时间 = 0.0
    
    async def 异步执行一次循环(self) -> bool:
        """
        异步执行一次技能循环
        
        返回:
            是否成功执行了技能释放
        """
        if self._当前模式 == 0:
            return False
        
        开始时间 = time.time()
        
        try:
            # 获取配置
            技能字典 = self._配置管理器.技能字典
            气劲字典 = self._配置管理器.气劲字典
            蓝条配置 = self._配置管理器.获取蓝条配置()
            检测区域 = self._配置管理器.获取检测区域()
            自动选人键值 = self._配置管理器.获取自动选人键值()
            
            # 异步检测技能状态（这里简化了决策逻辑）
            # 实际使用时需要根据具体策略进行异步决策
            技能配置列表 = list(技能字典.values())
            可用技能 = await self._异步检测器.异步批量检测技能(技能配置列表, 检测区域)
            
            # 简单的决策逻辑（实际使用时需要更复杂的策略）
            技能键值 = 0
            if 可用技能:
                # 选择第一个可用技能（实际使用时需要优先级排序）
                技能键值 = list(可用技能.values())[0]
            
            # 执行按键操作
            if 技能键值 > 0:
                # 先执行自动选人（如果有配置）
                if 自动选人键值 > 0:
                    await asyncio.get_event_loop().run_in_executor(
                        None, self._按键接口.按下并释放, 自动选人键值
                    )
                
                # 释放技能
                成功 = await asyncio.get_event_loop().run_in_executor(
                    None, self._按键接口.按下并释放, 技能键值
                )
                
                if 成功:
                    self._执行次数 += 1
                    
                    # 更新性能统计
                    响应时间 = time.time() - 开始时间
                    self._总响应时间 += 响应时间
                    self._平均响应时间 = self._总响应时间 / self._执行次数
                    
                    return True
            
            return False
            
        except Exception as e:
            print(f"异步技能循环执行错误: {e}")
            return False
    
    def 设置循环模式(self, 模式: int):
        """设置循环模式"""
        self._当前模式 = 模式
    
    def 获取性能统计(self) -> Dict[str, Any]:
        """获取性能统计信息"""
        检测统计 = self._异步检测器.获取统计信息()
        
        return {
            "执行次数": self._执行次数,
            "平均响应时间": f"{self._平均响应时间:.3f}秒",
            "检测统计": 检测统计
        }


# 使用示例
if __name__ == "__main__":
    # 创建异步任务管理器
    任务管理器 = 异步任务管理器()
    
    # 模拟异步图像处理接口
    class 模拟异步图像处理接口(异步图像处理接口):
        async def 异步获取屏幕图像(self, 区域: tuple) -> Any:
            await asyncio.sleep(0.01)  # 模拟IO操作
            return f"图像_{区域}"
        
        async def 异步技能检测(self, 图像: Any, 技能配置: Dict[str, Any]) -> int:
            await asyncio.sleep(0.005)  # 模拟处理时间
            return 81  # 模拟技能键值
    
    # 测试异步检测器
    图像接口 = 模拟异步图像处理接口()
    异步检测器 = 异步技能检测器(图像接口, 任务管理器)
    
    # 测试异步检测
    async def 测试异步检测():
        技能配置 = {"技能名称": "青川濯莲", "键值": 81}
        检测区域 = (0, 0, 100, 100)
        
        结果 = await 异步检测器.异步检测技能状态(技能配置, 检测区域)
        print(f"异步检测结果: {结果}")
        
        # 获取统计信息
        统计 = 异步检测器.获取统计信息()
        print(f"检测器统计: {统计}")
    
    # 运行测试
    asyncio.run(测试异步检测())
    
    # 关闭任务管理器
    任务管理器.关闭()