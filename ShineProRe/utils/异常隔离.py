"""
异常隔离模块
提供模块级异常处理和安全执行机制
支持自适应延迟优化
"""
import sys
import traceback
import time
from typing import Any, Callable, Optional, Dict
from functools import wraps
from collections import defaultdict, deque
from utils.自适应延迟 import 智能延迟


class 异常隔离器:
    """
    异常隔离器
    提供安全的函数执行环境，防止异常传播
    """
    
    # 异常控制配置
    _异常频率控制 = defaultdict(lambda: {"计数": 0, "最后时间": 0})
    _异常记录频率 = 5  # 每5秒记录一次相同异常
    _文件记录间隔 = 1.0  # 文件记录最小间隔（秒）
    _最后文件记录时间 = 0
    _启用详细堆栈模式 = False  # 默认不记录详细堆栈
    _异常统计 = defaultdict(lambda: {"总次数": 0, "最后记录时间": 0})
    """
    异常隔离器
    提供安全的函数执行环境，防止异常传播
    """
    
    @staticmethod
    def 安全执行(函数: Callable, *args, **kwargs) -> tuple[bool, Any, str]:
        """
        安全执行函数，捕获所有异常
        
        参数:
            函数: 要执行的函数
            *args: 函数参数
            **kwargs: 函数关键字参数
        
        返回:
            tuple: (是否成功, 返回值, 错误信息)
        """
        try:
            结果 = 函数(*args, **kwargs)
            return True, 结果, ""
        except Exception as e:
            错误信息 = f"{type(e).__name__}: {str(e)}"
            return False, None, 错误信息
    
    @staticmethod
    def 模块级异常处理(函数: Callable) -> Callable:
        """
        装饰器：为函数添加模块级异常处理
        
        参数:
            函数: 要装饰的函数
        
        返回:
            Callable: 装饰后的函数
        """
        @wraps(函数)
        def 包装器(*args, **kwargs) -> Optional[Any]:
            try:
                return 函数(*args, **kwargs)
            except Exception as e:
                # 优化：减少频繁的日志记录，只在必要时记录
                异常类型 = type(e).__name__
                
                # 控制台输出（优化：减少输出频率）
                if 异常隔离器._应该记录异常(函数.__name__, 异常类型):
                    print(f"模块级异常处理 - {函数.__name__}: {e}")
                    
                    # 文件日志（优化：减少IO操作）
                    if 异常隔离器._应该记录到文件():
                        with open("异常日志.txt", "a", encoding="utf-8") as f:
                            f.write(f"[{异常隔离器._获取当前时间()}] {函数.__name__}: {e}\n")
                            # 优化：仅在调试模式下记录堆栈
                            if 异常隔离器._启用详细堆栈():
                                f.write(traceback.format_exc())
                                f.write("\n" + "="*50 + "\n")
                            else:
                                f.write("\n")
                
                return None
        return 包装器
    
    @staticmethod
    def 安全上下文管理器():
        """
        安全上下文管理器
        在上下文中执行代码，自动处理异常
        """
        class 安全上下文:
            def __enter__(self):
                return self
            
            def __exit__(self, exc_type, exc_val, exc_tb):
                if exc_type is not None:
                    # 优化：减少频繁的日志记录
                    if 异常隔离器._应该记录异常("安全上下文", exc_type.__name__):
                        print(f"安全上下文捕获异常: {exc_type.__name__}: {exc_val}")
                        
                        # 文件日志（优化：减少IO操作）
                        if 异常隔离器._应该记录到文件():
                            with open("上下文异常日志.txt", "a", encoding="utf-8") as f:
                                f.write(f"[{异常隔离器._获取当前时间()}] 安全上下文异常:\n")
                                f.write(f"类型: {exc_type.__name__}\n")
                                f.write(f"信息: {exc_val}\n")
                                # 优化：仅在调试模式下记录堆栈
                                if 异常隔离器._启用详细堆栈():
                                    f.write(traceback.format_exc())
                                    f.write("\n" + "="*50 + "\n")
                                else:
                                    f.write("\n")
                    # 抑制异常，不传播
                    return True
                return False
        
        return 安全上下文()
    
    @staticmethod
    def 重试机制(最大重试次数: int = 3, 重试间隔: float = 1.0):
        """
        装饰器：为函数添加重试机制
        
        参数:
            最大重试次数: 最大重试次数
            重试间隔: 重试间隔（秒）
        """
        def 装饰器(函数: Callable) -> Callable:
            @wraps(函数)
            def 包装器(*args, **kwargs) -> Optional[Any]:
                import time
                
                重试次数 = 0
                while 重试次数 <= 最大重试次数:
                    try:
                        return 函数(*args, **kwargs)
                    except Exception as e:
                        重试次数 += 1
                        if 重试次数 > 最大重试次数:
                            print(f"函数 {函数.__name__} 重试 {最大重试次数} 次后仍失败: {e}")
                            return None
                        
                        print(f"函数 {函数.__name__} 第 {重试次数} 次重试，错误: {e}")
                        实际延迟 = 智能延迟(重试间隔)
                        print(f"智能重试延迟: {实际延迟:.3f}秒 (预期: {重试间隔:.3f}秒)")
                
                return None
            return 包装器
        return 装饰器
    
    @staticmethod
    def _获取当前时间() -> str:
        """获取当前时间字符串"""
        import datetime
        return datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    
    @staticmethod
    def _应该记录异常(函数名称: str, 异常类型: str) -> bool:
        """判断是否应该记录异常（频率控制）"""
        当前时间 = time.time()
        异常键 = f"{函数名称}_{异常类型}"
        
        # 更新统计
        异常隔离器._异常统计[异常键]["总次数"] += 1
        
        # 频率控制：相同异常每5秒记录一次
        if (当前时间 - 异常隔离器._异常频率控制[异常键]["最后时间"]) > 异常隔离器._异常记录频率:
            异常隔离器._异常频率控制[异常键]["最后时间"] = 当前时间
            异常隔离器._异常频率控制[异常键]["计数"] = 1
            return True
        else:
            异常隔离器._异常频率控制[异常键]["计数"] += 1
            return 异常隔离器._异常频率控制[异常键]["计数"] <= 3  # 前3次快速记录
    
    @staticmethod
    def _应该记录到文件() -> bool:
        """判断是否应该记录到文件（IO频率控制）"""
        当前时间 = time.time()
        
        # 文件记录频率控制
        if (当前时间 - 异常隔离器._最后文件记录时间) > 异常隔离器._文件记录间隔:
            异常隔离器._最后文件记录时间 = 当前时间
            return True
        return False
    
    @staticmethod
    def _启用详细堆栈() -> bool:
        """判断是否启用详细堆栈记录"""
        return 异常隔离器._启用详细堆栈模式
    
    @staticmethod
    def 设置异常记录配置(记录频率: int = 5, 文件间隔: float = 1.0, 详细堆栈: bool = False):
        """设置异常记录配置"""
        异常隔离器._异常记录频率 = 记录频率
        异常隔离器._文件记录间隔 = 文件间隔
        异常隔离器._启用详细堆栈模式 = 详细堆栈
    
    @staticmethod
    def 获取异常统计() -> dict:
        """获取异常统计信息"""
        return dict(异常隔离器._异常统计)


