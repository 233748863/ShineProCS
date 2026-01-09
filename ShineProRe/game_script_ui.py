"""
游戏脚本桌面 UI - 现代暗色重制版 (已修复 Unicode 编码错误)
"""
import sys
import threading
from PyQt6.QtWidgets import (QApplication, QMainWindow, QWidget, QVBoxLayout, 
                             QHBoxLayout, QLabel, QPushButton, QFrame, QSystemTrayIcon, 
                             QMenu, QMessageBox, QGridLayout, QGraphicsDropShadowEffect)
from PyQt6.QtGui import QAction, QFont, QColor, QCursor
from PyQt6.QtCore import Qt, QTimer, pyqtSignal, QPoint

class GameScriptUI(QMainWindow): # 修复：类名改为英文
    """现代暗黑风格游戏脚本界面"""
    
    # 保持原有的信号定义不变
    status_update_signal = pyqtSignal(dict)
    start_signal = pyqtSignal()
    stop_signal = pyqtSignal()
    pause_signal = pyqtSignal()
    exit_signal = pyqtSignal()
    
    def __init__(self, main_engine):
        super().__init__()
        
        self.engine = main_engine        # 修复：变量名改为英文
        self.running_status = "已停止"   # 状态值保持中文
        self.ui_visible = True
        self.tray_icon = None
        self.drag_pos = None             # 修复：变量名改为英文
        
        # 1. 设置窗口基础属性 (无边框 + 透明背景)
        self.setWindowFlags(Qt.WindowType.WindowStaysOnTopHint | Qt.WindowType.FramelessWindowHint)
        self.setAttribute(Qt.WidgetAttribute.WA_TranslucentBackground)
        self.setFixedSize(380, 450)
        self.setWindowTitle("游戏自动化助手")
        
        # 2. 初始化 UI
        self.init_ui()                   # 修复：方法名改为英文
        self.register_hotkeys()
        self.create_system_tray()
        self.connect_signals()           # 修复：方法名改为英文
        self.start_ui_updater()          # 修复：方法名改为英文
        self.move_to_corner()            # 修复：方法名改为英文

    def init_ui(self):
        """构建现代风格 UI"""
        self.main_container = QFrame()   # 修复：变量名改为英文
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
        self.minimize_button.clicked.connect(self.hide_to_tray) # 修复：方法名改为英文
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
        self.status_big_text.setObjectName("StatusBigText_Stopped") # 初始样式
        
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
        self.main_switch_button.clicked.connect(self.toggle_running_state) # 修复：方法名改为英文

        self.pause_button = QPushButton("暂停")
        self.pause_button.setObjectName("BtnPause")
        self.pause_button.setFixedHeight(45)
        self.pause_button.setCursor(QCursor(Qt.CursorShape.PointingHandCursor))
        self.pause_button.clicked.connect(self.pause_script) # 修复：方法名改为英文

        self.exit_button = QPushButton("退出")
        self.exit_button.setObjectName("BtnExit")
        self.exit_button.setFixedSize(45, 45) # 方形按钮
        self.exit_button.setCursor(QCursor(Qt.CursorShape.PointingHandCursor))
        self.exit_button.clicked.connect(self.safe_exit) # 修复：方法名改为英文

        control_layout.addWidget(self.main_switch_button, 2) # 占据更多比例
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

    def load_styles(self): # 修复：方法名改为英文
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
            
            /* 启动按钮 (绿色渐变) */
            QPushButton#BtnStart {
                background-color: #50FA7B;
                color: #282A36;
            }
            QPushButton#BtnStart:hover {
                background-color: #69FF94;
            }
            QPushButton#BtnStart[stopMode="true"] {
                background-color: #FF5555; /* 变成停止按钮时显红色 */
                color: white;
            }
            
            /* 暂停按钮 */
            QPushButton#BtnPause {
                background-color: #44475A;
                color: #F8F8F2;
                border: 1px solid #6272A4;
            }
            QPushButton#BtnPause:hover {
                background-color: #6272A4;
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

    def update_ui_status(self, engine_status): # 修复：方法名改为英文
        """更新 UI 数据"""
        try:
            is_running = engine_status.get('running', False)
            is_paused = engine_status.get('paused', False)
            
            # 1. 更新大状态文字和颜色
            if is_running and not is_paused:
                self.running_status = "已运行"
                self.status_big_text.setText("🟢 正在运行")
                self.status_big_text.setObjectName("StatusBigText_Running")
                
                # 按钮变为停止模式
                self.main_switch_button.setText("停止运行")
                self.main_switch_button.setProperty("stopMode", True)
                self.main_switch_button.style().unpolish(self.main_switch_button)
                self.main_switch_button.style().polish(self.main_switch_button)
                
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
                self.main_switch_button.style().unpolish(self.main_switch_button)
                self.main_switch_button.style().polish(self.main_switch_button)
            
            # 刷新样式
            self.status_big_text.style().unpolish(self.status_big_text)
            self.status_big_text.style().polish(self.status_big_text)

            # 2. 更新数据
            self.mode_text.setText(f"当前模式: {engine_status.get('mode', '等待中...')}")
            self.response_time_value.setText(f"{engine_status.get('avg_response_time', 0):.2f}s")
            self.execution_count_value.setText(f"{engine_status.get('execution_count', 0)}")
            self.success_rate_value.setText(f"{engine_status.get('success_rate', 0):.0f}%")

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

    def move_to_corner(self): # 修复：方法名改为英文
        screen = QApplication.primaryScreen()
        rect = screen.availableGeometry()
        self.move(rect.width() - self.width() - 30, 60)

    # --- 以下为原有的逻辑控制代码，内部标识符已修复 ---
    
    def connect_signals(self): # 修复：方法名改为英文
        self.status_update_signal.connect(self.update_ui_status) # 修复：方法名改为英文
        self.start_signal.connect(self.start_script)
        self.stop_signal.connect(self.stop_script)
        self.pause_signal.connect(self.pause_script)
        self.exit_signal.connect(self.safe_exit)

    def register_hotkeys(self):
        try:
            import keyboard
            keyboard.add_hotkey('ctrl+shift+p', lambda: self.pause_signal.emit())
            keyboard.add_hotkey('ctrl+shift+h', self.toggle_ui_visibility) # 修复：方法名改为英文
            keyboard.add_hotkey('ctrl+shift+q', lambda: self.exit_signal.emit())
            print("热键注册成功")
        except ImportError:
            print("警告: 缺少 keyboard 库")
        except Exception:
            pass

    def create_system_tray(self):
        self.tray_icon = QSystemTrayIcon(self)
        tray_menu = QMenu()
        
        # 简单动作
        tray_menu.addAction(QAction("显示界面", self, triggered=self.show_ui))
        tray_menu.addAction(QAction("启动", self, triggered=self.start_script))
        tray_menu.addAction(QAction("暂停", self, triggered=self.pause_script))
        tray_menu.addSeparator()
        tray_menu.addAction(QAction("退出", self, triggered=self.safe_exit))
        
        self.tray_icon.setContextMenu(tray_menu)
        self.tray_icon.setIcon(QApplication.style().standardIcon(QApplication.style().StandardPixmap.SP_ComputerIcon))
        self.tray_icon.show()
        self.tray_icon.activated.connect(self.tray_icon_activated) # 修复：方法名改为英文

    def tray_icon_activated(self, reason): # 修复：方法名改为英文
        if reason == QSystemTrayIcon.ActivationReason.DoubleClick:
            self.toggle_ui_visibility() # 修复：方法名改为英文

    def start_ui_updater(self): # 修复：方法名改为英文
        self.update_timer = QTimer()
        self.update_timer.timeout.connect(self.timed_status_update) # 修复：方法名改为英文
        self.update_timer.start(500)

    def timed_status_update(self): # 修复：方法名改为英文
        try:
            engine_status = self.engine.get_running_status() if hasattr(self.engine, 'get_running_status') else {}
            self.status_update_signal.emit(engine_status)
        except Exception:
            pass

    def toggle_running_state(self): # 修复：方法名改为英文
        if self.running_status == "已停止":
            self.start_script()
        else:
            self.stop_script()

    def start_script(self): # 修复：方法名改为英文
        if hasattr(self.engine, 'start'): self.engine.start()

    def stop_script(self): # 修复：方法名改为英文
        if hasattr(self.engine, 'stop'): self.engine.stop()

    def pause_script(self): # 修复：方法名改为英文
        if hasattr(self.engine, 'pause'): self.engine.pause()

    def toggle_ui_visibility(self): # 修复：方法名改为英文
        if self.ui_visible: self.hide_ui()
        else: self.show_ui()

    def show_ui(self): # 修复：方法名改为英文
        self.show()
        self.raise_()
        self.activateWindow()
        self.ui_visible = True

    def hide_ui(self): # 修复：方法名改为英文
        self.hide()
        self.ui_visible = False

    def hide_to_tray(self): # 修复：方法名改为英文
        self.hide_ui()
        self.tray_icon.showMessage("游戏脚本", "已最小化，双击托盘图标恢复", QSystemTrayIcon.MessageIcon.Information, 1000)

    def safe_exit(self): # 修复：方法名改为英文
        # 询问用户是否退出
        reply = QMessageBox.question(self, "确认退出", "你确定要退出游戏脚本吗？",
                                   QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No, 
                                   QMessageBox.StandardButton.No)
        
        if reply == QMessageBox.StandardButton.Yes:
            if hasattr(self.engine, 'stop'): self.engine.stop()
            QApplication.quit()
    
    def closeEvent(self, event):
        event.ignore()
        self.hide_to_tray()

# --- 测试引擎 ---
class TestEngine:
    # 保持 TestEngine 内部标识符不变，模式名称保持中文
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
        print("Engine: Stop")

    def pause(self):
        self.paused = not self.paused
        print("Engine: Pause")

    def get_running_status(self):
        if self.running and not self.paused:
            self.execution_count += 1
        
        return {
            'running': self.running,
            'paused': self.paused,
            'mode': self.mode,
            'execution_count': self.execution_count,
            'avg_response_time': self.avg_response_time,
            'success_rate': self.success_rate
        }

def main():
    app = QApplication(sys.argv)
    app.setStyle("Fusion")
    
    engine = TestEngine()
    ui = GameScriptUI(engine) # 修复：使用英文类名
    ui.show_ui()
    
    sys.exit(app.exec())

if __name__ == "__main__":
    main()