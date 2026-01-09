"""
游戏脚本桌面 UI - 现代暗色重制版 (最终优化版：居中、统一风格、增强置顶、修复暂停/恢复按钮)
"""
import sys
import threading
from PyQt6.QtWidgets import (QApplication, QMainWindow, QWidget, QVBoxLayout, 
                             QHBoxLayout, QLabel, QPushButton, QFrame, QSystemTrayIcon, 
                             QMenu, QMessageBox, QGridLayout, QGraphicsDropShadowEffect)
from PyQt6.QtGui import QAction, QFont, QColor, QCursor
from PyQt6.QtCore import Qt, QTimer, pyqtSignal, QPoint

class GameScriptUI(QMainWindow):
    """现代暗黑风格游戏脚本界面"""
    
    # 信号定义
    status_update_signal = pyqtSignal(dict)
    start_signal = pyqtSignal()
    stop_signal = pyqtSignal()
    pause_signal = pyqtSignal()
    exit_signal = pyqtSignal()
    
    def __init__(self, main_engine):
        super().__init__()
        
        self.engine = main_engine        
        self.running_status = "已停止"   
        self.ui_visible = True
        self.tray_icon = None
        self.drag_pos = None             
        
        # 1. 设置窗口基础属性 (无边框 + 透明背景 + 增强置顶)
        self.setWindowFlags(
            Qt.WindowType.WindowStaysOnTopHint | 
            Qt.WindowType.FramelessWindowHint |
            Qt.WindowType.Tool 
        )
        self.setAttribute(Qt.WidgetAttribute.WA_TranslucentBackground)
        self.setFixedSize(380, 450)
        self.setWindowTitle("游戏自动化助手")
        
        # 2. 初始化 UI
        self.init_ui()                   
        self.register_hotkeys()
        self.create_system_tray()
        self.connect_signals()           
        self.start_ui_updater()          
        self.move_to_center()            

    def init_ui(self):
        """构建现代风格 UI"""
        self.main_container = QFrame()   
        self.main_container.setObjectName("MainFrame")
        self.setCentralWidget(self.main_container)
        
        # 添加阴影效果
        shadow = QGraphicsDropShadowEffect(self)
        shadow.setBlurRadius(20)
        shadow.setXOffset(0)
        shadow.setYOffset(0)
        shadow.setColor(QColor(0, 0, 0, 150))
        self.main_container.setGraphicsEffect(shadow)

        # 主布局
        main_layout = QVBoxLayout(self.main_container)
        main_layout.setContentsMargins(20, 20, 20, 20)
        main_layout.setSpacing(15)

        # --- 顶部标题栏 (自定义) ---
        title_bar_layout = QHBoxLayout()
        
        self.title_label = QLabel("⚡ 游戏自动化助手")
        self.title_label.setObjectName("TitleLabel")
        
        self.minimize_button = QPushButton("－")
        self.minimize_button.setObjectName("WinBtn")
        self.minimize_button.clicked.connect(self.hide_to_tray) 
        self.minimize_button.setFixedSize(30, 30)

        title_bar_layout.addWidget(self.title_label)
        title_bar_layout.addStretch()
        title_bar_layout.addWidget(self.minimize_button)
        
        main_layout.addLayout(title_bar_layout)

        # --- 状态核心展示区 ---
        self.status_box = QFrame()
        self.status_box.setObjectName("StatusBox")
        status_layout = QVBoxLayout(self.status_box)
        
        self.status_big_text = QLabel("🔴 已停止")
        self.status_big_text.setAlignment(Qt.AlignmentFlag.AlignCenter)
        self.status_big_text.setObjectName("StatusBigText_Stopped") 
        
        self.mode_text = QLabel("当前模式: 等待指令...")
        self.mode_text.setAlignment(Qt.AlignmentFlag.AlignCenter)
        self.mode_text.setObjectName("ModeText")
        
        status_layout.addWidget(self.status_big_text)
        status_layout.addWidget(self.mode_text)
        
        main_layout.addWidget(self.status_box)

        # --- 数据仪表盘 (网格布局) ---
        dashboard_layout = QGridLayout()
        dashboard_layout.setSpacing(10)

        # 辅助函数：创建数据卡片
        def create_data_card(title, default_value):
            card = QFrame()
            card.setObjectName("DataCard")
            card_layout = QVBoxLayout(card)
            card_layout.setContentsMargins(10, 10, 10, 10)
            
            value_label = QLabel(default_value)
            value_label.setObjectName("DataValue")
            value_label.setAlignment(Qt.AlignmentFlag.AlignCenter)
            
            title_label = QLabel(title)
            title_label.setObjectName("DataTitle")
            title_label.setAlignment(Qt.AlignmentFlag.AlignCenter)
            
            card_layout.addWidget(value_label)
            card_layout.addWidget(title_label)
            return card, value_label

        # 创建三个指标卡片
        card1, self.response_time_value = create_data_card("响应 (秒)", "0.00")
        card2, self.execution_count_value = create_data_card("执行次数", "0")
        card3, self.success_rate_value = create_data_card("成功率", "--%")

        dashboard_layout.addWidget(card1, 0, 0)
        dashboard_layout.addWidget(card2, 0, 1)
        dashboard_layout.addWidget(card3, 0, 2)
        
        main_layout.addLayout(dashboard_layout)

        # --- 底部控制区 ---
        control_layout = QHBoxLayout()
        control_layout.setSpacing(10)

        self.main_switch_button = QPushButton("启动脚本")
        self.main_switch_button.setObjectName("BtnStart")
        self.main_switch_button.setFixedHeight(45)
        self.main_switch_button.setCursor(QCursor(Qt.CursorShape.PointingHandCursor))
        self.main_switch_button.clicked.connect(self.toggle_running_state) 

        self.pause_button = QPushButton("暂停")
        self.pause_button.setObjectName("BtnPause")
        # 修复点：添加属性用于CSS判断暂停状态
        self.pause_button.setProperty("pausedMode", False) 
        self.pause_button.setFixedHeight(45)
        self.pause_button.setCursor(QCursor(Qt.CursorShape.PointingHandCursor))
        self.pause_button.clicked.connect(self.pause_script) 

        self.exit_button = QPushButton("退出")
        self.exit_button.setObjectName("BtnExit")
        self.exit_button.setFixedSize(45, 45) 
        self.exit_button.setCursor(QCursor(Qt.CursorShape.PointingHandCursor))
        self.exit_button.clicked.connect(self.safe_exit) 

        control_layout.addWidget(self.main_switch_button, 2) 
        control_layout.addWidget(self.pause_button, 1)
        control_layout.addWidget(self.exit_button, 0)

        main_layout.addLayout(control_layout)

        # --- 底部提示 ---
        footer_tip = QLabel("快捷键: Ctrl+Shift+H (隐藏/显示)")
        footer_tip.setObjectName("FooterTip")
        footer_tip.setAlignment(Qt.AlignmentFlag.AlignCenter)
        main_layout.addWidget(footer_tip)

        # 加载样式表
        self.load_styles()

    def load_styles(self): 
        """设置 CSS 样式 (暗色赛博朋克风)"""
        self.setStyleSheet("""
            /* 全局字体与背景 */
            QWidget {
                font-family: 'Microsoft YaHei', 'Segoe UI', sans-serif;
            }
            
            /* 主容器：深色圆角背景 */
            QFrame#MainFrame {
                background-color: #1E1E2E; 
                border-radius: 16px;
                border: 1px solid #303040;
            }

            /* 标题栏 */
            QLabel#TitleLabel {
                color: #FFFFFF;
                font-size: 16px;
                font-weight: bold;
            }
            QPushButton#WinBtn {
                background-color: transparent;
                color: #8888AA;
                border: none;
                font-size: 18px;
                font-weight: bold;
            }
            QPushButton#WinBtn:hover {
                color: #FFFFFF;
                background-color: #303040;
                border-radius: 15px;
            }

            /* 状态显示区 */
            QFrame#StatusBox {
                background-color: #262636;
                border-radius: 10px;
                padding: 10px;
            }
            QLabel#StatusBigText_Stopped {
                color: #FF5555; /* 红色 */
                font-size: 24px;
                font-weight: 900;
            }
            QLabel#StatusBigText_Running {
                color: #50FA7B; /* 亮绿色 */
                font-size: 24px;
                font-weight: 900;
            }
            QLabel#StatusBigText_Paused {
                color: #F1FA8C; /* 黄色 */
                font-size: 24px;
                font-weight: 900;
            }
            QLabel#ModeText {
                color: #8888AA;
                font-size: 12px;
                margin-top: 5px;
            }

            /* 数据卡片 */
            QFrame#DataCard {
                background-color: #262636;
                border-radius: 8px;
                border: 1px solid #323246;
            }
            QLabel#DataValue {
                color: #8BE9FD; /* 青色 */
                font-size: 18px;
                font-weight: bold;
            }
            QLabel#DataTitle {
                color: #6272A4;
                font-size: 11px;
            }

            /* 按钮样式 */
            QPushButton {
                border-radius: 8px;
                font-weight: bold;
                font-size: 14px;
                border: none;
            }
            
            /* 主开关按钮 (启动/停止) */
            QPushButton#BtnStart {
                background-color: #50FA7B;
                color: #282A36;
            }
            QPushButton#BtnStart:hover {
                background-color: #69FF94;
            }
            QPushButton#BtnStart[stopMode="true"] {
                background-color: #FF5555; /* 停止按钮显红色 */
                color: white;
            }
            
            /* 暂停/恢复按钮 */
            QPushButton#BtnPause {
                background-color: #44475A; /* 默认灰色 */
                color: #F8F8F2;
                border: 1px solid #6272A4;
            }
            QPushButton#BtnPause:hover {
                background-color: #6272A4;
            }
            /* 暂停模式下的样式 (显示“恢复”) */
            QPushButton#BtnPause[pausedMode="true"] {
                background-color: #F1FA8C; /* 亮黄色 */
                color: #282A36;
                border: 1px solid #F1FA8C;
            }
            
            /* 退出按钮 */
            QPushButton#BtnExit {
                background-color: #262636;
                color: #FF5555;
                border: 1px solid #FF5555;
            }
            QPushButton#BtnExit:hover {
                background-color: #FF5555;
                color: white;
            }

            /* 底部提示 */
            QLabel#FooterTip {
                color: #44475A;
                font-size: 10px;
            }
        """)

    def get_message_box_style(self):
        """生成 QMessageBox 的暗色主题样式"""
        return """
            QMessageBox {
                background-color: #1E1E2E; 
                color: #FFFFFF;
                font-family: 'Microsoft YaHei', 'Segoe UI', sans-serif;
            }
            QLabel {
                color: #F8F8F2; 
                font-size: 14px;
            }
            QPushButton {
                background-color: #44475A; 
                color: #F8F8F2;
                border: 1px solid #6272A4;
                border-radius: 5px;
                padding: 5px 15px;
                min-width: 80px;
                font-weight: bold;
            }
            QPushButton:hover {
                background-color: #6272A4;
            }
            QPushButton[text="确定退出"] {
                background-color: #FF5555; 
                color: white;
                border: none;
            }
            QPushButton[text="确定退出"]:hover {
                background-color: #CC4444;
            }
        """

    def update_ui_status(self, engine_status): 
        """更新 UI 数据"""
        try:
            is_running = engine_status.get('running', False)
            is_paused = engine_status.get('paused', False)
            
            # --- 1. 更新大状态文字和颜色 ---
            if is_running and not is_paused:
                self.running_status = "已运行"
                self.status_big_text.setText("🟢 正在运行")
                self.status_big_text.setObjectName("StatusBigText_Running")
                
                # 按钮变为停止模式
                self.main_switch_button.setText("停止运行")
                self.main_switch_button.setProperty("stopMode", True)
                
            elif is_running and is_paused:
                self.running_status = "已暂停"
                self.status_big_text.setText("🟡 已暂停")
                self.status_big_text.setObjectName("StatusBigText_Paused")
                
            else:
                self.running_status = "已停止"
                self.status_big_text.setText("🔴 已停止")
                self.status_big_text.setObjectName("StatusBigText_Stopped")
                
                # 按钮变为启动模式
                self.main_switch_button.setText("立即启动")
                self.main_switch_button.setProperty("stopMode", False)

            # --- 2. 修复点：更新暂停/恢复按钮文本和样式 ---
            if is_running:
                self.pause_button.setEnabled(True) # 运行时才能暂停/恢复
                if is_paused:
                    self.pause_button.setText("恢复")
                    self.pause_button.setProperty("pausedMode", True)
                else:
                    self.pause_button.setText("暂停")
                    self.pause_button.setProperty("pausedMode", False)
            else:
                self.pause_button.setText("暂停") # 停止时默认显示“暂停”
                self.pause_button.setProperty("pausedMode", False)
                self.pause_button.setEnabled(False) # 停止时禁用暂停键
                
            
            # 刷新所有需要动态改变样式的控件
            self.status_big_text.style().unpolish(self.status_big_text)
            self.status_big_text.style().polish(self.status_big_text)
            self.main_switch_button.style().unpolish(self.main_switch_button)
            self.main_switch_button.style().polish(self.main_switch_button)
            self.pause_button.style().unpolish(self.pause_button)
            self.pause_button.style().polish(self.pause_button)


            # --- 3. 更新仪表盘和托盘信息 ---
            self.mode_text.setText(f"当前模式: {engine_status.get('mode', '等待中...')}")
            self.response_time_value.setText(f"{engine_status.get('avg_response_time', 0):.2f}s")
            self.execution_count_value.setText(f"{engine_status.get('execution_count', 0)}")
            self.success_rate_value.setText(f"{engine_status.get('success_rate', 0):.0f}%")

            status_text = "【游戏自动化助手】\n"
            status_text += f"当前状态: {self.running_status}\n"
            status_text += f"运行模式: {engine_status.get('mode', '未知')}\n"
            status_text += f"总执行次数: {engine_status.get('execution_count', 0)}\n"
            status_text += "\n----------------------\n"
            status_text += "单击图标：显示/隐藏主界面"
            
            if self.tray_icon:
                 self.tray_icon.setToolTip(status_text)

        except Exception as e:
            print(f"UI更新错误: {e}")

    # --- 无边框窗口拖拽逻辑 ---
    def mousePressEvent(self, event):
        if event.button() == Qt.MouseButton.LeftButton:
            self.drag_pos = event.globalPosition().toPoint() - self.frameGeometry().topLeft()
            event.accept()

    def mouseMoveEvent(self, event):
        if event.buttons() == Qt.MouseButton.LeftButton and self.drag_pos:
            self.move(event.globalPosition().toPoint() - self.drag_pos)
            event.accept()

    def move_to_center(self): 
        """将窗口移动到屏幕中央"""
        screen = QApplication.primaryScreen()
        rect = screen.availableGeometry()
        
        center_x = (rect.width() - self.width()) // 2
        center_y = (rect.height() - self.height()) // 2
        
        self.move(center_x, center_y)

    # --- 逻辑控制代码 ---
    
    def connect_signals(self): 
        self.status_update_signal.connect(self.update_ui_status) 
        self.start_signal.connect(self.start_script)
        self.stop_signal.connect(self.stop_script)
        self.pause_signal.connect(self.pause_script)
        self.exit_signal.connect(self.safe_exit)

    def register_hotkeys(self):
        try:
            import keyboard
            keyboard.add_hotkey('ctrl+shift+p', lambda: self.pause_signal.emit())
            keyboard.add_hotkey('ctrl+shift+h', self.toggle_ui_visibility) 
            keyboard.add_hotkey('ctrl+shift+q', lambda: self.exit_signal.emit())
        except ImportError:
            pass
        except Exception:
            pass

    def create_system_tray(self):
        self.tray_icon = QSystemTrayIcon(self)
        
        self.tray_icon.setToolTip("游戏自动化助手：正在初始化...")

        tray_menu = QMenu()
        tray_menu.addAction(QAction("显示界面", self, triggered=self.show_ui))
        tray_menu.addAction(QAction("启动", self, triggered=self.start_script))
        tray_menu.addAction(QAction("暂停/恢复", self, triggered=self.pause_script)) # 更改菜单文本
        tray_menu.addSeparator()
        tray_menu.addAction(QAction("退出", self, triggered=self.safe_exit))
        
        self.tray_icon.setContextMenu(tray_menu)
        self.tray_icon.setIcon(QApplication.style().standardIcon(QApplication.style().StandardPixmap.SP_ComputerIcon))
        self.tray_icon.show()
        self.tray_icon.activated.connect(self.tray_icon_activated) 

    def tray_icon_activated(self, reason): 
        if reason == QSystemTrayIcon.ActivationReason.Trigger or \
           reason == QSystemTrayIcon.ActivationReason.DoubleClick:
            self.toggle_ui_visibility() 

    def start_ui_updater(self): 
        self.update_timer = QTimer()
        self.update_timer.timeout.connect(self.timed_status_update) 
        self.update_timer.start(500)

    def timed_status_update(self): 
        try:
            engine_status = self.engine.get_running_status() if hasattr(self.engine, 'get_running_status') else {}
            self.status_update_signal.emit(engine_status)
        except Exception:
            pass

    def toggle_running_state(self): 
        if self.running_status == "已停止":
            self.start_script()
        else:
            self.stop_script()

    def start_script(self): 
        if hasattr(self.engine, 'start'): self.engine.start()

    def stop_script(self): 
        if hasattr(self.engine, 'stop'): self.engine.stop()

    def pause_script(self): 
        # 实际操作是切换暂停状态
        if hasattr(self.engine, 'pause'): self.engine.pause()

    def toggle_ui_visibility(self): 
        if self.ui_visible: self.hide_ui()
        else: self.show_ui()

    def show_ui(self): 
        """显示并强制置顶窗口"""
        self.show()
        self.raise_()
        self.activateWindow()
        self.ui_visible = True

    def hide_ui(self): 
        self.hide()
        self.ui_visible = False

    def hide_to_tray(self): 
        self.hide_ui()
        self.tray_icon.showMessage("游戏脚本", "已最小化，双击或单击托盘图标恢复", QSystemTrayIcon.MessageIcon.Information, 1000)

    def safe_exit(self): 
        """使用自定义风格的退出确认框"""
        
        msg_box = QMessageBox(self)
        msg_box.setWindowTitle("确认退出")
        msg_box.setText("你确定要退出游戏脚本吗？")
        msg_box.setIcon(QMessageBox.Icon.Question)
        
        msg_box.setStyleSheet(self.get_message_box_style()) 
        
        yes_button = msg_box.addButton("确定退出", QMessageBox.ButtonRole.YesRole)
        no_button = msg_box.addButton("取消", QMessageBox.ButtonRole.NoRole)
        msg_box.setDefaultButton(no_button) 

        msg_box.exec()
        
        if msg_box.clickedButton() == yes_button:
            if hasattr(self.engine, 'stop'): self.engine.stop()
            QApplication.quit()
    
    def closeEvent(self, event):
        event.ignore()
        self.hide_to_tray()