def 安全执行技能检测(函数: Callable) -> Callable:
    """
    装饰器：专门用于技能检测的安全执行
    """
    @wraps(函数)
    def 包装器(*args, **kwargs) -> Optional[int]:
        with 异常隔离器.安全上下文管理器():
            结果 = 函数(*args, **kwargs)
            
            # 验证结果类型
            if 结果 is not None and not isinstance(结果, int):
                print(f"技能检测返回类型错误: {type(结果)}")
                return 0
            
            return 结果
    return 包装器


def 安全执行按键操作(函数: Callable) -> Callable:
    """
    装饰器：专门用于按键操作的安全执行
    """
    @wraps(函数)
    def 包装器(*args, **kwargs) -> bool:
        成功, 结果, 错误信息 = 异常隔离器.安全执行(函数, *args, **kwargs)
        
        if not 成功:
            print(f"按键操作失败: {错误信息}")
            return False
        
        # 确保返回布尔值
        if isinstance(结果, bool):
            return 结果
        else:
            # 非布尔值视为成功
            return True
    return 包装器


# 统一的异常处理标准
异常处理标准 = {
    "错误信息格式": "{时间} {模块} {操作}: {异常类型}: {异常信息}",
    "日志级别": {
        "调试": 0,
        "信息": 1,
        "警告": 2,
        "错误": 3
    },
    "默认日志级别": "错误",
    "是否记录堆栈": True,
    "是否抑制异常": False,
    "最大重试次数": 3,
    "默认重试间隔": 1.0
}


