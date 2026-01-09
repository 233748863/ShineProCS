"""
幽灵盒子 (ghostboxm) 按键操作接口
基于 ghostboxm.pyd 驱动封装
支持自适应延迟优化
"""
import time
import logging
from typing import Optional

from interface.按键操作接口 import 按键操作接口
from utils.自适应延迟 import 智能延迟

class 幽灵盒子按键接口(按键操作接口):
    """幽灵盒子硬件按键接口实现，继承自标准按键操作接口"""
    
    def __init__(self, 调试模式: bool = False):
        """
        初始化幽灵盒子接口
        
        参数:
            调试模式: 是否启用调试信息
        """
        self.调试模式 = 调试模式
        self.设备已连接 = False
        self.初始化成功 = False
        
        # 设置日志
        logging.basicConfig(
            level=logging.DEBUG if 调试模式 else logging.INFO,
            format='%(asctime)s [幽灵盒子] %(message)s',
            datefmt='%H:%M:%S'
        )
        self.logger = logging.getLogger('幽灵盒子')
        
        # 尝试导入幽灵盒子库
        try:
            import ghostboxm as gb
            self.gb = gb
            self.logger.info("✅ 幽灵盒子库导入成功")
        except ImportError as e:
            self.logger.error(f"❌ 无法导入幽灵盒子库: {e}")
            self.logger.warning("⚠️ 请确保 ghostboxm.pyd 文件已复制到项目根目录")
            return
        
        # 初始化设备
        self.初始化设备()
    
    def 初始化设备(self) -> bool:
        """初始化幽灵盒子设备"""
        try:
            self.logger.info("正在初始化幽灵盒子设备...")
            
            # 获取SDK信息
            sdk_type = self.gb.SDKType()
            sdk_version = self.gb.SDKVersion()
            self.logger.info(f"SDK类型: {sdk_type}, 版本: {sdk_version}")
            
            # 打开设备
            open_result = self.gb.OpenDevice()
            if open_result == 0:
                self.logger.error("❌ 无法打开幽灵盒子设备")
                self.logger.warning("⚠️ 请检查设备是否连接，或驱动是否正确安装")
                return False
            
            self.logger.info("✅ 设备打开成功")
            
            # 获取设备信息
            model = self.gb.GetModel()
            self.logger.info(f"设备型号: {model}")
            
            # 设置速度优化模式
            self.gb.SetSpeedOptimizationMode(1)
            
            # 设置脚本兼容模式
            self.gb.SetScriptCompatibilityMode(1, 1)
            
            # 设置按键延迟（提高稳定性）
            self.gb.SetPressKeyDelay(30, 80)
            
            self.设备已连接 = True
            self.初始化成功 = True
            self.logger.success("✅ 幽灵盒子设备初始化完成")
            return True
            
        except Exception as e:
            self.logger.error(f"❌ 设备初始化失败: {e}")
            self.logger.warning("⚠️ 请检查幽灵盒子设备状态和连接")
            return False
    
    def 按键(self, 键值: int, 按键延迟: float = 0.1) -> bool:
        """
        按下并释放指定键值
        
        参数:
            键值: 键盘键值（如 81 对应 Q键）
            按键延迟: 按键间隔时间（秒）
            
        返回:
            bool: 按键是否成功
        """
        if not self.初始化成功:
            self.logger.error("❌ 设备未初始化，无法执行按键操作")
            return False
        
        try:
            # 按下并释放按键
            result = self.gb.PressAndReleaseKeyByValue(键值)
            
            if result == 0:
                self.logger.error(f"❌ 按键失败 (键值: {键值})")
                return False
            
            self.logger.debug(f"✅ 按键成功 (键值: {键值})")
            
            # 添加智能按键延迟
            if 按键延迟 > 0:
                实际延迟 = 智能延迟(按键延迟)
                self.logger.debug(f"智能延迟: {实际延迟:.3f}秒 (预期: {按键延迟:.3f}秒)")
            
            return True
            
        except Exception as e:
            self.logger.error(f"❌ 按键操作异常 (键值: {键值}): {e}")
            return False
    
    def 组合按键(self, 组合键: str, 按键延迟: float = 0.1) -> bool:
        """
        执行组合按键
        
        参数:
            组合键: 组合键字符串（如 "ctrl+c"）
            按键延迟: 按键间隔时间（秒）
            
        返回:
            bool: 组合按键是否成功
        """
        if not self.初始化成功:
            self.logger.error("❌ 设备未初始化，无法执行组合按键")
            return False
        
        try:
            result = self.gb.CombinationKey(组合键)
            
            if result == 0:
                self.logger.error(f"❌ 组合按键失败: {组合键}")
                return False
            
            self.logger.debug(f"✅ 组合按键成功: {组合键}")
            
            # 添加智能按键延迟
            if 按键延迟 > 0:
                实际延迟 = 智能延迟(按键延迟)
                self.logger.debug(f"智能延迟: {实际延迟:.3f}秒 (预期: {按键延迟:.3f}秒)")
            
            return True
            
        except Exception as e:
            self.logger.error(f"❌ 组合按键异常 ({组合键}): {e}")
            return False
    
    def 移动鼠标(self, x: int, y: int, 移动延迟: float = 0.1) -> bool:
        """
        移动鼠标到指定坐标
        
        参数:
            x: 目标X坐标
            y: 目标Y坐标
            移动延迟: 移动后延迟时间（秒）
            
        返回:
            bool: 移动是否成功
        """
        if not self.初始化成功:
            self.logger.error("❌ 设备未初始化，无法移动鼠标")
            return False
        
        try:
            result = self.gb.MoveMouseTo(x, y)
            
            if result == 0:
                self.logger.error(f"❌ 鼠标移动失败 (坐标: {x}, {y})")
                return False
            
            self.logger.debug(f"✅ 鼠标移动成功 (坐标: {x}, {y})")
            
            # 添加智能延迟
            if 移动延迟 > 0:
                实际延迟 = 智能延迟(移动延迟)
                self.logger.debug(f"智能延迟: {实际延迟:.3f}秒 (预期: {移动延迟:.3f}秒)")
            
            return True
            
        except Exception as e:
            self.logger.error(f"❌ 鼠标移动异常 ({x}, {y}): {e}")
            return False
    
    def 点击鼠标(self, 按钮: int = 1, 点击延迟: float = 0.1) -> bool:
        """
        点击鼠标按钮
        
        参数:
            按钮: 1=左键, 2=右键, 3=中键
            点击延迟: 点击间隔时间（秒）
            
        返回:
            bool: 点击是否成功
        """
        if not self.初始化成功:
            self.logger.error("❌ 设备未初始化，无法点击鼠标")
            return False
        
        try:
            result = self.gb.PressAndReleaseMouseButton(按钮)
            
            if result == 0:
                self.logger.error(f"❌ 鼠标点击失败 (按钮: {按钮})")
                return False
            
            self.logger.debug(f"✅ 鼠标点击成功 (按钮: {按钮})")
            
            # 添加智能延迟
            if 点击延迟 > 0:
                实际延迟 = 智能延迟(点击延迟)
                self.logger.debug(f"智能延迟: {实际延迟:.3f}秒 (预期: {点击延迟:.3f}秒)")
            
            return True
            
        except Exception as e:
            self.logger.error(f"❌ 鼠标点击异常 (按钮: {按钮}): {e}")
            return False
    
    def 获取设备状态(self) -> dict:
        """获取设备状态信息"""
        if not self.初始化成功:
            return {"状态": "未初始化", "错误": "设备连接失败"}
        
        try:
            status = {
                "状态": "已连接",
                "设备型号": self.gb.GetModel(),
                "连接状态": self.gb.DeviceConnectState(),
                "设备能力": self.gb.GetDeviceAbility(),
                "CapsLock状态": self.gb.GetCapslock(),
                "NumLock状态": self.gb.GetNumlock(),
                "鼠标位置": f"({self.gb.GetMouseX()}, {self.gb.GetMouseY()})"
            }
            return status
        except Exception as e:
            return {"状态": "错误", "错误信息": str(e)}
    
    def 关闭设备(self):
        """关闭幽灵盒子设备"""
        if self.初始化成功:
            try:
                self.gb.CloseDevice()
                self.logger.info("✅ 幽灵盒子设备已关闭")
            except Exception as e:
                self.logger.error(f"❌ 关闭设备失败: {e}")
            finally:
                self.初始化成功 = False
                self.设备已连接 = False
    
    def __del__(self):
        """析构函数，自动关闭设备"""
        self.关闭设备()


