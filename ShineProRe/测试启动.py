"""
测试PyQt6应用启动
"""
import sys
import os

# 设置编码以支持中文
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8')

# 添加项目路径
sys.path.append(os.path.dirname(os.path.abspath(__file__)))

print("开始导入PyQt6...")

try:
    from PyQt6.QtWidgets import QApplication, QMainWindow, QLabel
    from PyQt6.QtCore import Qt
    print("✅ PyQt6导入成功")
except Exception as e:
    print(f"❌ PyQt6导入失败: {e}")
    sys.exit(1)

class 测试界面(QMainWindow):
    def __init__(self):
        super().__init__()
        self.setWindowTitle("测试界面")
        self.setGeometry(100, 100, 300, 200)
        
        label = QLabel("测试界面 - 中文显示正常", self)
        label.setAlignment(Qt.AlignmentFlag.AlignCenter)
        self.setCentralWidget(label)

def 主程序():
    print("创建QApplication...")
    app = QApplication(sys.argv)
    
    print("创建界面...")
    window = 测试界面()
    window.show()
    
    print("✅ 应用启动成功!")
    sys.exit(app.exec())

if __name__ == "__main__":
    主程序()