class 统一异常处理器:
    """统一异常处理器，提供一致的异常处理机制"""
    
    def __init__(self, 模块名称: str = "未知模块"):
        self.模块名称 = 模块名称
        self.异常统计 = {
            "总异常数": 0,
            "按类型统计": {},
            "按操作统计": {}
        }
    
    def 安全执行(self, 操作名称: str, 函数: Callable, *args, **kwargs) -> Any:
        """
        统一的安全执行方法
        
        参数:
            操作名称: 操作名称
            函数: 要执行的函数
            *args: 函数参数
            **kwargs: 函数关键字参数
        
        返回:
            Any: 函数执行结果，异常时返回None
        """
        try:
            结果 = 函数(*args, **kwargs)
            return 结果
        except Exception as e:
            self._记录异常(操作名称, e)
            return None
    
    def 带重试执行(self, 操作名称: str, 函数: Callable, 最大重试次数: int = None, 
                  重试间隔: float = None, *args, **kwargs) -> Any:
        """
        带重试的异常处理执行
        
        参数:
            操作名称: 操作名称
            函数: 要执行的函数
            最大重试次数: 最大重试次数
            重试间隔: 重试间隔（秒）
            *args: 函数参数
            **kwargs: 函数关键字参数
        
        返回:
            Any: 函数执行结果
        """
        最大重试次数 = 最大重试次数 or 异常处理标准["最大重试次数"]
        重试间隔 = 重试间隔 or 异常处理标准["默认重试间隔"]
        
        重试次数 = 0
        while 重试次数 <= 最大重试次数:
            try:
                结果 = 函数(*args, **kwargs)
                return 结果
            except Exception as e:
                重试次数 += 1
                if 重试次数 > 最大重试次数:
                    self._记录异常(操作名称, e, f"重试{最大重试次数}次后仍失败")
                    return None
                
                print(f"{操作名称} 第{重试次数}次重试，错误: {e}")
                实际延迟 = 智能延迟(重试间隔)
                print(f"智能重试延迟: {实际延迟:.3f}秒 (预期: {重试间隔:.3f}秒)")
        
        return None
    
    def _记录异常(self, 操作名称: str, 异常: Exception, 额外信息: str = ""):
        """记录异常信息"""
        import datetime
        
        # 更新统计
        self.异常统计["总异常数"] += 1
        
        异常类型 = type(异常).__name__
        self.异常统计["按类型统计"][异常类型] = self.异常统计["按类型统计"].get(异常类型, 0) + 1
        self.异常统计["按操作统计"][操作名称] = self.异常统计["按操作统计"].get(操作名称, 0) + 1
        
        # 格式化错误信息
        时间 = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        错误信息 = 异常处理标准["错误信息格式"].format(
            时间=时间,
            模块=self.模块名称,
            操作=操作名称,
            异常类型=异常类型,
            异常信息=str(异常)
        )
        
        if 额外信息:
            错误信息 += f" ({额外信息})"
        
        # 记录到日志文件
        with open("统一异常日志.txt", "a", encoding="utf-8") as f:
            f.write(错误信息 + "\n")
            if 异常处理标准["是否记录堆栈"]:
                f.write(traceback.format_exc())
                f.write("\n" + "="*50 + "\n")
        
        # 控制台输出
        print(错误信息)
    
    def 获取异常统计(self) -> Dict[str, Any]:
        """获取异常统计信息"""
        return self.异常统计.copy()
    
    def 重置异常统计(self):
        """重置异常统计"""
        self.异常统计 = {
            "总异常数": 0,
            "按类型统计": {},
            "按操作统计": {}
        }


# 全局异常处理实例
全局异常处理器 = 统一异常处理器("全局")

# 全局异常隔离器实例
全局异常隔离器 = 异常隔离器()