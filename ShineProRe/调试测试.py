"""
调试PyQt6应用启动问题
"""
import sys

print("开始调试...")

# 测试基本导入
try:
    from PyQt6.QtWidgets import QApplication, QMainWindow
    print("✅ PyQt6导入成功")
except Exception as e:
    print(f"❌ PyQt6导入失败: {e}")
    sys.exit(1)

# 测试简单窗口
try:
    app = QApplication(sys.argv)
    print("✅ QApplication创建成功")
    
    window = QMainWindow()
    print("✅ QMainWindow创建成功")
    
    window.setWindowTitle("测试窗口")
    window.setGeometry(100, 100, 300, 200)
    window.show()
    print("✅ 窗口显示成功")
    
    print("✅ 调试完成，应用正常运行")
    sys.exit(app.exec())
    
except Exception as e:
    print(f"❌ 调试失败: {e}")
    import traceback
    traceback.print_exc()