"""
图像处理缓存优化模块
提供图像缓存和增量更新功能
"""
import time
from typing import Dict, Any, Optional, Tuple
import threading
from dataclasses import dataclass
from collections import deque


@dataclass
class 缓存条目:
    """缓存条目数据结构"""
    图像数据: Any
    缓存时间: float
    访问次数: int = 0
    区域: Optional[Tuple] = None


class 图像缓存管理器:
    """图像缓存管理器"""
    
    def __init__(self, 最大缓存大小: int = 20, 默认缓存时间: float = 0.05, 
                 启用智能缓存: bool = True, 自适应调整: bool = True):
        """
        初始化缓存管理器
        
        参数:
            最大缓存大小: 最大缓存条目数（优化：增加到20条）
            默认缓存时间: 默认缓存有效期（秒）（优化：缩短到50ms）
            启用智能缓存: 是否启用智能缓存策略
            自适应调整: 是否启用自适应调整
        """
        self._缓存: Dict[str, 缓存条目] = {}
        self._最大缓存大小 = 最大缓存大小
        self._默认缓存时间 = 默认缓存时间
        self._锁 = threading.RLock()
        
        # 统计信息
        self._命中次数 = 0
        self._未命中次数 = 0
        self._清理次数 = 0
        
        # 智能缓存配置
        self._启用智能缓存 = 启用智能缓存
        self._自适应调整 = 自适应调整
        self._命中率阈值 = 0.8  # 目标命中率
        self._最近命中率: deque = deque(maxlen=20)  # 最近20次命中率
        
        # 性能优化
        self._缓存命中分布: Dict[str, int] = {}  # 各缓存键的命中次数
        self._最后调整时间 = time.time()
        self._调整间隔 = 30  # 调整间隔（秒）
        

    
    def 获取图像(self, 缓存键: str, 获取函数: callable, 区域: Tuple, 
               缓存时间: Optional[float] = None) -> Any:
        """
        获取图像（带缓存）
        
        参数:
            缓存键: 缓存键值
            获取函数: 图像获取函数
            区域: 截图区域
            缓存时间: 缓存有效期（秒），None则使用默认值
            
        返回:
            图像数据
        """
        if 缓存时间 is None:
            缓存时间 = self._默认缓存时间
        
        当前时间 = time.time()
        
        with self._锁:
            # 检查缓存
            条目 = self._缓存.get(缓存键)
            
            if 条目 and (当前时间 - 条目.缓存时间) <= 缓存时间:
                # 缓存命中
                self._命中次数 += 1
                条目.访问次数 += 1
                
                # 记录命中分布
                self._缓存命中分布[缓存键] = self._缓存命中分布.get(缓存键, 0) + 1
                
                # 智能缓存优化：更新命中率统计
                if self._启用智能缓存:
                    self._更新命中率统计(True)
                    self._自适应调整缓存策略()
                
                return 条目.图像数据
            
            # 缓存未命中或过期
            self._未命中次数 += 1
            
            # 智能缓存优化：更新命中率统计
            if self._启用智能缓存:
                self._更新命中率统计(False)
            
            # 获取新图像
            图像数据 = 获取函数(区域)
            
            # 更新缓存
            self._设置缓存(缓存键, 图像数据, 区域, 当前时间)
            
            return 图像数据
    
    def _设置缓存(self, 缓存键: str, 图像数据: Any, 区域: Tuple, 缓存时间: float):
        """设置缓存条目"""
        with self._锁:
            # 检查是否需要清理
            if len(self._缓存) >= self._最大缓存大小:
                self._清理过期缓存()
            
            # 添加新条目
            self._缓存[缓存键] = 缓存条目(
                图像数据=图像数据,
                缓存时间=缓存时间,
                区域=区域
            )
    
    def _清理过期缓存(self):
        """清理过期缓存"""
        当前时间 = time.time()
        过期键值 = []
        
        with self._锁:
            for 键, 条目 in self._缓存.items():
                if (当前时间 - 条目.缓存时间) > self._默认缓存时间:
                    过期键值.append(键)
            
            for 键 in 过期键值:
                del self._缓存[键]
            
            self._清理次数 += len(过期键值)
            
            # 如果仍然超过大小，按访问频率清理
            if len(self._缓存) >= self._最大缓存大小:
                排序条目 = sorted(self._缓存.items(), key=lambda x: x[1].访问次数)
                for 键, _ in 排序条目[:len(排序条目) // 2]:
                    del self._缓存[键]
    
    def 清除缓存(self):
        """清除所有缓存"""
        with self._锁:
            self._缓存.clear()
    
    def _更新命中率统计(self, 命中: bool):
        """更新命中率统计"""
        当前时间 = time.time()
        
        # 计算当前命中率
        总次数 = self._命中次数 + self._未命中次数
        if 总次数 > 0:
            当前命中率 = self._命中次数 / 总次数
            self._最近命中率.append(当前命中率)
    
    def _自适应调整缓存策略(self):
        """自适应调整缓存策略"""
        当前时间 = time.time()
        
        # 避免频繁调整
        if 当前时间 - self._最后调整时间 < self._调整间隔:
            return
        
        # 计算平均命中率
        if len(self._最近命中率) > 0:
            平均命中率 = sum(self._最近命中率) / len(self._最近命中率)
            
            # 根据命中率调整缓存策略
            if 平均命中率 < self._命中率阈值:
                # 命中率低，增加缓存大小或延长缓存时间
                if self._最大缓存大小 < 50:  # 最大限制
                    self._最大缓存大小 += 1
                
                if self._默认缓存时间 < 0.5:  # 最多500ms
                    self._默认缓存时间 *= 1.1
            
            elif 平均命中率 > self._命中率阈值 + 0.1:
                # 命中率高，可以适当减少缓存
                if self._最大缓存大小 > 5:  # 最小限制
                    self._最大缓存大小 -= 1
                
                if self._默认缓存时间 > 0.05:  # 最少50ms
                    self._默认缓存时间 *= 0.9
        
        # 更新最后调整时间
        self._最后调整时间 = 当前时间
    
    def _智能清理缓存(self):
        """智能清理缓存策略"""
        当前时间 = time.time()
        
        with self._锁:
            # 根据访问频率进行智能清理
            访问频率统计 = {}
            for 键, 条目 in self._缓存.items():
                访问频率统计[键] = 条目.访问次数
            
            # 按访问频率排序
            排序条目 = sorted(访问频率统计.items(), key=lambda x: x[1])
            
            # 清理访问频率最低的30%缓存
            清理数量 = max(len(排序条目) // 3, 1)
            for 键, _ in 排序条目[:清理数量]:
                if 键 in self._缓存:
                    del self._缓存[键]
            
            self._清理次数 += 清理数量
            
            print(f"智能清理完成: 清理{清理数量}个低访问频率缓存")
    
    def 获取智能统计信息(self) -> Dict[str, Any]:
        """获取智能缓存统计信息"""
        with self._锁:
            基础统计 = self.获取统计信息()
            
            # 计算命中率趋势
            if len(self._最近命中率) > 0:
                平均命中率 = sum(self._最近命中率) / len(self._最近命中率)
                命中率趋势 = "稳定"
                
                if len(self._最近命中率) >= 10:
                    最近命中率 = list(self._最近命中率)[-10:]
                    早期命中率 = list(self._最近命中率)[-10:-5]
                    后期命中率 = list(self._最近命中率)[-5:]
                    
                    早期平均 = sum(早期命中率) / len(早期命中率)
                    后期平均 = sum(后期命中率) / len(后期命中率)
                    
                    if 后期平均 > 早期平均 + 0.05:
                        命中率趋势 = "上升"
                    elif 后期平均 < 早期平均 - 0.05:
                        命中率趋势 = "下降"
            else:
                平均命中率 = 0.0
                命中率趋势 = "无数据"
            
            # 计算缓存效率
            缓存效率 = {
                "平均命中率": 平均命中率,
                "命中率趋势": 命中率趋势,
                "最近命中率样本": len(self._最近命中率),
                "缓存大小": len(self._缓存),
                "自适应调整": self._自适应调整,
                "智能缓存启用": self._启用智能缓存
            }
            
            return {
                "基础统计": 基础统计,
                "缓存效率": 缓存效率,
                "命中分布": dict(sorted(self._缓存命中分布.items(), key=lambda x: x[1], reverse=True)[:10])
            }
    
    def 获取统计信息(self) -> Dict[str, Any]:
        """获取缓存统计信息"""
        with self._锁:
            命中率 = self._命中次数 / max(self._命中次数 + self._未命中次数, 1)
            
            return {
                "缓存大小": len(self._缓存),
                "最大缓存大小": self._最大缓存大小,
                "命中次数": self._命中次数,
                "未命中次数": self._未命中次数,
                "命中率": f"{命中率:.2%}",
                "清理次数": self._清理次数,
                "平均访问次数": sum(e.访问次数 for e in self._缓存.values()) / max(len(self._缓存), 1)
            }


class 增量图像处理器:
    """增量图像处理器"""
    
    def __init__(self):
        self._缓存管理器 = 图像缓存管理器()
        self._上次图像 = None
        self._上次区域 = None
    
    def 获取增量图像(self, 获取函数: callable, 区域: Tuple, 
                   缓存键: str = "default") -> Tuple[Any, bool]:
        """
        获取增量图像
        
        参数:
            获取函数: 图像获取函数
            区域: 截图区域
            缓存键: 缓存键值
            
        返回:
            (图像数据, 是否来自增量)
        """
        # 先尝试获取完整图像
        当前图像 = self._缓存管理器.获取图像(缓存键, 获取函数, 区域)
        
        # 检查是否可以使用增量
        if self._上次图像 is not None and self._上次区域 == 区域:
            # 简单比较图像差异（实际使用时可以更复杂）
            if self._图像差异较小(当前图像, self._上次图像):
                return self._上次图像, True
        
        # 更新缓存
        self._上次图像 = 当前图像
        self._上次区域 = 区域
        
        return 当前图像, False
    
    def _图像差异较小(self, 图像1: Any, 图像2: Any, 阈值: float = 0.95) -> bool:
        """
        简单判断两幅图像差异是否较小
        实际使用时可以替换为更复杂的图像相似度算法
        """
        try:
            # 简单的像素差异比较
            # 实际使用时可以替换为SSIM、MSE等算法
            import numpy as np
            
            if 图像1.shape != 图像2.shape:
                return False
            
            # 计算平均差异
            差异 = np.abs(图像1.astype(float) - 图像2.astype(float))
            平均差异 = np.mean(差异)
            
            # 如果差异小于阈值则认为相似
            return 平均差异 < 10  # 简单阈值
            
        except Exception:
            # 如果无法计算差异，返回False
            return False
    
    def 获取缓存统计(self) -> Dict[str, Any]:
        """获取缓存统计信息"""
        return self._缓存管理器.获取统计信息()


# 使用示例
if __name__ == "__main__":
    # 创建缓存管理器
    缓存管理器 = 图像缓存管理器()
    
    # 模拟图像获取函数
    def 模拟获取图像(区域):
        import numpy as np
        x1, y1, x2, y2 = 区域
        宽度 = x2 - x1
        高度 = y2 - y1
        return np.random.randint(0, 255, (高度, 宽度, 3), dtype=np.uint8)
    
    # 测试缓存功能
    区域 = (0, 0, 100, 100)
    
    # 第一次获取（未命中）
    图像1 = 缓存管理器.获取图像("test", 模拟获取图像, 区域)
    print("第一次获取（未命中）")
    
    # 第二次获取（命中）
    图像2 = 缓存管理器.获取图像("test", 模拟获取图像, 区域)
    print("第二次获取（命中）")
    
    # 获取统计信息
    统计 = 缓存管理器.获取统计信息()
    print(f"缓存统计: {统计}")