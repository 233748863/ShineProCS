#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
基于OpenCV模板匹配的技能循环引擎示例 - 优化版
使用更友好的日志系统，方便排查问题
"""

import time
import os
import json
import logging
from opencv_技能状态检测器 import OpenCV技能状态检测器, 智能图像适配器
from 技能循环引擎.interface.按键操作接口 import 按键操作接口


class 友好日志系统:
    """友好的日志系统，为示例代码专门优化"""
    
    def __init__(self, 调试模式: bool = False):
        self.调试模式 = 调试模式
        
        # 设置日志格式
        logging.basicConfig(
            level=logging.INFO if not 调试模式 else logging.DEBUG,
            format='%(asctime)s [%(levelname)s] %(message)s',
            datefmt='%H:%M:%S'
        )
        self.logger = logging.getLogger('技能循环引擎')
    
    def 标题(self, 标题: str):
        """输出标题"""
        self.logger.info(f"🎯 {'='*50}")
        self.logger.info(f"🎯 {标题}")
        self.logger.info(f"🎯 {'='*50}")
    
    def 信息(self, 消息: str):
        """输出信息级别日志"""
        self.logger.info(f"📝 {消息}")
    
    def 调试(self, 消息: str):
        """输出调试级别日志"""
        if self.调试模式:
            self.logger.debug(f"🔍 {消息}")
    
    def 成功(self, 消息: str):
        """输出成功级别日志"""
        self.logger.info(f"✅ {消息}")
    
    def 警告(self, 消息: str):
        """输出警告级别日志"""
        self.logger.warning(f"⚠️ {消息}")
    
    def 错误(self, 消息: str):
        """输出错误级别日志"""
        self.logger.error(f"❌ {消息}")


class 真实按键适配器(按键操作接口):
    """
    真实的按键适配器实现
    需要安装: pip install pynput 或 pip install pyautogui
    """
    
    def __init__(self, 使用库: str = "pynput", 日志系统=None):
        """
        初始化按键适配器
        
        参数:
            使用库: "pynput" 或 "pyautogui"
            日志系统: 友好的日志系统实例
        """
        self.使用库 = 使用库
        self.日志 = 日志系统 or 友好日志系统()
        
        try:
            if 使用库 == "pynput":
                from pynput.keyboard import Controller, Key
                self.键盘 = Controller()
                self.日志.成功("按键适配器: 使用 pynput 库")
            elif 使用库 == "pyautogui":
                import pyautogui
                self.pyautogui = pyautogui
                self.日志.成功("按键适配器: 使用 pyautogui 库")
            else:
                raise ValueError("不支持的按键库")
        except ImportError as e:
            self.日志.错误(f"按键库导入失败: {e}")
            self.日志.警告("请运行: pip install pynput 或 pip install pyautogui")
            raise
    
    def 按下按键(self, 键值: int) -> bool:
        """按下指定键值的按键"""
        try:
            if self.使用库 == "pynput":
                self.键盘.press(chr(键值) if 键值 < 128 else Key.from_vk(键值))
            else:
                self.pyautogui.keyDown(chr(键值) if 键值 < 128 else str(键值))
            
            self.日志.调试(f"按下按键: {键值} ({chr(键值) if 键值 < 128 else '特殊键'})")
            return True
        except Exception as e:
            self.日志.错误(f"按下按键失败: {键值}, 错误: {e}")
            return False
    
    def 释放按键(self, 键值: int) -> bool:
        """释放指定键值的按键"""
        try:
            if self.使用库 == "pynput":
                self.键盘.release(chr(键值) if 键值 < 128 else Key.from_vk(键值))
            else:
                self.pyautogui.keyUp(chr(键值) if 键值 < 128 else str(键值))
            
            self.日志.调试(f"释放按键: {键值}")
            return True
        except Exception as e:
            self.日志.错误(f"释放按键失败: {键值}, 错误: {e}")
            return False
    
    def 按下并释放(self, 键值: int) -> bool:
        """按下并立即释放指定键值的按键"""
        try:
            if self.使用库 == "pynput":
                self.键盘.press(chr(键值) if 键值 < 128 else Key.from_vk(键值))
                time.sleep(0.05)  # 短暂延迟
                self.键盘.release(chr(键值) if 键值 < 128 else Key.from_vk(键值))
            else:
                self.pyautogui.press(chr(键值) if 键值 < 128 else str(键值))
            
            self.日志.成功(f"释放技能按键: {键值} ({chr(键值) if 键值 < 128 else '特殊键'})")
            return True
        except Exception as e:
            self.日志.错误(f"释放技能失败: {键值}, 错误: {e}")
            return False
    
    def 释放所有按键(self) -> bool:
        """释放所有当前按下的按键"""
        try:
            if self.使用库 == "pyautogui":
                self.pyautogui.keyUp("shift")
                self.pyautogui.keyUp("ctrl") 
                self.pyautogui.keyUp("alt")
            # pynput会自动管理按键状态
            
            self.日志.信息("已释放所有按键")
            return True
        except Exception as e:
            self.日志.错误(f"释放所有按键失败: {e}")
            return False


class OpenCV技能循环引擎:
    """基于OpenCV模板匹配的技能循环引擎 - 优化版"""
    
    def __init__(self, 配置路径: str, 按键适配器: 按键操作接口, 调试模式: bool = False):
        """
        初始化OpenCV技能循环引擎
        
        参数:
            配置路径: OpenCV配置文件路径
            按键适配器: 按键操作接口实例
            调试模式: 是否启用详细调试信息
        """
        self.配置路径 = 配置路径
        self.按键适配器 = 按键适配器
        self.日志 = 友好日志系统(调试模式)
        
        self.日志.标题("OpenCV技能循环引擎初始化")
        
        # 加载配置
        self.配置 = self.加载配置()
        
        # 初始化检测器
        self.检测器 = OpenCV技能状态检测器(
            匹配阈值=self.配置["全局配置"]["匹配阈值"],
            调试模式=调试模式
        )
        
        # 初始化图像适配器
        self.图像适配器 = 智能图像适配器()
        
        # 加载技能模板
        self.加载所有模板()
        
        # 引擎状态
        self.当前模式 = 0  # 0=停止, 1=运行
        self.最后技能 = None
        self.执行次数 = 0
        self.开始时间 = time.time()
        
        self.日志.成功("OpenCV技能循环引擎初始化完成")
        self.日志.信息(f"技能数量: {len(self.配置['技能配置'])}")
        self.日志.信息(f"匹配阈值: {self.配置['全局配置']['匹配阈值']}")
        self.日志.信息(f"检测间隔: {self.配置['全局配置']['检测间隔']}秒")
    
    def 加载配置(self) -> dict:
        """加载OpenCV配置文件"""
        self.日志.信息(f"正在加载配置文件: {self.配置路径}")
        
        try:
            with open(self.配置路径, 'r', encoding='utf-8') as f:
                配置 = json.load(f)
            
            self.日志.成功("配置文件加载成功")
            return 配置
        except FileNotFoundError:
            self.日志.错误(f"配置文件不存在: {self.配置路径}")
            self.日志.警告("请检查配置文件路径是否正确")
            raise
        except json.JSONDecodeError as e:
            self.日志.错误(f"配置文件格式错误: {e}")
            self.日志.警告("请检查JSON格式是否正确")
            raise
        except Exception as e:
            self.日志.错误(f"加载配置失败: {e}")
            raise
    
    def 加载所有模板(self):
        """加载所有技能模板"""
        self.日志.信息("开始加载技能模板...")
        
        加载统计 = {"成功": 0, "失败": 0}
        
        # 加载技能模板
        for 技能配置 in self.配置["技能配置"]:
            技能名称 = 技能配置["技能名称"]
            模板路径 = 技能配置["模板路径"]
            
            # 确保模板文件存在
            if not os.path.exists(模板路径):
                self.日志.警告(f"模板文件不存在: {模板路径}")
                加载统计["失败"] += 1
                continue
            
            if self.检测器.加载技能模板(技能名称, 模板路径):
                加载统计["成功"] += 1
            else:
                加载统计["失败"] += 1
        
        # 加载气劲模板
        for 气劲配置 in self.配置["气劲配置"]:
            气劲名称 = 气劲配置["技能名称"]
            模板路径 = 气劲配置["模板路径"]
            
            if not os.path.exists(模板路径):
                self.日志.警告(f"气劲模板文件不存在: {模板路径}")
                加载统计["失败"] += 1
                continue
            
            if self.检测器.加载技能模板(气劲名称, 模板路径):
                加载统计["成功"] += 1
            else:
                加载统计["失败"] += 1
        
        self.日志.信息(f"模板加载完成: 成功 {加载统计['成功']} 个, 失败 {加载统计['失败']} 个")
    
    def 启动循环(self):
        """启动技能循环"""
        self.当前模式 = 1
        self.开始时间 = time.time()
        self.日志.标题("🚀 技能循环已启动")
        self.日志.信息("按 Ctrl+C 停止循环")
    
    def 停止循环(self):
        """停止技能循环"""
        self.当前模式 = 0
        运行时间 = time.time() - self.开始时间
        self.日志.标题("🛑 技能循环已停止")
        self.日志.信息(f"运行时间: {运行时间:.1f}秒")
        self.日志.信息(f"执行次数: {self.执行次数}次")
        
        if self.执行次数 > 0:
            平均间隔 = 运行时间 / self.执行次数 if self.执行次数 > 0 else 0
            self.日志.信息(f"平均间隔: {平均间隔:.2f}秒/次")
    
    def 执行一次循环(self) -> bool:
        """
        执行一次技能循环决策
        
        返回:
            bool: 是否成功执行了技能释放
        """
        if self.当前模式 == 0:
            return False
        
        try:
            self.日志.调试("开始执行技能循环...")
            
            # 获取整个屏幕截图
            屏幕截图 = self.图像适配器.获取屏幕区域((0, 0, 1920, 1080))
            
            # 批量检测所有技能状态
            可用技能 = self.检测器.多技能批量检测(屏幕截图, self.配置["技能配置"])
            
            if not 可用技能:
                self.日志.调试("📭 当前没有可释放的技能")
                return False
            
            # 根据优先级选择技能
            选择技能 = self.根据优先级选择技能(可用技能)
            
            if 选择技能:
                技能名称, 键值 = 选择技能
                
                # 释放技能
                if self.按键适配器.按下并释放(键值):
                    self.最后技能 = 技能名称
                    self.执行次数 += 1
                    self.日志.成功(f"🎯 成功释放技能: {技能名称} (键值: {键值})")
                    return True
                else:
                    self.日志.错误(f"❌ 释放技能失败: {技能名称}")
                    return False
            
            return False
            
        except Exception as e:
            self.日志.错误(f"❌ 技能循环执行错误: {e}")
            return False
    
    def 根据优先级选择技能(self, 可用技能: dict):
        """根据优先级选择要释放的技能"""
        self.日志.调试(f"可用技能列表: {list(可用技能.keys())}")
        
        # 这里可以实现更复杂的优先级逻辑
        # 目前简单返回第一个可用的技能
        for 技能名称, 键值 in 可用技能.items():
            self.日志.调试(f"选择技能: {技能名称}")
            return 技能名称, 键值
        
        return None
    
    def 获取状态(self) -> dict:
        """获取引擎状态信息"""
        运行时间 = time.time() - self.开始时间
        
        return {
            "模式": "运行中" if self.当前模式 == 1 else "已停止",
            "最后技能": self.最后技能,
            "执行次数": self.执行次数,
            "运行时间": f"{运行时间:.1f}秒",
            "技能数量": len(self.配置["技能配置"])
        }


def 演示OpenCV引擎():
    """演示OpenCV技能循环引擎的使用"""
    日志系统 = 友好日志系统()
    日志系统.标题("=== OpenCV技能循环引擎演示 ===")
    
    try:
        # 创建真实按键适配器
        日志系统.信息("1. 初始化按键适配器...")
        按键适配器 = 真实按键适配器(使用库="pynput", 日志系统=日志系统)
        日志系统.成功("✓ 按键适配器初始化完成")
        
        # 配置路径
        配置路径 = os.path.join(os.path.dirname(__file__), "config", "opencv_技能配置.json")
        
        # 创建OpenCV引擎
        日志系统.信息("\n2. 初始化OpenCV引擎...")
        引擎 = OpenCV技能循环引擎(配置路径, 按键适配器, 调试模式=True)
        
        # 启动循环
        日志系统.信息("\n3. 启动技能循环...")
        引擎.启动循环()
        
        # 执行几次循环演示
        日志系统.信息("\n4. 执行技能循环演示...")
        for i in range(5):
            日志系统.信息(f"\n--- 第 {i+1} 次循环 ---")
            
            成功 = 引擎.执行一次循环()
            
            if 成功:
                状态 = 引擎.获取状态()
                日志系统.信息(f"状态: {状态}")
            
            time.sleep(1)  # 1秒间隔
        
        # 停止循环
        日志系统.信息("\n5. 停止技能循环...")
        引擎.停止循环()
        
        日志系统.成功("\n✅ 演示完成！")
        
    except Exception as e:
        日志系统.错误(f"\n❌ 演示过程中出现错误: {e}")
        日志系统.信息("\n💡 可能的原因:")
        日志系统.信息("1. 请确保安装了必要的依赖: pip install opencv-python pynput")
        日志系统.信息("2. 检查配置文件路径是否正确")
        日志系统.信息("3. 确保模板图片文件存在")


def 创建模板指南():
    """创建技能模板的指南"""
    日志系统 = 友好日志系统()
    日志系统.标题("=== 技能模板创建指南 ===")
    
    日志系统.信息("1. 准备模板图片:")
    日志系统.信息("   - 在游戏中使用技能冷却完毕时的状态截图")
    日志系统.信息("   - 只截取技能图标区域，大小建议 50x50 像素")
    日志系统.信息("   - 保存为PNG格式，背景透明")
    
    日志系统.信息("\n2. 模板图片要求:")
    日志系统.信息("   - 文件名: 技能名称.png (如: 青川濯莲.png)")
    日志系统.信息("   - 存放路径: ./templates/ 目录下")
    日志系统.信息("   - 确保图片清晰，特征明显")
    
    日志系统.信息("\n3. 配置检测区域:")
    日志系统.信息("   - 在配置文件中设置每个技能的检测区域")
    日志系统.信息("   - 格式: [x1, y1, x2, y2]")
    日志系统.信息("   - 区域应包含技能图标位置")
    
    日志系统.信息("\n4. 调试技巧:")
    日志系统.信息("   - 使用可视化调试功能查看匹配效果")
    日志系统.信息("   - 调整匹配阈值以获得最佳效果")
    日志系统.信息("   - 测试不同光照条件下的稳定性")


def 系统检查():
    """检查系统环境和依赖"""
    日志系统 = 友好日志系统()
    日志系统.标题("=== 系统环境检查 ===")
    
    try:
        import cv2
        日志系统.成功("✅ OpenCV 安装正常")
        日志系统.信息(f"  版本: {cv2.__version__}")
    except ImportError:
        日志系统.错误("❌ OpenCV 未安装")
        日志系统.警告("   请运行: pip install opencv-python")
    
    try:
        import numpy
        日志系统.成功("✅ NumPy 安装正常")
        日志系统.信息(f"  版本: {numpy.__version__}")
    except ImportError:
        日志系统.错误("❌ NumPy 未安装")
    
    try:
        import pynput
        日志系统.成功("✅ pynput 安装正常")
    except ImportError:
        日志系统.警告("⚠️ pynput 未安装")
        日志系统.信息("   请运行: pip install pynput")
    
    try:
        import pyautogui
        日志系统.成功("✅ pyautogui 安装正常")
    except ImportError:
        日志系统.警告("⚠️ pyautogui 未安装")
        日志系统.信息("   请运行: pip install pyautogui")
    
    # 检查配置文件
    配置路径 = os.path.join(os.path.dirname(__file__), "config", "opencv_技能配置.json")
    if os.path.exists(配置路径):
        日志系统.成功("✅ 配置文件存在")
    else:
        日志系统.错误("❌ 配置文件不存在")
        
    # 检查模板文件夹
    模板路径 = os.path.join(os.path.dirname(__file__), "templates")
    if os.path.exists(模板路径):
        日志系统.成功("✅ 模板文件夹存在")
    else:
        日志系统.警告("⚠️ 模板文件夹不存在")
        日志系统.信息("   请创建: mkdir templates")


if __name__ == "__main__":
    日志系统 = 友好日志系统()
    日志系统.标题("OpenCV技能循环引擎优化版")
    日志系统.信息("使用更友好的日志系统，方便排查问题")
    
    # 显示菜单
    while True:
        日志系统.信息("\n请选择操作:")
        日志系统.信息("1. 运行OpenCV引擎演示")
        日志系统.信息("2. 查看模板创建指南")
        日志系统.信息("3. 系统环境检查")
        日志系统.信息("4. 退出")
        
        选择 = input("请输入选择 (1-4): ").strip()
        
        if 选择 == "1":
            演示OpenCV引擎()
        elif 选择 == "2":
            创建模板指南()
        elif 选择 == "3":
            系统检查()
        elif 选择 == "4":
            日志系统.信息("再见！")
            break
        else:
            日志系统.警告("无效选择，请重新输入")