# 键值映射表（常用键值）
键值映射表 = {
    # 字母键
    'a': 65, 'b': 66, 'c': 67, 'd': 68, 'e': 69, 'f': 70, 'g': 71, 'h': 72, 'i': 73,
    'j': 74, 'k': 75, 'l': 76, 'm': 77, 'n': 78, 'o': 79, 'p': 80, 'q': 81, 'r': 82,
    's': 83, 't': 84, 'u': 85, 'v': 86, 'w': 87, 'x': 88, 'y': 89, 'z': 90,
    
    # 数字键
    '0': 48, '1': 49, '2': 50, '3': 51, '4': 52, '5': 53, '6': 54, '7': 55, '8': 56, '9': 57,
    
    # 功能键
    'f1': 112, 'f2': 113, 'f3': 114, 'f4': 115, 'f5': 116, 'f6': 117, 'f7': 118, 
    'f8': 119, 'f9': 120, 'f10': 121, 'f11': 122, 'f12': 123,
    
    # 特殊键
    'esc': 27, 'tab': 9, 'capslock': 20, 'shift': 16, 'ctrl': 17, 'alt': 18,
    'space': 32, 'enter': 13, 'backspace': 8, 'delete': 46,
    
    # 方向键
    'left': 37, 'up': 38, 'right': 39, 'down': 40,
    
    # 符号键
    '`': 192, '-': 189, '=': 187, '[': 219, ']': 221, '\\': 220, ';': 186, "'": 222,
    ',': 188, '.': 190, '/': 191
}


def 获取键值(按键名称: str) -> Optional[int]:
    """根据按键名称获取键值"""
    return 键值映射表.get(按键名称.lower())


def 创建幽灵盒子接口(调试模式: bool = False) -> Optional[幽灵盒子按键接口]:
    """
    创建幽灵盒子接口实例
    
    参数:
        调试模式: 是否启用调试信息
        
    返回:
        幽灵盒子按键接口实例，失败返回None
    """
    try:
        return 幽灵盒子按键接口(调试模式)
    except Exception as e:
        logging.error(f"❌ 创建幽灵盒子接口失败: {e}")
        return None