"""
基于OpenCV模板匹配的技能状态检测器
替代原来的点色检测方法
"""
import cv2
import numpy as np
from typing import Dict, Any, Tuple, Optional
import time
import logging


class 友好日志系统:
    """友好的日志系统，方便排查问题"""
    
    def __init__(self, 调试模式: bool = False):
        self.调试模式 = 调试模式
        self.开始时间 = time.time()
        
        # 设置日志格式
        logging.basicConfig(
            level=logging.INFO if not 调试模式 else logging.DEBUG,
            format='%(asctime)s [%(levelname)s] %(message)s',
            datefmt='%H:%M:%S'
        )
        self.logger = logging.getLogger('OpenCV技能检测器')
    
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
    
    def 开始计时(self, 任务名称: str):
        """开始计时"""
        self.调试(f"开始执行: {任务名称}")
        return time.time()
    
    def 结束计时(self, 开始时间: float, 任务名称: str):
        """结束计时并输出耗时"""
        耗时 = time.time() - 开始时间
        self.调试(f"完成: {任务名称} (耗时: {耗时:.3f}秒)")


class OpenCV技能状态检测器:
    """基于OpenCV模板匹配的技能状态检测器"""
    
    def __init__(self, 匹配阈值: float = 0.8, 调试模式: bool = False):
        """
        初始化检测器
        
        参数:
            匹配阈值: 模板匹配的置信度阈值 (0-1)
            调试模式: 是否启用详细调试信息
        """
        self.匹配阈值 = 匹配阈值
        self.技能模板缓存 = {}  # 缓存技能模板
        self.日志 = 友好日志系统(调试模式)
        
        self.日志.成功(f"OpenCV技能状态检测器初始化完成 (匹配阈值: {匹配阈值})")
    
    def 加载技能模板(self, 技能名称: str, 模板图片路径: str) -> bool:
        """
        加载技能模板图片
        
        参数:
            技能名称: 技能标识
            模板图片路径: 模板图片文件路径
            
        返回:
            bool: 加载是否成功
        """
        self.日志.信息(f"正在加载技能模板: {技能名称}")
        
        try:
            模板 = cv2.imread(模板图片路径, cv2.IMREAD_COLOR)
            if 模板 is None:
                self.日志.错误(f"无法加载技能模板: {技能名称}")
                self.日志.调试(f"模板路径: {模板图片路径}")
                self.日志.警告("请检查文件是否存在且格式正确")
                return False
            
            self.技能模板缓存[技能名称] = 模板
            self.日志.成功(f"成功加载技能模板: {技能名称}")
            self.日志.调试(f"模板尺寸: {模板.shape}, 路径: {模板图片路径}")
            return True
        except Exception as e:
            self.日志.错误(f"加载技能模板失败: {技能名称}")
            self.日志.调试(f"错误详情: {e}")
            return False
    
    def 判断技能可用性(self, 屏幕截图, 技能配置: Dict[str, Any]) -> int:
        """
        使用模板匹配判断技能是否可用
        
        参数:
            屏幕截图: 屏幕截图图像 (numpy数组)
            技能配置: 技能配置字典
                - 技能名称: 技能标识
                - 检测区域: (x1, y1, x2, y2) 检测区域坐标
                - 模板路径: 技能模板图片路径
                - 键值: 技能按键值
            
        返回:
            int: 技能键值 (可释放时) 或 0 (不可释放时)
        """
        技能名称 = 技能配置.get("技能名称")
        检测区域 = 技能配置.get("检测区域")
        模板路径 = 技能配置.get("模板路径")
        键值 = 技能配置.get("键值", 0)
        
        # 检查配置完整性
        if not all([技能名称, 检测区域, 模板路径]):
            self.日志.错误(f"技能配置不完整: {技能名称}")
            self.日志.调试(f"技能名称: {技能名称}, 检测区域: {检测区域}, 模板路径: {模板路径}")
            return 0
        
        # 确保模板已加载
        if 技能名称 not in self.技能模板缓存:
            self.日志.信息(f"首次加载技能模板: {技能名称}")
            if not self.加载技能模板(技能名称, 模板路径):
                return 0
        else:
            self.日志.调试(f"使用缓存模板: {技能名称}")
        
        # 提取检测区域
        try:
            x1, y1, x2, y2 = 检测区域
            区域图像 = 屏幕截图[y1:y2, x1:x2]
            
            if 区域图像.size == 0:
                self.日志.错误(f"检测区域无效: {检测区域}")
                self.日志.调试(f"屏幕截图尺寸: {屏幕截图.shape}")
                self.日志.警告("请检查检测区域坐标是否超出屏幕范围")
                return 0
                
            self.日志.调试(f"成功提取检测区域: {检测区域}, 区域图像尺寸: {区域图像.shape}")
        except Exception as e:
            self.日志.错误(f"提取检测区域失败: {技能名称}")
            self.日志.调试(f"错误详情: {e}, 检测区域: {检测区域}")
            return 0
        
        # 获取模板
        模板 = self.技能模板缓存[技能名称]
        
        # 模板匹配
        匹配开始时间 = self.日志.开始计时("模板匹配")
        匹配结果 = cv2.matchTemplate(区域图像, 模板, cv2.TM_CCOEFF_NORMED)
        _, 最大置信度, _, _ = cv2.minMaxLoc(匹配结果)
        self.日志.结束计时(匹配开始时间, "模板匹配")
        
        # 详细匹配结果
        状态描述 = "可用" if 最大置信度 >= self.匹配阈值 else "不可用"
        self.日志.信息(f"技能检测: {技能名称} | 置信度: {最大置信度:.3f} | 状态: {状态描述}")
        
        # 判断是否匹配成功
        if 最大置信度 >= self.匹配阈值:
            self.日志.成功(f"技能 {技能名称} 可以释放 (键值: {键值})")
            return 键值
        else:
            self.日志.调试(f"技能 {技能名称} 当前不可用")
            self.日志.调试(f"置信度 {最大置信度:.3f} 低于阈值 {self.匹配阈值}")
            return 0
    
    def 多技能批量检测(self, 屏幕截图, 技能配置列表: list) -> Dict[str, int]:
        """
        批量检测多个技能状态
        
        参数:
            屏幕截图: 屏幕截图图像
            技能配置列表: 技能配置字典列表
            
        返回:
            dict: {技能名称: 键值} 可释放的技能
        """
        self.日志.信息(f"开始批量检测 {len(技能配置列表)} 个技能")
        批量开始时间 = self.日志.开始计时("批量技能检测")
        
        可用技能 = {}
        检测统计 = {"总数": len(技能配置列表), "可用": 0, "不可用": 0}
        
        for 序号, 技能配置 in enumerate(技能配置列表, 1):
            技能名称 = 技能配置.get("技能名称")
            self.日志.调试(f"检测第 {序号}/{检测统计['总数']} 个技能: {技能名称}")
            
            键值 = self.判断技能可用性(屏幕截图, 技能配置)
            if 键值 > 0:
                可用技能[技能名称] = 键值
                检测统计["可用"] += 1
            else:
                检测统计["不可用"] += 1
        
        self.日志.结束计时(批量开始时间, "批量技能检测")
        
        # 输出检测结果统计
        self.日志.信息(f"批量检测完成: 共检测 {检测统计['总数']} 个技能")
        self.日志.信息(f"可用技能: {检测统计['可用']} 个 | 不可用技能: {检测统计['不可用']} 个")
        
        if 可用技能:
            self.日志.成功(f"发现 {len(可用技能)} 个可用技能: {list(可用技能.keys())}")
        else:
            self.日志.警告("当前没有可用的技能")
        
        return 可用技能
    
    def 可视化调试(self, 屏幕截图, 技能配置: Dict[str, Any]) -> Optional[np.ndarray]:
        """
        可视化调试功能，显示匹配结果
        
        参数:
            屏幕截图: 原始屏幕截图
            技能配置: 技能配置
            
        返回:
            numpy数组: 带标记的图像
        """
        技能名称 = 技能配置.get("技能名称")
        检测区域 = 技能配置.get("检测区域")
        
        if 技能名称 not in self.技能模板缓存:
            return None
        
        # 复制图像用于标记
        标记图像 = 屏幕截图.copy()
        
        # 提取检测区域
        x1, y1, x2, y2 = 检测区域
        区域图像 = 屏幕截图[y1:y2, x1:x2]
        
        # 模板匹配
        模板 = self.技能模板缓存[技能名称]
        匹配结果 = cv2.matchTemplate(区域图像, 模板, cv2.TM_CCOEFF_NORMED)
        _, 最大置信度, 最大位置, _ = cv2.minMaxLoc(匹配结果)
        
        # 绘制检测区域
        cv2.rectangle(标记图像, (x1, y1), (x2, y2), (0, 255, 0), 2)
        
        # 绘制匹配结果
        if 最大置信度 >= self.匹配阈值:
            # 匹配成功，绘制绿色框
            匹配位置 = (x1 + 最大位置[0], y1 + 最大位置[1])
            cv2.rectangle(标记图像, 匹配位置, 
                         (匹配位置[0] + 模板.shape[1], 匹配位置[1] + 模板.shape[0]), 
                         (0, 255, 0), 3)
            cv2.putText(标记图像, f"{技能名称}: {最大置信度:.3f}", 
                       (x1, y1 - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 255, 0), 2)
        else:
            # 匹配失败，绘制红色框
            cv2.putText(标记图像, f"{技能名称}: {最大置信度:.3f}", 
                       (x1, y1 - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 0, 255), 2)
        
        return 标记图像