# --- 测试引擎 ---
class TestEngine:
    def __init__(self):
        self.running = False
        self.paused = False
        self.execution_count = 0
        self.mode = "智能识别"
        self.avg_response_time = 0.12
        self.success_rate = 95.5

    def start(self):
        self.running = True
        self.paused = False
        print("Engine: Start")

    def stop(self):
        self.running = False
        self.paused = False # 停止后重置暂停状态
        print("Engine: Stop")

    def pause(self):
        # 切换暂停状态
        self.paused = not self.paused
        print(f"Engine: Paused: {self.paused}")

    def get_running_status(self):
        if self.running and not self.paused:
            self.execution_count += 1
            self.avg_response_time = 0.1 + (self.execution_count % 10) * 0.01
            self.success_rate = 90 + (self.execution_count % 10) * 0.8
        
        return {
            'running': self.running,
            'paused': self.paused,
            'mode': self.mode,
            'execution_count': self.execution_count,
            'avg_response_time': self.avg_response_time,
            'success_rate': self.success_rate
        }

def main():
    QApplication.setHighDpiScaleFactorRoundingPolicy(Qt.HighDpiScaleFactorRoundingPolicy.PassThrough)
    
    app = QApplication(sys.argv)
    app.setStyle("Fusion")
    
    engine = TestEngine()
    ui = GameScriptUI(engine)
    ui.show_ui()
    
    sys.exit(app.exec())

if __name__ == "__main__":
    main()