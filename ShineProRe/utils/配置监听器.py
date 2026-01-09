"""
配置监听器模块
实现配置文件的热重载功能
"""
import time
import threading
import os
from typing import Callable, List
from pathlib import Path
from watchdog.observers import Observer
from watchdog.events import FileSystemEventHandler


class 配置变更处理器(FileSystemEventHandler):
    """配置变更处理器（性能优化版）"""
    
    def __init__(self, 配置路径: str, 变更回调: Callable):
        """
        初始化配置变更处理器
        
        参数:
            配置路径: 配置文件路径
            变更回调: 配置变更时的回调函数
        """
        self.配置路径 = 配置路径
        self.变更回调 = 变更回调
        self.最后修改时间 = self._获取文件修改时间()
        self.最后回调时间 = 0.0
        self.回调间隔 = 1.0  # 优化：最小回调间隔1秒，避免频繁回调
    
    def _获取文件修改时间(self) -> float:
        """获取文件最后修改时间"""
        try:
            return os.path.getmtime(self.配置路径)
        except OSError:
            return 0.0
    
    def on_modified(self, event):
        """文件修改事件处理（优化：添加频率控制）"""
        if event.src_path == self.配置路径:
            当前时间 = time.time()
            
            # 频率控制：避免过于频繁的回调
            if 当前时间 - self.最后回调时间 < self.回调间隔:
                return
            
            当前修改时间 = self._获取文件修改时间()
            if 当前修改时间 > self.最后修改时间:
                self.最后修改时间 = 当前修改时间
                self.最后回调时间 = 当前时间
                
                # 优化：异步执行回调，避免阻塞主线程
                threading.Thread(
                    target=self.变更回调, 
                    args=(self.配置路径,), 
                    daemon=True
                ).start()


class 配置监听器:
    """
    配置监听器
    监听配置文件变化并自动重载
    """
    
    def __init__(self, 配置目录: str = "./config/"):
        """
        初始化配置监听器
        
        参数:
            配置目录: 配置文件目录路径
        """
        self.配置目录 = Path(配置目录)
        self.观察器 = Observer()
        self.监听器列表 = []
        self.回调函数列表 = []
        self.运行中 = False
        self.锁 = threading.Lock()
    
    def 添加监听(self, 配置文件: str, 回调函数: Callable):
        """
        添加配置文件监听
        
        参数:
            配置文件: 配置文件名（相对路径）
            回调函数: 配置变更时的回调函数
        """
        配置路径 = str(self.配置目录 / 配置文件)
        
        if not os.path.exists(配置路径):
            print(f"警告: 配置文件不存在: {配置路径}")
            return
        
        处理器 = 配置变更处理器(配置路径, 回调函数)
        事件处理器 = self.观察器.schedule(处理器, str(self.配置目录), recursive=False)
        
        self.监听器列表.append((配置路径, 事件处理器))
        self.回调函数列表.append(回调函数)
    
    def 开始监听(self):
        """开始监听配置文件变化"""
        with self.锁:
            if not self.运行中:
                self.观察器.start()
                self.运行中 = True
                print("配置监听器已启动")
    
    def 停止监听(self):
        """停止监听配置文件变化"""
        with self.锁:
            if self.运行中:
                self.观察器.stop()
                self.观察器.join()
                self.运行中 = False
                print("配置监听器已停止")
    
    def 手动重载所有配置(self):
        """手动重载所有监听配置"""
        for 回调函数 in self.回调函数列表:
            try:
                回调函数("手动重载")
            except Exception as e:
                print(f"手动重载配置失败: {e}")
    
    def 获取监听状态(self) -> dict:
        """获取监听器状态"""
        return {
            "运行中": self.运行中,
            "监听文件数量": len(self.监听器列表),
            "配置目录": str(self.配置目录)
        }


# 全局配置监听器实例
全局配置监听器 = 配置监听器()


def 热重载配置(配置文件: str):
    """
    装饰器：为函数添加配置热重载功能
    
    参数:
        配置文件: 配置文件名
    """
    def 装饰器(函数):
        def 包装器(*args, **kwargs):
            # 注册配置变更监听
            全局配置监听器.添加监听(配置文件, lambda path: 函数(*args, **kwargs))
            
            # 执行函数
            return 函数(*args, **kwargs)
        return 包装器
    return 装饰器