class 智能图像适配器:
    """基于OpenCV的图像适配器实现"""
    
    def __init__(self):
        self.检测器 = OpenCV技能状态检测器()
    
    def 获取屏幕区域(self, 区域: tuple) -> np.ndarray:
        """
        获取屏幕指定区域的截图
        
        参数:
            区域: (x1, y1, x2, y2) 截图区域
            
        返回:
            numpy数组: 截图图像
        """
        # 这里需要集成具体的截图库
        # 例如: pyautogui, mss, PIL等
        
        # 临时返回一个模拟图像
        # 实际使用时需要替换为真实的截图代码
        x1, y1, x2, y2 = 区域
        宽度 = x2 - x1
        高度 = y2 - y1
        
        # 创建一个模拟的屏幕截图
        # 实际使用时替换为: 
        # import pyautogui
        # screenshot = pyautogui.screenshot(region=区域)
        # return cv2.cvtColor(np.array(screenshot), cv2.COLOR_RGB2BGR)
        
        模拟图像 = np.random.randint(0, 255, (高度, 宽度, 3), dtype=np.uint8)
        return 模拟图像
    
    def 设置技能模板(self, 技能名称: str, 模板路径: str) -> bool:
        """设置技能模板"""
        return self.检测器.加载技能模板(技能名称, 模板路径)
    
    def 判断技能状态(self, 技能配置: Dict[str, Any]) -> int:
        """判断单个技能状态"""
        屏幕截图 = self.获取屏幕区域(技能配置.get("检测区域"))
        return self.检测器.判断技能可用性(屏幕截图, 技能配置)


# 使用示例
if __name__ == "__main__":
    # 创建检测器
    检测器 = OpenCV技能状态检测器(匹配阈值=0.8)
    
    # 示例技能配置
    示例技能配置 = {
        "技能名称": "青川濯莲",
        "检测区域": (100, 100, 200, 200),  # (x1, y1, x2, y2)
        "模板路径": "./templates/青川濯莲.png",
        "键值": 81  # Q键
    }
    
    # 加载模板
    检测器.加载技能模板("青川濯莲", "./templates/青川濯莲.png")
    
    print("OpenCV技能状态检测器初始化完成")