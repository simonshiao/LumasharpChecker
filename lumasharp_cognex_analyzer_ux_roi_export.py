import sys
import os
import json
import time
import csv
from pathlib import Path

import pandas as pd
from PySide6.QtCore import (
    Qt,
    QByteArray,
    QAbstractTableModel,
    QModelIndex,
    QSortFilterProxyModel,
    QTimer,
    QUrl,
    QRectF,
)
from PySide6.QtGui import (
    QAction,
    QColor,
    QDesktopServices,
    QFont,
    QImage,
    QKeySequence,
    QPainter,
    QPen,
    QPixmap,
)
from PySide6.QtWidgets import (
    QApplication,
    QAbstractItemView,
    QButtonGroup,
    QCheckBox,
    QComboBox,
    QDialog,
    QDialogButtonBox,
    QDoubleSpinBox,
    QFileDialog,
    QFormLayout,
    QFrame,
    QGraphicsRectItem,
    QGraphicsScene,
    QGraphicsSimpleTextItem,
    QGraphicsView,
    QGridLayout,
    QGroupBox,
    QHBoxLayout,
    QHeaderView,
    QLabel,
    QLineEdit,
    QListWidget,
    QListWidgetItem,
    QMainWindow,
    QMenu,
    QMessageBox,
    QPushButton,
    QRadioButton,
    QScrollArea,
    QSizePolicy,
    QSplitter,
    QStackedWidget,
    QStyle,
    QTableView,
    QToolButton,
    QVBoxLayout,
    QWidget,
)


# ============================================================
# 路徑設定
# ============================================================
APP_DIR = Path(__file__).resolve().parent
PROFILE_DIR = APP_DIR / "profile"
CONFIG_PATH = APP_DIR / "config.json"
PROFILE_DIR.mkdir(parents=True, exist_ok=True)


# ============================================================
# 明亮科技風樣式
# ============================================================
APP_STYLESHEET = """
QMainWindow, QWidget {
    background: #f3f7fc;
    color: #172438;
    font-family: "Microsoft JhengHei UI", "Noto Sans TC", sans-serif;
    font-size: 13px;
}

QFrame#HeaderCard,
QFrame#PanelCard,
QFrame#RecipeCard,
QFrame#StatCard,
QGroupBox {
    background: #ffffff;
    border: 1px solid #dce6f2;
    border-radius: 12px;
}

QFrame#HeaderCard {
    border-left: 5px solid #2f79f6;
}

QFrame#RecipeCard {
    background: #f8fbff;
    border-color: #d4e5ff;
}

QGroupBox {
    margin-top: 11px;
    padding-top: 13px;
    font-weight: 700;
}

QGroupBox::title {
    subcontrol-origin: margin;
    left: 14px;
    padding: 0 7px;
    color: #294766;
}

QLabel {
    background: transparent;
}

QLabel#TitleLabel {
    color: #10243f;
    font-size: 21px;
    font-weight: 850;
}

QLabel#SectionTitle {
    color: #173a67;
    font-size: 15px;
    font-weight: 800;
}

QLabel#MutedLabel {
    color: #6b7c91;
}

QLabel#PathLabel {
    background: #edf5ff;
    border: 1px solid #d5e7ff;
    border-radius: 8px;
    color: #285d9f;
    padding: 7px 10px;
}

QLabel#ThresholdLabel {
    background: #f5f8fc;
    border: 1px solid #dfe8f3;
    border-radius: 8px;
    color: #40556f;
    padding: 9px 11px;
}

QLabel#WarningLabel {
    background: #fff8e8;
    border: 1px solid #f2d38e;
    border-radius: 8px;
    color: #95620b;
    padding: 8px 10px;
}

QLabel#BadgeBlue {
    background: #eaf3ff;
    border: 1px solid #cfe2ff;
    border-radius: 8px;
    color: #2462aa;
    padding: 5px 9px;
    font-weight: 700;
}

QLabel#BadgeGreen {
    background: #eaf8f0;
    border: 1px solid #caead8;
    border-radius: 8px;
    color: #147a47;
    padding: 5px 9px;
    font-weight: 700;
}

QLabel#BadgeGray {
    background: #f1f4f8;
    border: 1px solid #dce3eb;
    border-radius: 8px;
    color: #607086;
    padding: 5px 9px;
    font-weight: 700;
}

QPushButton {
    min-height: 34px;
    padding: 0 13px;
    border: 1px solid #cbd8e7;
    border-radius: 8px;
    background: #ffffff;
    color: #23384f;
    font-weight: 650;
}

QPushButton:hover {
    background: #f0f6ff;
    border-color: #9db8dc;
}

QPushButton:pressed {
    background: #e6eef9;
}

QPushButton:disabled {
    color: #9ca9b8;
    background: #f3f5f8;
    border-color: #e0e6ed;
}

QPushButton[accent="true"] {
    background: #2f79f6;
    border-color: #2f79f6;
    color: #ffffff;
}

QPushButton[accent="true"]:hover {
    background: #246be0;
    border-color: #246be0;
}

QPushButton[soft="true"] {
    background: #edf5ff;
    border-color: #cfe2ff;
    color: #225ea7;
}

QLineEdit,
QComboBox,
QDoubleSpinBox {
    min-height: 34px;
    padding: 0 9px;
    border: 1px solid #cbd8e7;
    border-radius: 8px;
    background: #ffffff;
    selection-background-color: #2f79f6;
}

QLineEdit:focus,
QComboBox:focus,
QDoubleSpinBox:focus {
    border: 2px solid #2f79f6;
}

QRadioButton,
QCheckBox {
    spacing: 7px;
    color: #334c68;
}

QRadioButton::indicator,
QCheckBox::indicator {
    width: 17px;
    height: 17px;
}

QListWidget,
QTableView {
    background: #ffffff;
    border: 1px solid #dce6f2;
    border-radius: 9px;
    gridline-color: #e6edf5;
    alternate-background-color: #f8fafd;
    selection-background-color: #dceaff;
    selection-color: #173a67;
}

QListWidget::item {
    min-height: 31px;
    padding: 4px 7px;
    border-radius: 6px;
}

QListWidget::item:hover {
    background: #eef5ff;
}

QHeaderView::section {
    background: #edf3f9;
    color: #304660;
    border: 0;
    border-right: 1px solid #dbe4ef;
    border-bottom: 1px solid #d4deea;
    padding: 8px;
    font-weight: 750;
}

QScrollArea {
    border: 0;
    background: transparent;
}

QScrollBar:vertical {
    background: transparent;
    width: 12px;
    margin: 2px;
}

QScrollBar::handle:vertical {
    background: #c2cedc;
    min-height: 28px;
    border-radius: 5px;
}

QScrollBar::handle:vertical:hover {
    background: #9eafc2;
}

QScrollBar::add-line:vertical,
QScrollBar::sub-line:vertical {
    height: 0;
}

QStatusBar {
    background: #ffffff;
    color: #607086;
    border-top: 1px solid #dce6f2;
}
"""


# ============================================================
# 共用輔助函式
# ============================================================
def 安全浮點值(值, 預設=0.0):
    try:
        if 值 is None or 值 == "":
            return float(預設)
        return float(值)
    except (TypeError, ValueError):
        return float(預設)


def ROI有效(roi):
    if not isinstance(roi, dict):
        return False
    try:
        return float(roi.get("w", 0)) > 0 and float(roi.get("h", 0)) > 0
    except (TypeError, ValueError):
        return False


def ROI文字(roi):
    if not ROI有效(roi):
        return "未設定"
    return (
        f"X={安全浮點值(roi.get('x')):.1f}　"
        f"Y={安全浮點值(roi.get('y')):.1f}　"
        f"W={安全浮點值(roi.get('w')):.1f}　"
        f"H={安全浮點值(roi.get('h')):.1f}"
    )


def 從欄位取ROI(row_data, columns):
    值 = []
    for 欄位 in columns:
        目前值 = row_data.get(欄位, 0)
        值.append(0 if pd.isna(目前值) else 安全浮點值(目前值))
    return {"x": 值[0], "y": 值[1], "w": 值[2], "h": 值[3]}


# ============================================================
# DataFrame Table Model
# ============================================================
class DataFrameModel(QAbstractTableModel):
    判定欄位 = {
        "亮度合格",
        "銳利度合格",
        "總判定",
        "新亮度判定",
        "新銳利判定",
        "新總判定",
    }

    def __init__(self, 資料表=None, parent=None):
        super().__init__(parent)
        self._df = 資料表 if 資料表 is not None else pd.DataFrame()

    def set_dataframe(self, 資料表):
        self.beginResetModel()
        self._df = 資料表
        self.endResetModel()

    def dataframe_index_at(self, row):
        if 0 <= row < len(self._df):
            return self._df.index[row]
        return None

    def rowCount(self, parent=QModelIndex()):
        return 0 if parent.isValid() else len(self._df)

    def columnCount(self, parent=QModelIndex()):
        return 0 if parent.isValid() else len(self._df.columns)

    def data(self, index, role=Qt.DisplayRole):
        if not index.isValid():
            return None

        值 = self._df.iloc[index.row(), index.column()]
        欄名 = str(self._df.columns[index.column()])

        if role == Qt.DisplayRole:
            if pd.isna(值):
                return ""
            if isinstance(值, float):
                return f"{值:.3f}"
            return str(值)

        if role == Qt.UserRole:
            if pd.isna(值):
                return ""
            if hasattr(值, "item"):
                try:
                    return 值.item()
                except Exception:
                    pass
            return 值

        if role == Qt.TextAlignmentRole:
            if 欄名 in {"檔名", "完整路徑"}:
                return Qt.AlignVCenter | Qt.AlignLeft
            return Qt.AlignCenter

        if role == Qt.ToolTipRole:
            return None if pd.isna(值) else str(值)

        if role == Qt.FontRole and 欄名 in {"總判定", "新總判定"}:
            字型 = QFont()
            字型.setBold(True)
            return 字型

        if role in {Qt.ForegroundRole, Qt.BackgroundRole} and 欄名 in self.判定欄位:
            判定 = "" if pd.isna(值) else str(值).strip()
            顏色 = None
            背景 = None

            if 判定.upper() == "OK":
                顏色, 背景 = QColor("#147a47"), QColor("#e9f8f0")
            elif 判定.upper() == "NG":
                顏色, 背景 = QColor("#b52e35"), QColor("#fff0f1")
            elif 判定 == "下限超規":
                顏色, 背景 = QColor("#a15c00"), QColor("#fff5e4")
            elif 判定 == "上限超規":
                顏色, 背景 = QColor("#be354b"), QColor("#fff0f3")
            elif 判定 == "數值無效":
                顏色, 背景 = QColor("#7750a8"), QColor("#f4edff")

            if role == Qt.ForegroundRole:
                return 顏色
            return 背景

        if role == Qt.ForegroundRole and 欄名 == "完整路徑":
            return QColor("#607086")

        return None

    def headerData(self, section, orientation, role=Qt.DisplayRole):
        if role != Qt.DisplayRole:
            return None
        if orientation == Qt.Horizontal:
            if 0 <= section < len(self._df.columns):
                return str(self._df.columns[section])
            return ""
        return str(section + 1)


class AnalysisFilterProxyModel(QSortFilterProxyModel):
    """同時支援全文搜尋與判定條件篩選。"""

    def __init__(self, parent=None):
        super().__init__(parent)
        self._文字條件 = ""
        self._判定條件 = "全部"
        self.setDynamicSortFilter(True)

    def set_text_filter(self, 文字):
        self._文字條件 = str(文字 or "").strip().lower()
        self.invalidateFilter()

    def set_result_filter(self, 條件):
        self._判定條件 = str(條件 or "全部")
        self.invalidateFilter()

    def filterAcceptsRow(self, source_row, source_parent):
        模型 = self.sourceModel()
        if not isinstance(模型, DataFrameModel) or source_row >= len(模型._df):
            return True

        資料列 = 模型._df.iloc[source_row]

        if self._文字條件:
            所有文字 = " | ".join(
                "" if pd.isna(值) else str(值) for 值 in 資料列.tolist()
            ).lower()
            if self._文字條件 not in 所有文字:
                return False

        條件 = self._判定條件
        if 條件 == "全部":
            return True

        新亮度 = str(資料列.get("新亮度判定", "")).strip()
        新銳利 = str(資料列.get("新銳利判定", "")).strip()
        新總判定 = str(資料列.get("新總判定", "")).strip().upper()

        if 條件 == "OK":
            return 新總判定 == "OK"
        if 條件 == "NG":
            return 新總判定 == "NG"
        if 條件 == "亮度下限超規":
            return 新亮度 == "下限超規"
        if 條件 == "亮度上限超規":
            return 新亮度 == "上限超規"
        if 條件 == "銳利度下限超規":
            return 新銳利 == "下限超規"
        if 條件 == "數值無效":
            return 新亮度 == "數值無效" or 新銳利 == "數值無效"
        if 條件 == "未判定":
            return 新總判定 == ""
        return True


# ============================================================
# 統計卡片
# ============================================================
class StatCard(QFrame):
    def __init__(self, 標題, parent=None):
        super().__init__(parent)
        self.setObjectName("StatCard")
        self.setMinimumWidth(105)
        self.setSizePolicy(QSizePolicy.Expanding, QSizePolicy.Fixed)

        版面 = QVBoxLayout(self)
        版面.setContentsMargins(12, 9, 12, 9)
        版面.setSpacing(1)

        self.標題標籤 = QLabel(標題)
        self.標題標籤.setObjectName("MutedLabel")
        self.數值標籤 = QLabel("0")
        self.數值標籤.setStyleSheet(
            "font-size: 19px; font-weight: 850; color: #173a67; background: transparent;"
        )

        版面.addWidget(self.標題標籤)
        版面.addWidget(self.數值標籤)

    def set_value(self, 數值, 色彩="#173a67"):
        self.數值標籤.setText(str(數值))
        self.數值標籤.setStyleSheet(
            f"font-size: 19px; font-weight: 850; color: {色彩}; background: transparent;"
        )


# ============================================================
# ROI 輸入與圖片檢視
# ============================================================
class ROIInputDialog(QDialog):
    def __init__(self, default_x=0, default_y=0, default_w=0, default_h=0, parent=None):
        super().__init__(parent)
        self.setWindowTitle("輸入手動 ROI 座標")
        self.resize(340, 245)

        版面 = QFormLayout(self)
        版面.setContentsMargins(18, 18, 18, 18)
        版面.setSpacing(12)

        self.sp_x = self._建立輸入(default_x)
        self.sp_y = self._建立輸入(default_y)
        self.sp_w = self._建立輸入(default_w)
        self.sp_h = self._建立輸入(default_h)

        版面.addRow("ROI X（左上角）", self.sp_x)
        版面.addRow("ROI Y（左上角）", self.sp_y)
        版面.addRow("ROI 寬度 W", self.sp_w)
        版面.addRow("ROI 高度 H", self.sp_h)

        按鈕列 = QDialogButtonBox(QDialogButtonBox.Ok | QDialogButtonBox.Cancel)
        按鈕列.accepted.connect(self.accept)
        按鈕列.rejected.connect(self.reject)
        版面.addRow(按鈕列)

    @staticmethod
    def _建立輸入(預設值):
        控制項 = QDoubleSpinBox()
        控制項.setRange(0, 20000)
        控制項.setDecimals(1)
        控制項.setSingleStep(1)
        控制項.setValue(安全浮點值(預設值))
        return 控制項

    def get_values(self):
        return self.sp_x.value(), self.sp_y.value(), self.sp_w.value(), self.sp_h.value()


class ZoomGraphicsView(QGraphicsView):
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setRenderHints(QPainter.Antialiasing | QPainter.SmoothPixmapTransform)
        self.setDragMode(QGraphicsView.ScrollHandDrag)
        self.setTransformationAnchor(QGraphicsView.AnchorUnderMouse)
        self.setResizeAnchor(QGraphicsView.AnchorViewCenter)
        self.setBackgroundBrush(QColor("#202733"))
        self._縮放倍率 = 1.0

    def wheelEvent(self, event):
        倍率 = 1.18 if event.angleDelta().y() > 0 else 1 / 1.18
        新倍率 = self._縮放倍率 * 倍率
        if 0.04 <= 新倍率 <= 50:
            self.scale(倍率, 倍率)
            self._縮放倍率 = 新倍率
        event.accept()

    def fit_rect(self, rect):
        if rect is None or rect.isEmpty():
            return
        self.resetTransform()
        self.fitInView(rect, Qt.KeepAspectRatio)
        self._縮放倍率 = max(self.transform().m11(), 0.0001)

    def actual_size(self):
        self.resetTransform()
        self._縮放倍率 = 1.0


class ROIImageViewer(QMainWindow):
    """顯示原圖、定位 ROI、亮度 ROI、銳利度 ROI、判定數值，並可輸出標註結果。"""

    def __init__(
        self,
        image_path,
        monitor_roi=None,
        pattern_roi=None,
        sharpness_roi=None,
        image_info=None,
        threshold_text="",
        recipe_name="",
        parent=None,
    ):
        super().__init__(parent)
        self.setAttribute(Qt.WA_DeleteOnClose, True)
        self.setWindowTitle(f"影像檢視與輸出｜{os.path.basename(image_path)}")
        self.resize(1320, 860)
        self.setMinimumSize(980, 680)

        self.image_path = os.path.abspath(str(image_path))
        self.brightness_roi = dict(monitor_roi or {})
        self.sharpness_roi = dict(sharpness_roi or monitor_roi or {})
        self.monitor_roi = self.brightness_roi
        self.pattern_roi = dict(pattern_roi or {})
        self.image_info = dict(image_info or {})
        self.threshold_text = str(threshold_text or "")
        self.recipe_name = str(recipe_name or "未載入")

        self.monitor_item = None
        self.sharpness_item = None
        self.pattern_item = None
        self.image_item = None
        self.original_pixmap = QPixmap(self.image_path)
        self._首次顯示 = True

        if self.original_pixmap.isNull():
            QMessageBox.critical(self, "圖片載入失敗", f"無法載入圖片：\n{self.image_path}")
            QTimer.singleShot(0, self.close)
            return

        中央 = QWidget()
        self.setCentralWidget(中央)
        主版面 = QVBoxLayout(中央)
        主版面.setContentsMargins(10, 10, 10, 10)
        主版面.setSpacing(8)

        主版面.addLayout(self._建立工具列())

        內容版面 = QHBoxLayout()
        內容版面.setSpacing(10)

        self.view = ZoomGraphicsView()
        self.scene = QGraphicsScene(self)
        self.view.setScene(self.scene)
        內容版面.addWidget(self.view, 1)

        內容版面.addWidget(self._建立資訊面板())
        主版面.addLayout(內容版面, 1)

        self.image_item = self.scene.addPixmap(self.original_pixmap)
        self.scene.setSceneRect(self.image_item.boundingRect())
        線寬 = max(2.0, self.original_pixmap.width() / 700.0)

        if ROI有效(self.pattern_roi):
            self.pattern_item = self._加入ROI(
                self.pattern_roi,
                QColor("#20a464"),
                QColor(32, 164, 100, 34),
                "定位 ROI",
                線寬,
            )

        if ROI有效(self.brightness_roi):
            self.monitor_item = self._加入ROI(
                self.brightness_roi,
                QColor("#1687ff"),
                QColor(22, 135, 255, 34),
                "亮度 ROI",
                線寬,
            )

        if ROI有效(self.sharpness_roi):
            self.sharpness_item = self._加入ROI(
                self.sharpness_roi,
                QColor("#ff3b30"),
                QColor(255, 59, 48, 38),
                "銳利度 ROI",
                線寬,
            )

        self.btn_monitor.setEnabled(self.monitor_item is not None)
        self.btn_sharpness.setEnabled(self.sharpness_item is not None)
        self.btn_pattern.setEnabled(self.pattern_item is not None)
        self.statusBar().showMessage(
            "雙擊資料列開啟｜綠框定位｜藍框亮度｜紅框銳利度｜可輸出標註圖與數值 CSV"
        )

    def _建立工具列(self):
        工具列 = QHBoxLayout()
        工具列.setSpacing(7)

        圖例 = QLabel("■ 綠色：定位 ROI　　■ 藍色：亮度 ROI　　■ 紅色：銳利度 ROI　　滾輪縮放／左鍵拖曳")
        圖例.setObjectName("PathLabel")

        btn_fit = QPushButton("⤢ 適合視窗")
        btn_fit.clicked.connect(self._適合視窗)
        btn_actual = QPushButton("1:1")
        btn_actual.clicked.connect(self._原始尺寸)

        self.btn_monitor = QPushButton("聚焦亮度區")
        self.btn_monitor.clicked.connect(self._聚焦監控)
        self.btn_sharpness = QPushButton("聚焦銳利度區")
        self.btn_sharpness.clicked.connect(self._聚焦銳利度)
        self.btn_pattern = QPushButton("聚焦定位區")
        self.btn_pattern.clicked.connect(self._聚焦定位)

        btn_open = QPushButton("↗ 系統開原圖")
        btn_open.clicked.connect(self._系統開啟原圖)

        btn_export_csv = QPushButton("▤ 輸出數值 CSV")
        btn_export_csv.clicked.connect(self._輸出數值CSV)

        btn_export_image = QPushButton("⬇ 輸出標註圖")
        btn_export_image.setProperty("accent", True)
        btn_export_image.clicked.connect(self._輸出標註圖)

        工具列.addWidget(圖例, 1)
        工具列.addWidget(btn_fit)
        工具列.addWidget(btn_actual)
        工具列.addWidget(self.btn_monitor)
        工具列.addWidget(self.btn_sharpness)
        工具列.addWidget(self.btn_pattern)
        工具列.addWidget(btn_open)
        工具列.addWidget(btn_export_csv)
        工具列.addWidget(btn_export_image)
        return 工具列

    def _建立資訊面板(self):
        外框 = QFrame()
        外框.setObjectName("PanelCard")
        外框.setMinimumWidth(315)
        外框.setMaximumWidth(390)

        外版面 = QVBoxLayout(外框)
        外版面.setContentsMargins(14, 14, 14, 14)
        外版面.setSpacing(10)

        標題 = QLabel("檢測資訊")
        標題.setObjectName("SectionTitle")
        外版面.addWidget(標題)

        判定文字 = self._值文字("新總判定")
        if 判定文字 == "—":
            判定文字 = self._值文字("總判定")
        self.label_result = QLabel(f"總判定：{判定文字}")
        self.label_result.setAlignment(Qt.AlignCenter)
        self.label_result.setMinimumHeight(42)
        self.label_result.setStyleSheet(self._判定樣式(判定文字))
        外版面.addWidget(self.label_result)

        捲動 = QScrollArea()
        捲動.setWidgetResizable(True)
        捲動.setFrameShape(QFrame.NoFrame)
        捲動內容 = QWidget()
        資訊版面 = QVBoxLayout(捲動內容)
        資訊版面.setContentsMargins(0, 0, 2, 0)
        資訊版面.setSpacing(7)

        顯示列 = [
            ("檔名", os.path.basename(self.image_path)),
            ("圖片尺寸", f"{self.original_pixmap.width()} × {self.original_pixmap.height()} px"),
            ("Recipe", self.recipe_name or "未載入"),
            ("亮度", self._值文字("亮度")),
            ("銳利度", self._值文字("銳利度")),
            ("原亮度判定", self._值文字("亮度合格")),
            ("原銳利判定", self._值文字("銳利度合格")),
            ("原總判定", self._值文字("總判定")),
            ("新亮度判定", self._值文字("新亮度判定")),
            ("新銳利判定", self._值文字("新銳利判定")),
            ("新總判定", self._值文字("新總判定")),
            ("亮度 ROI", ROI文字(self.brightness_roi)),
            ("銳利度 ROI", ROI文字(self.sharpness_roi)),
            ("定位 ROI", ROI文字(self.pattern_roi)),
        ]

        for 名稱, 值 in 顯示列:
            資訊版面.addWidget(self._建立資訊列(名稱, 值))

        門檻標題 = QLabel("目前規格門檻")
        門檻標題.setObjectName("MutedLabel")
        資訊版面.addWidget(門檻標題)
        門檻 = QLabel(self.threshold_text or "未提供門檻資訊")
        門檻.setWordWrap(True)
        門檻.setTextInteractionFlags(Qt.TextSelectableByMouse)
        門檻.setObjectName("ThresholdLabel")
        資訊版面.addWidget(門檻)

        路徑標題 = QLabel("圖片路徑")
        路徑標題.setObjectName("MutedLabel")
        資訊版面.addWidget(路徑標題)
        路徑 = QLabel(self.image_path)
        路徑.setWordWrap(True)
        路徑.setTextInteractionFlags(Qt.TextSelectableByMouse)
        路徑.setObjectName("PathLabel")
        資訊版面.addWidget(路徑)
        資訊版面.addStretch()

        捲動.setWidget(捲動內容)
        外版面.addWidget(捲動, 1)
        return 外框

    @staticmethod
    def _建立資訊列(名稱, 值):
        卡片 = QFrame()
        卡片.setStyleSheet(
            "QFrame {background:#f7faff; border:1px solid #e1eaf5; "
            "border-radius:8px;}"
        )
        版面 = QHBoxLayout(卡片)
        版面.setContentsMargins(9, 7, 9, 7)
        版面.setSpacing(8)

        名稱標籤 = QLabel(str(名稱))
        名稱標籤.setObjectName("MutedLabel")
        名稱標籤.setMinimumWidth(88)
        值標籤 = QLabel(str(值))
        值標籤.setWordWrap(True)
        值標籤.setTextInteractionFlags(Qt.TextSelectableByMouse)
        值標籤.setAlignment(Qt.AlignRight | Qt.AlignVCenter)

        版面.addWidget(名稱標籤)
        版面.addWidget(值標籤, 1)
        return 卡片

    def _值文字(self, 欄位):
        值 = self.image_info.get(欄位, "")
        try:
            if pd.isna(值):
                return "—"
        except Exception:
            pass
        if 值 is None or str(值).strip() == "":
            return "—"
        if isinstance(值, float):
            return f"{值:.3f}"
        return str(值)

    @staticmethod
    def _判定樣式(判定):
        判定 = str(判定 or "").strip()
        if 判定.upper() == "OK":
            return (
                "QLabel {background:#e9f8f0; color:#147a47; border:1px solid #bfe5cf; "
                "border-radius:9px; font-size:18px; font-weight:850;}"
            )
        if 判定.upper() == "NG":
            return (
                "QLabel {background:#fff0f1; color:#b52e35; border:1px solid #f0c5c8; "
                "border-radius:9px; font-size:18px; font-weight:850;}"
            )
        return (
            "QLabel {background:#eef4fb; color:#38536f; border:1px solid #d8e4f1; "
            "border-radius:9px; font-size:17px; font-weight:800;}"
        )

    def _加入ROI(self, roi, 線色, 填色, 名稱, 線寬):
        x = 安全浮點值(roi.get("x"))
        y = 安全浮點值(roi.get("y"))
        w = 安全浮點值(roi.get("w"))
        h = 安全浮點值(roi.get("h"))

        畫筆 = QPen(線色)
        畫筆.setWidthF(線寬)
        矩形 = QGraphicsRectItem(x, y, w, h)
        矩形.setPen(畫筆)
        矩形.setBrush(填色)
        矩形.setZValue(10)
        self.scene.addItem(矩形)

        標示文字 = f"{名稱}  X={x:.1f} Y={y:.1f} W={w:.1f} H={h:.1f}"
        文字 = QGraphicsSimpleTextItem(標示文字)
        字型 = QFont()
        字型.setBold(True)
        字型.setPointSize(10)
        文字.setFont(字型)
        文字.setBrush(線色)
        文字.setPos(x + 4, max(0, y - 23))
        文字.setZValue(11)
        self.scene.addItem(文字)
        return 矩形

    def showEvent(self, event):
        super().showEvent(event)
        if self._首次顯示:
            self._首次顯示 = False
            QTimer.singleShot(0, self._適合視窗)

    def _適合視窗(self):
        if self.image_item is not None:
            self.view.fit_rect(self.image_item.boundingRect())

    def _原始尺寸(self):
        self.view.actual_size()
        if self.monitor_item is not None:
            self.view.centerOn(self.monitor_item)
        elif self.sharpness_item is not None:
            self.view.centerOn(self.sharpness_item)
        elif self.pattern_item is not None:
            self.view.centerOn(self.pattern_item)

    def _聚焦監控(self):
        if self.monitor_item is not None:
            self.view.fit_rect(self.monitor_item.sceneBoundingRect().adjusted(-30, -30, 30, 30))

    def _聚焦銳利度(self):
        if self.sharpness_item is not None:
            self.view.fit_rect(self.sharpness_item.sceneBoundingRect().adjusted(-30, -30, 30, 30))

    def _聚焦定位(self):
        if self.pattern_item is not None:
            self.view.fit_rect(self.pattern_item.sceneBoundingRect().adjusted(-30, -30, 30, 30))

    def _系統開啟原圖(self):
        QDesktopServices.openUrl(QUrl.fromLocalFile(self.image_path))

    def _輸出標註圖(self):
        預設檔名 = f"{Path(self.image_path).stem}_ROI檢測資訊.png"
        預設路徑 = str(Path(self.image_path).with_name(預設檔名))
        儲存路徑, _ = QFileDialog.getSaveFileName(
            self,
            "輸出含 ROI 與數值的標註圖",
            預設路徑,
            "PNG 圖片 (*.png);;JPEG 圖片 (*.jpg *.jpeg)",
        )
        if not 儲存路徑:
            return
        if not Path(儲存路徑).suffix:
            儲存路徑 += ".png"

        try:
            報表圖 = self._建立輸出報表圖()
            if not 報表圖.save(儲存路徑):
                raise RuntimeError("圖片編碼器無法儲存此格式")
            QMessageBox.information(self, "輸出完成", f"標註圖已輸出：\n{儲存路徑}")
            self.statusBar().showMessage(f"已輸出標註圖：{儲存路徑}", 5000)
        except Exception as 錯誤:
            QMessageBox.critical(self, "輸出失敗", str(錯誤))

    def _建立輸出報表圖(self):
        圖寬 = self.original_pixmap.width()
        圖高 = self.original_pixmap.height()
        比例 = max(1.0, min(2.2, 圖寬 / 1100.0))
        面板寬 = int(440 * 比例)

        資訊列 = self._輸出資訊列()
        預估面板高 = int((165 + len(資訊列) * 43 + 145) * 比例)
        畫布高 = max(圖高, 預估面板高)
        畫布 = QImage(圖寬 + 面板寬, 畫布高, QImage.Format_ARGB32)
        畫布.fill(QColor("#ffffff"))

        畫家 = QPainter(畫布)
        畫家.setRenderHints(QPainter.Antialiasing | QPainter.SmoothPixmapTransform)
        畫家.drawPixmap(0, 0, self.original_pixmap)

        線寬 = max(3.0, 圖寬 / 420.0)
        if ROI有效(self.pattern_roi):
            self._畫輸出ROI(
                畫家,
                self.pattern_roi,
                QColor("#20a464"),
                QColor(32, 164, 100, 38),
                "定位 ROI",
                線寬,
                比例,
                圖寬,
                圖高,
            )
        if ROI有效(self.brightness_roi):
            self._畫輸出ROI(
                畫家,
                self.brightness_roi,
                QColor("#087fff"),
                QColor(8, 127, 255, 38),
                "亮度 ROI",
                線寬,
                比例,
                圖寬,
                圖高,
            )
        if ROI有效(self.sharpness_roi):
            self._畫輸出ROI(
                畫家,
                self.sharpness_roi,
                QColor("#ff2d27"),
                QColor(255, 45, 39, 42),
                "銳利度 ROI",
                線寬,
                比例,
                圖寬,
                圖高,
            )

        panel_x = 圖寬
        畫家.fillRect(QRectF(panel_x, 0, 面板寬, 畫布高), QColor("#f4f8fd"))
        分隔筆 = QPen(QColor("#cfdceb"))
        分隔筆.setWidth(max(1, int(比例)))
        畫家.setPen(分隔筆)
        畫家.drawLine(panel_x, 0, panel_x, 畫布高)

        左 = panel_x + int(28 * 比例)
        右界 = panel_x + 面板寬 - int(24 * 比例)
        y = int(30 * 比例)

        標題字 = QFont("Microsoft JhengHei UI")
        標題字.setBold(True)
        標題字.setPixelSize(int(25 * 比例))
        畫家.setFont(標題字)
        畫家.setPen(QColor("#173a67"))
        畫家.drawText(QRectF(左, y, 右界 - 左, int(40 * 比例)), Qt.AlignLeft | Qt.AlignVCenter, "影像檢測資訊")
        y += int(50 * 比例)

        判定 = self._值文字("新總判定")
        if 判定 == "—":
            判定 = self._值文字("總判定")
        if 判定.upper() == "OK":
            判定背景, 判定文字色 = QColor("#dff5e9"), QColor("#147a47")
        elif 判定.upper() == "NG":
            判定背景, 判定文字色 = QColor("#ffe7e9"), QColor("#b52e35")
        else:
            判定背景, 判定文字色 = QColor("#e7eff8"), QColor("#38536f")
        判定框 = QRectF(左, y, 右界 - 左, int(54 * 比例))
        畫家.fillRect(判定框, 判定背景)
        判定字 = QFont("Microsoft JhengHei UI")
        判定字.setBold(True)
        判定字.setPixelSize(int(23 * 比例))
        畫家.setFont(判定字)
        畫家.setPen(判定文字色)
        畫家.drawText(判定框, Qt.AlignCenter, f"總判定：{判定}")
        y += int(72 * 比例)

        名稱字 = QFont("Microsoft JhengHei UI")
        名稱字.setBold(True)
        名稱字.setPixelSize(int(15 * 比例))
        值字 = QFont("Microsoft JhengHei UI")
        值字.setPixelSize(int(15 * 比例))

        for 名稱, 值 in 資訊列:
            row_h = int(39 * 比例)
            畫家.fillRect(QRectF(左, y, 右界 - 左, row_h), QColor("#ffffff"))
            畫家.setFont(名稱字)
            畫家.setPen(QColor("#62748a"))
            畫家.drawText(QRectF(左 + 8 * 比例, y, 135 * 比例, row_h), Qt.AlignLeft | Qt.AlignVCenter, 名稱)
            畫家.setFont(值字)
            畫家.setPen(QColor("#172438"))
            畫家.drawText(
                QRectF(左 + 142 * 比例, y, 右界 - 左 - 150 * 比例, row_h),
                Qt.AlignRight | Qt.AlignVCenter,
                str(值),
            )
            y += int(43 * 比例)

        y += int(10 * 比例)
        畫家.setFont(名稱字)
        畫家.setPen(QColor("#62748a"))
        畫家.drawText(QRectF(左, y, 右界 - 左, int(28 * 比例)), Qt.AlignLeft | Qt.AlignVCenter, "規格門檻")
        y += int(30 * 比例)
        畫家.setFont(值字)
        畫家.setPen(QColor("#294766"))
        畫家.drawText(
            QRectF(左, y, 右界 - 左, max(int(90 * 比例), 畫布高 - y - 20)),
            Qt.AlignLeft | Qt.AlignTop | Qt.TextWordWrap,
            self.threshold_text or "未提供門檻資訊",
        )

        畫家.end()
        return 畫布

    def _畫輸出ROI(self, 畫家, roi, 線色, 填色, 名稱, 線寬, 比例, 圖寬, 圖高):
        x = 安全浮點值(roi.get("x"))
        y = 安全浮點值(roi.get("y"))
        w = 安全浮點值(roi.get("w"))
        h = 安全浮點值(roi.get("h"))
        矩形 = QRectF(x, y, w, h)

        畫家.fillRect(矩形, 填色)
        筆 = QPen(線色)
        筆.setWidthF(線寬)
        畫家.setPen(筆)
        畫家.drawRect(矩形)

        字 = QFont("Microsoft JhengHei UI")
        字.setBold(True)
        字.setPixelSize(max(14, int(17 * 比例)))
        畫家.setFont(字)
        文字 = f"{名稱}  X={x:.1f} Y={y:.1f} W={w:.1f} H={h:.1f}"
        可用寬 = max(160.0, min(float(圖寬 - 8), float(圖寬 - x - 8)))
        文字框 = 畫家.boundingRect(QRectF(0, 0, 可用寬, 100 * 比例), Qt.AlignLeft | Qt.AlignVCenter, 文字)
        標籤寬 = min(可用寬, 文字框.width() + 16 * 比例)
        標籤高 = 文字框.height() + 8 * 比例
        標籤x = max(4.0, min(float(x), float(圖寬) - 標籤寬 - 4))
        標籤y = y - 標籤高 - 4
        if 標籤y < 4:
            標籤y = min(float(圖高) - 標籤高 - 4, y + 4)
        背景 = QColor(線色)
        背景.setAlpha(225)
        畫家.fillRect(QRectF(標籤x, 標籤y, 標籤寬, 標籤高), 背景)
        畫家.setPen(QColor("#ffffff"))
        畫家.drawText(
            QRectF(標籤x + 8 * 比例, 標籤y, 標籤寬 - 12 * 比例, 標籤高),
            Qt.AlignLeft | Qt.AlignVCenter,
            文字,
        )

    def _輸出資訊列(self):
        return [
            ("檔名", os.path.basename(self.image_path)),
            ("圖片尺寸", f"{self.original_pixmap.width()} × {self.original_pixmap.height()} px"),
            ("Recipe", self.recipe_name or "未載入"),
            ("亮度", self._值文字("亮度")),
            ("銳利度", self._值文字("銳利度")),
            ("原亮度判定", self._值文字("亮度合格")),
            ("原銳利判定", self._值文字("銳利度合格")),
            ("原總判定", self._值文字("總判定")),
            ("新亮度判定", self._值文字("新亮度判定")),
            ("新銳利判定", self._值文字("新銳利判定")),
            ("新總判定", self._值文字("新總判定")),
            ("亮度 ROI", ROI文字(self.brightness_roi)),
            ("銳利度 ROI", ROI文字(self.sharpness_roi)),
            ("定位 ROI", ROI文字(self.pattern_roi)),
        ]

    def _輸出數值CSV(self):
        預設檔名 = f"{Path(self.image_path).stem}_檢測資訊.csv"
        預設路徑 = str(Path(self.image_path).with_name(預設檔名))
        儲存路徑, _ = QFileDialog.getSaveFileName(
            self,
            "輸出此筆檢測數值",
            預設路徑,
            "CSV (*.csv)",
        )
        if not 儲存路徑:
            return
        if not 儲存路徑.lower().endswith(".csv"):
            儲存路徑 += ".csv"

        try:
            輸出資料 = {}
            for 欄位, 值 in self.image_info.items():
                try:
                    if pd.isna(值):
                        輸出資料[str(欄位)] = ""
                    elif hasattr(值, "item"):
                        輸出資料[str(欄位)] = 值.item()
                    else:
                        輸出資料[str(欄位)] = 值
                except Exception:
                    輸出資料[str(欄位)] = str(值)

            輸出資料.update(
                {
                    "輸出圖片路徑": self.image_path,
                    "Recipe名稱": self.recipe_name,
                    "目前規格門檻": self.threshold_text,
                    "檢測ROI_X": 安全浮點值(self.brightness_roi.get("x")),
                    "檢測ROI_Y": 安全浮點值(self.brightness_roi.get("y")),
                    "檢測ROI_W": 安全浮點值(self.brightness_roi.get("w")),
                    "檢測ROI_H": 安全浮點值(self.brightness_roi.get("h")),
                    "亮度ROI_X": 安全浮點值(self.brightness_roi.get("x")),
                    "亮度ROI_Y": 安全浮點值(self.brightness_roi.get("y")),
                    "亮度ROI_W": 安全浮點值(self.brightness_roi.get("w")),
                    "亮度ROI_H": 安全浮點值(self.brightness_roi.get("h")),
                    "銳利度ROI_X": 安全浮點值(self.sharpness_roi.get("x")),
                    "銳利度ROI_Y": 安全浮點值(self.sharpness_roi.get("y")),
                    "銳利度ROI_W": 安全浮點值(self.sharpness_roi.get("w")),
                    "銳利度ROI_H": 安全浮點值(self.sharpness_roi.get("h")),
                    "定位ROI_X": 安全浮點值(self.pattern_roi.get("x")),
                    "定位ROI_Y": 安全浮點值(self.pattern_roi.get("y")),
                    "定位ROI_W": 安全浮點值(self.pattern_roi.get("w")),
                    "定位ROI_H": 安全浮點值(self.pattern_roi.get("h")),
                }
            )
            pd.DataFrame([輸出資料]).to_csv(儲存路徑, encoding="utf-8-sig", index=False)
            QMessageBox.information(self, "輸出完成", f"此筆數值已輸出：\n{儲存路徑}")
            self.statusBar().showMessage(f"已輸出數值 CSV：{儲存路徑}", 5000)
        except Exception as 錯誤:
            QMessageBox.critical(self, "輸出失敗", str(錯誤))


# ============================================================
# 主視窗
# ============================================================
class LumasharpCSVAnalyzer(QMainWindow):
    DISPLAY_COLUMNS = [
        "檔名",
        "完整路徑",
        "亮度",
        "銳利度",
        "亮度合格",
        "銳利度合格",
        "總判定",
        "新亮度判定",
        "新銳利判定",
        "新總判定",
    ]
    LEGACY_ROI_COLUMNS = ["ROI_X", "ROI_Y", "ROI_W", "ROI_H"]
    ROI_COLUMNS = LEGACY_ROI_COLUMNS
    BRIGHTNESS_ROI_COLUMNS = ["亮度ROI_X", "亮度ROI_Y", "亮度ROI_W", "亮度ROI_H"]
    SHARPNESS_ROI_COLUMNS = ["銳利度ROI_X", "銳利度ROI_Y", "銳利度ROI_W", "銳利度ROI_H"]

    def __init__(self):
        super().__init__()
        self.setWindowTitle("亮度銳利度 CSV 分析工具｜Cognex Recipe")
        self.resize(1740, 940)
        self.setMinimumSize(1220, 760)
        self.setAcceptDrops(True)

        self.raw_df = pd.DataFrame()
        self.is_loaded = False
        self.has_roi = False
        self.has_legacy_roi = False
        self.has_brightness_roi = False
        self.has_sharpness_roi = False
        self.result_ready = False
        self.current_csv_path = ""
        self.current_csv_dir = ""

        self.current_recipe = {}
        self.current_recipe_path = ""
        self.recipe_name = "未載入"
        self.pattern_image_path = ""
        self.monitor_roi = {"x": 0, "y": 0, "w": 0, "h": 0}
        self.brightness_roi = {"x": 0, "y": 0, "w": 0, "h": 0}
        self.sharpness_roi = {"x": 0, "y": 0, "w": 0, "h": 0}
        self.pattern_roi = {"x": 0, "y": 0, "w": 0, "h": 0}

        self.roi_windows = []
        self._loading_recipe = False
        self._applying_config = False
        self.config = self._load_config()

        self._建立介面()
        self._建立快捷鍵()
        self._套用設定()
        self._載入啟動資料()

    # --------------------------------------------------------
    # UI 建立
    # --------------------------------------------------------
    def _建立介面(self):
        中央 = QWidget()
        self.setCentralWidget(中央)
        根版面 = QVBoxLayout(中央)
        根版面.setContentsMargins(10, 10, 10, 8)
        根版面.setSpacing(9)

        根版面.addWidget(self._建立標題卡片())

        self.main_splitter = QSplitter(Qt.Horizontal)
        self.main_splitter.setChildrenCollapsible(False)
        self.main_splitter.addWidget(self._建立左側面板())
        self.main_splitter.addWidget(self._建立右側面板())
        self.main_splitter.setStretchFactor(0, 0)
        self.main_splitter.setStretchFactor(1, 1)
        self.main_splitter.setSizes([290, 1400])
        根版面.addWidget(self.main_splitter, 1)

        self.statusBar().showMessage(
            "可拖曳 CSV、CSV 資料夾或 Cognex Recipe JSON 到視窗內載入"
        )

    def _建立標題卡片(self):
        卡片 = QFrame()
        卡片.setObjectName("HeaderCard")
        版面 = QHBoxLayout(卡片)
        版面.setContentsMargins(17, 13, 15, 13)
        版面.setSpacing(12)

        文字區 = QVBoxLayout()
        文字區.setSpacing(3)
        標題 = QLabel("亮度／銳利度 CSV 分析")
        標題.setObjectName("TitleLabel")
        副標題 = QLabel("Cognex Recipe、雙 ROI、上下限細分與完整報表匯出")
        副標題.setObjectName("MutedLabel")
        文字區.addWidget(標題)
        文字區.addWidget(副標題)

        self.label_csv_path = QLabel("尚未載入 CSV")
        self.label_csv_path.setObjectName("PathLabel")
        self.label_csv_path.setMinimumWidth(340)
        self.label_csv_path.setTextInteractionFlags(Qt.TextSelectableByMouse)

        self.btn_recalc = self._建立按鈕(
            "↻ 重新判定", QStyle.SP_BrowserReload, accent=True
        )
        self.btn_recalc.clicked.connect(self.recalculate_spec)
        self.btn_recalc.setEnabled(False)
        self.btn_recalc.setToolTip("依目前規格重新判定（Ctrl+R）")

        self.btn_export = self._建立按鈕(
            "⬇ 匯出報表", QStyle.SP_DialogSaveButton
        )
        self.btn_export.clicked.connect(self.export_new_csv)
        self.btn_export.setEnabled(False)
        self.btn_export.setToolTip("保留原始 CSV 全部欄位匯出（Ctrl+E）")

        版面.addLayout(文字區)
        版面.addStretch(1)
        版面.addWidget(self.label_csv_path, 2)
        版面.addWidget(self.btn_recalc)
        版面.addWidget(self.btn_export)
        return 卡片

    def _建立左側面板(self):
        面板 = QFrame()
        面板.setObjectName("PanelCard")
        面板.setMinimumWidth(250)
        面板.setMaximumWidth(430)
        版面 = QVBoxLayout(面板)
        版面.setContentsMargins(12, 12, 12, 12)
        版面.setSpacing(9)

        標題列 = QHBoxLayout()
        標題 = QLabel("CSV 檔案")
        標題.setObjectName("SectionTitle")
        self.label_csv_count = QLabel("0 個")
        self.label_csv_count.setObjectName("MutedLabel")
        標題列.addWidget(標題)
        標題列.addStretch()
        標題列.addWidget(self.label_csv_count)
        版面.addLayout(標題列)

        按鈕列 = QHBoxLayout()
        btn_dir = self._建立按鈕("📁 資料夾", QStyle.SP_DirOpenIcon)
        btn_dir.clicked.connect(self.load_csv_directory)
        btn_dir.setToolTip("載入 CSV 資料夾（Ctrl+Shift+O）")
        btn_file = self._建立按鈕("📄 單一 CSV", QStyle.SP_FileIcon)
        btn_file.clicked.connect(self.load_single_csv)
        btn_file.setToolTip("載入單一 CSV（Ctrl+O）")
        按鈕列.addWidget(btn_dir)
        按鈕列.addWidget(btn_file)
        版面.addLayout(按鈕列)

        self.csv_search = QLineEdit()
        self.csv_search.setPlaceholderText("🔎 搜尋 CSV 檔名…")
        self.csv_search.setClearButtonEnabled(True)
        self.csv_search.textChanged.connect(self._filter_csv_list)
        版面.addWidget(self.csv_search)

        self.csv_list = QListWidget()
        self.csv_list.setAlternatingRowColors(True)
        self.csv_list.setSelectionMode(QAbstractItemView.SingleSelection)
        self.csv_list.itemClicked.connect(self.on_csv_selected)
        self.csv_list.setContextMenuPolicy(Qt.CustomContextMenu)
        self.csv_list.customContextMenuRequested.connect(self._show_csv_list_menu)
        版面.addWidget(self.csv_list, 1)

        csv工具列 = QHBoxLayout()
        self.btn_refresh_csv = QToolButton()
        self.btn_refresh_csv.setText("↻ 重新整理")
        self.btn_refresh_csv.setToolButtonStyle(Qt.ToolButtonTextBesideIcon)
        self.btn_refresh_csv.setIcon(self.style().standardIcon(QStyle.SP_BrowserReload))
        self.btn_refresh_csv.clicked.connect(self.refresh_csv_folder)
        self.btn_refresh_csv.setEnabled(False)

        self.btn_open_csv_folder = QToolButton()
        self.btn_open_csv_folder.setText("開啟資料夾")
        self.btn_open_csv_folder.setToolButtonStyle(Qt.ToolButtonTextBesideIcon)
        self.btn_open_csv_folder.setIcon(self.style().standardIcon(QStyle.SP_DirIcon))
        self.btn_open_csv_folder.clicked.connect(self._open_current_csv_folder)
        self.btn_open_csv_folder.setEnabled(False)

        csv工具列.addWidget(self.btn_refresh_csv)
        csv工具列.addStretch()
        csv工具列.addWidget(self.btn_open_csv_folder)
        版面.addLayout(csv工具列)

        分隔線 = QFrame()
        分隔線.setFrameShape(QFrame.HLine)
        分隔線.setStyleSheet("color: #dce6f2;")
        版面.addWidget(分隔線)

        recipe標題 = QLabel("Cognex Recipe")
        recipe標題.setObjectName("SectionTitle")
        版面.addWidget(recipe標題)

        self.label_recipe_side = QLabel("尚未載入 Recipe")
        self.label_recipe_side.setWordWrap(True)
        self.label_recipe_side.setObjectName("BadgeGray")
        版面.addWidget(self.label_recipe_side)

        btn_recipe = self._建立按鈕("🧩 載入 Recipe JSON", QStyle.SP_DialogOpenButton)
        btn_recipe.setProperty("soft", True)
        btn_recipe.clicked.connect(self.load_cognex_recipe)
        版面.addWidget(btn_recipe)

        recipe工具列 = QHBoxLayout()
        btn_profile = QToolButton()
        btn_profile.setText("開啟 profile")
        btn_profile.setToolButtonStyle(Qt.ToolButtonTextBesideIcon)
        btn_profile.setIcon(self.style().standardIcon(QStyle.SP_DirIcon))
        btn_profile.clicked.connect(self._open_profile_folder)

        self.btn_open_recipe_folder = QToolButton()
        self.btn_open_recipe_folder.setText("Recipe 位置")
        self.btn_open_recipe_folder.setToolButtonStyle(Qt.ToolButtonTextBesideIcon)
        self.btn_open_recipe_folder.setIcon(self.style().standardIcon(QStyle.SP_FileDialogContentsView))
        self.btn_open_recipe_folder.clicked.connect(self._open_recipe_folder)
        self.btn_open_recipe_folder.setEnabled(False)

        recipe工具列.addWidget(btn_profile)
        recipe工具列.addStretch()
        recipe工具列.addWidget(self.btn_open_recipe_folder)
        版面.addLayout(recipe工具列)
        return 面板

    def _建立右側面板(self):
        容器 = QWidget()
        版面 = QVBoxLayout(容器)
        版面.setContentsMargins(0, 0, 0, 0)
        版面.setSpacing(9)

        版面.addWidget(self._建立Recipe摘要卡片())
        版面.addWidget(self._建立規格卡片())
        版面.addLayout(self._建立統計卡片列())
        版面.addWidget(self._建立表格卡片(), 1)
        return 容器

    def _建立Recipe摘要卡片(self):
        卡片 = QFrame()
        卡片.setObjectName("RecipeCard")
        主版面 = QVBoxLayout(卡片)
        主版面.setContentsMargins(13, 11, 13, 11)
        主版面.setSpacing(8)

        頂部 = QHBoxLayout()
        標題 = QLabel("Recipe 與 ROI 摘要")
        標題.setObjectName("SectionTitle")

        self.label_recipe_name = QLabel("Recipe：未載入")
        self.label_recipe_name.setObjectName("BadgeGray")
        self.label_recipe_mode = QLabel("模式：—")
        self.label_recipe_mode.setObjectName("BadgeGray")
        self.label_template_state = QLabel("模板圖：—")
        self.label_template_state.setObjectName("BadgeGray")

        btn_load = self._建立按鈕("載入 Recipe", QStyle.SP_DialogOpenButton, soft=True)
        btn_load.clicked.connect(self.load_cognex_recipe)

        頂部.addWidget(標題)
        頂部.addSpacing(8)
        頂部.addWidget(self.label_recipe_name)
        頂部.addWidget(self.label_recipe_mode)
        頂部.addWidget(self.label_template_state)
        頂部.addStretch()
        頂部.addWidget(btn_load)
        主版面.addLayout(頂部)

        資訊格 = QGridLayout()
        資訊格.setHorizontalSpacing(10)
        資訊格.setVerticalSpacing(7)

        monitor_title = QLabel("亮度／銳利度 ROI（藍／紅框）")
        monitor_title.setStyleSheet("font-weight: 750; color: #173a67;")
        self.label_monitor_roi = QLabel("未設定")
        self.label_monitor_roi.setObjectName("PathLabel")

        pattern_title = QLabel("定位 ROI（綠框）")
        pattern_title.setStyleSheet("font-weight: 750; color: #147a47;")
        self.label_pattern_roi = QLabel("未設定")
        self.label_pattern_roi.setObjectName("PathLabel")

        self.btn_view_template = self._建立按鈕(
            "▣ 檢視模板雙 ROI", QStyle.SP_FileDialogDetailedView
        )
        self.btn_view_template.clicked.connect(self.view_template_roi)
        self.btn_view_template.setEnabled(False)

        self.btn_view_row_roi = self._建立按鈕(
            "▣ 檢視選取圖片 ROI", QStyle.SP_FileDialogListView
        )
        self.btn_view_row_roi.clicked.connect(self.view_current_roi)
        self.btn_view_row_roi.setEnabled(False)

        btn_manual = self._建立按鈕("＋ 手動選圖 ROI", QStyle.SP_FileDialogNewFolder)
        btn_manual.clicked.connect(self.manual_view_roi)

        資訊格.addWidget(monitor_title, 0, 0)
        資訊格.addWidget(self.label_monitor_roi, 0, 1)
        資訊格.addWidget(pattern_title, 1, 0)
        資訊格.addWidget(self.label_pattern_roi, 1, 1)
        資訊格.addWidget(self.btn_view_template, 0, 2, 2, 1)
        資訊格.addWidget(self.btn_view_row_roi, 0, 3, 2, 1)
        資訊格.addWidget(btn_manual, 0, 4, 2, 1)
        資訊格.setColumnStretch(1, 1)
        主版面.addLayout(資訊格)
        return 卡片

    def _建立規格卡片(self):
        群組 = QGroupBox("規格設定")
        主版面 = QVBoxLayout(群組)
        主版面.setContentsMargins(13, 15, 13, 12)
        主版面.setSpacing(9)

        頂部 = QHBoxLayout()
        模式標籤 = QLabel("規格模式")
        模式標籤.setStyleSheet("font-weight: 750;")
        self.radio_percent = QRadioButton("百分比模式 (%)")
        self.radio_fixed = QRadioButton("固定上下限")
        self.mode_group = QButtonGroup(self)
        self.mode_group.addButton(self.radio_percent)
        self.mode_group.addButton(self.radio_fixed)
        self.radio_percent.setChecked(True)
        self.radio_percent.toggled.connect(self.on_mode_changed)

        self.chk_auto_recalc = QCheckBox("修改完成後自動重判")
        self.chk_auto_recalc.setToolTip("輸入框編輯完成或切換模式後，自動重新判定目前 CSV")

        頂部.addWidget(模式標籤)
        頂部.addWidget(self.radio_percent)
        頂部.addWidget(self.radio_fixed)
        頂部.addStretch()
        頂部.addWidget(self.chk_auto_recalc)
        主版面.addLayout(頂部)

        self.spec_stack = QStackedWidget()
        self.spec_stack.addWidget(self._建立百分比設定頁())
        self.spec_stack.addWidget(self._建立固定值設定頁())
        主版面.addWidget(self.spec_stack)

        self.label_threshold = QLabel("請載入 CSV 後按「重新判定」")
        self.label_threshold.setObjectName("ThresholdLabel")
        self.label_threshold.setWordWrap(True)
        主版面.addWidget(self.label_threshold)
        return 群組

    def _建立百分比設定頁(self):
        頁面 = QWidget()
        版面 = QGridLayout(頁面)
        版面.setContentsMargins(0, 0, 0, 0)
        版面.setHorizontalSpacing(12)
        版面.setVerticalSpacing(5)

        self.sp_bright_base = self._建立規格輸入(0, 10000, 171.080)
        self.sp_bright_percent_lower = self._建立規格輸入(0, 100, 15.000, "%")
        self.sp_bright_percent_upper = self._建立規格輸入(0, 100, 15.000, "%")
        self.sp_sharp_base = self._建立規格輸入(0, 10000, 29.820)
        self.sp_sharp_percent_lower = self._建立規格輸入(0, 100, 30.000, "%")

        欄位 = [
            ("亮度基準值", self.sp_bright_base),
            ("亮度下限偏差", self.sp_bright_percent_lower),
            ("亮度上限偏差", self.sp_bright_percent_upper),
            ("銳利度基準值", self.sp_sharp_base),
            ("銳利度下限偏差", self.sp_sharp_percent_lower),
        ]

        for 欄, (名稱, 控制項) in enumerate(欄位):
            標籤 = QLabel(名稱)
            標籤.setObjectName("MutedLabel")
            版面.addWidget(標籤, 0, 欄)
            版面.addWidget(控制項, 1, 欄)
            版面.setColumnStretch(欄, 1)
            self._連接規格輸入(控制項)
        return 頁面

    def _建立固定值設定頁(self):
        頁面 = QWidget()
        版面 = QGridLayout(頁面)
        版面.setContentsMargins(0, 0, 0, 0)
        版面.setHorizontalSpacing(12)
        版面.setVerticalSpacing(5)

        self.sp_bright_fixed_min = self._建立規格輸入(0, 10000, 145.418)
        self.sp_bright_fixed_max = self._建立規格輸入(0, 10000, 196.742)
        self.sp_sharp_fixed_min = self._建立規格輸入(0, 10000, 20.874)

        欄位 = [
            ("亮度固定下限", self.sp_bright_fixed_min),
            ("亮度固定上限", self.sp_bright_fixed_max),
            ("銳利度固定下限", self.sp_sharp_fixed_min),
        ]

        for 欄, (名稱, 控制項) in enumerate(欄位):
            標籤 = QLabel(名稱)
            標籤.setObjectName("MutedLabel")
            版面.addWidget(標籤, 0, 欄)
            版面.addWidget(控制項, 1, 欄)
            版面.setColumnStretch(欄, 1)
            self._連接規格輸入(控制項)
        版面.setColumnStretch(3, 2)
        return 頁面

    def _建立統計卡片列(self):
        統計列 = QHBoxLayout()
        統計列.setSpacing(7)
        self.stat_cards = {
            "total": StatCard("總筆數"),
            "ok": StatCard("OK"),
            "ng": StatCard("總 NG"),
            "bright_low": StatCard("亮度下限 NG"),
            "bright_high": StatCard("亮度上限 NG"),
            "sharp_low": StatCard("銳利度下限 NG"),
            "invalid": StatCard("數值異常"),
            "rate": StatCard("不良率"),
        }
        for 卡片 in self.stat_cards.values():
            統計列.addWidget(卡片)
        return 統計列

    def _建立表格卡片(self):
        卡片 = QFrame()
        卡片.setObjectName("PanelCard")
        版面 = QVBoxLayout(卡片)
        版面.setContentsMargins(11, 11, 11, 11)
        版面.setSpacing(8)

        工具列 = QHBoxLayout()
        標題 = QLabel("分析結果")
        標題.setObjectName("SectionTitle")
        提示 = QLabel("雙擊檢視圖片、定位／亮度／銳利度 ROI、數值與輸出｜右鍵可用系統開原圖")
        提示.setObjectName("MutedLabel")

        self.combo_result_filter = QComboBox()
        self.combo_result_filter.addItems(
            [
                "全部",
                "OK",
                "NG",
                "亮度下限超規",
                "亮度上限超規",
                "銳利度下限超規",
                "數值無效",
                "未判定",
            ]
        )
        self.combo_result_filter.currentTextChanged.connect(
            self._filter_table_result
        )

        self.table_search = QLineEdit()
        self.table_search.setPlaceholderText("🔎 搜尋檔名、路徑或判定…")
        self.table_search.setClearButtonEnabled(True)
        self.table_search.setMaximumWidth(340)
        self.table_search.textChanged.connect(self._filter_table)

        工具列.addWidget(標題)
        工具列.addWidget(提示)
        工具列.addStretch()
        工具列.addWidget(QLabel("顯示"))
        工具列.addWidget(self.combo_result_filter)
        工具列.addWidget(self.table_search)
        版面.addLayout(工具列)

        初始資料 = pd.DataFrame(columns=self.DISPLAY_COLUMNS)
        self.table_model = DataFrameModel(初始資料)
        self.table_proxy = AnalysisFilterProxyModel(self)
        self.table_proxy.setSourceModel(self.table_model)
        self.table_proxy.setSortRole(Qt.UserRole)

        self.table_view = QTableView()
        self.table_view.setModel(self.table_proxy)
        self.table_view.setSortingEnabled(True)
        self.table_view.setAlternatingRowColors(True)
        self.table_view.setSelectionBehavior(QAbstractItemView.SelectRows)
        self.table_view.setSelectionMode(QAbstractItemView.SingleSelection)
        self.table_view.setEditTriggers(QAbstractItemView.NoEditTriggers)
        self.table_view.setContextMenuPolicy(Qt.CustomContextMenu)
        self.table_view.customContextMenuRequested.connect(self._show_table_menu)
        self.table_view.doubleClicked.connect(self.open_image)
        self.table_view.verticalHeader().setVisible(False)
        self.table_view.verticalHeader().setDefaultSectionSize(33)

        表頭 = self.table_view.horizontalHeader()
        表頭.setMinimumSectionSize(68)
        表頭.setSectionResizeMode(QHeaderView.Interactive)
        表頭.setStretchLastSection(False)

        欄寬 = [165, 430, 90, 90, 95, 95, 90, 110, 110, 100]
        for i, 寬度 in enumerate(欄寬):
            self.table_view.setColumnWidth(i, 寬度)
        表頭.setSectionResizeMode(1, QHeaderView.Stretch)

        版面.addWidget(self.table_view, 1)
        return 卡片

    def _建立按鈕(self, 文字, standard_icon=None, accent=False, soft=False):
        按鈕 = QPushButton(文字)
        if standard_icon is not None:
            按鈕.setIcon(self.style().standardIcon(standard_icon))
        if accent:
            按鈕.setProperty("accent", True)
        if soft:
            按鈕.setProperty("soft", True)
        return 按鈕

    @staticmethod
    def _建立規格輸入(最小值, 最大值, 預設值, 後綴=""):
        控制項 = QDoubleSpinBox()
        控制項.setRange(最小值, 最大值)
        控制項.setDecimals(3)
        控制項.setSingleStep(0.1)
        控制項.setValue(預設值)
        控制項.setKeyboardTracking(False)
        if 後綴:
            控制項.setSuffix(f" {後綴}")
        return 控制項

    def _連接規格輸入(self, 控制項):
        控制項.valueChanged.connect(self._on_spec_changed)
        控制項.editingFinished.connect(self._maybe_auto_recalculate)
        控制項.lineEdit().returnPressed.connect(self.recalculate_spec)

    # --------------------------------------------------------
    # 快捷鍵與設定
    # --------------------------------------------------------
    def _建立快捷鍵(self):
        快捷鍵清單 = [
            ("載入單一 CSV", QKeySequence.Open, self.load_single_csv),
            ("載入 CSV 資料夾", QKeySequence("Ctrl+Shift+O"), self.load_csv_directory),
            ("載入 Recipe", QKeySequence("Ctrl+P"), self.load_cognex_recipe),
            ("重新判定", QKeySequence("Ctrl+R"), self.recalculate_spec),
            ("匯出報表", QKeySequence("Ctrl+E"), self.export_new_csv),
            ("重新整理 CSV", QKeySequence.Refresh, self.refresh_csv_folder),
            ("搜尋表格", QKeySequence.Find, self._focus_table_search),
            ("上一個 CSV", QKeySequence("Alt+Up"), self._select_previous_csv),
            ("下一個 CSV", QKeySequence("Alt+Down"), self._select_next_csv),
        ]
        for 名稱, 快捷鍵, 函式 in 快捷鍵清單:
            動作 = QAction(名稱, self)
            動作.setShortcut(快捷鍵)
            動作.triggered.connect(函式)
            self.addAction(動作)

    def _load_config(self):
        預設 = {
            "last_csv_dir": "",
            "last_csv_path": "",
            "last_recipe_path": "",
            "limit_mode": "percent",
            "auto_recalc": False,
            "bright_base": 171.080,
            "bright_percent_lower": 15.000,
            "bright_percent_upper": 15.000,
            "bright_fixed_min": 145.418,
            "bright_fixed_max": 196.742,
            "sharp_base": 29.820,
            "sharp_percent_lower": 30.000,
            "sharp_fixed_min": 20.874,
            "splitter_sizes": [290, 1400],
            "window_geometry": "",
        }
        if CONFIG_PATH.exists():
            try:
                with CONFIG_PATH.open("r", encoding="utf-8") as 檔案:
                    內容 = json.load(檔案)
                if isinstance(內容, dict):
                    預設.update(內容)
            except Exception:
                pass
        return 預設

    def _save_config(self):
        self.config.update(
            {
                "last_csv_dir": self.current_csv_dir,
                "last_csv_path": self.current_csv_path if self.is_loaded else "",
                "last_recipe_path": self.current_recipe_path,
                "limit_mode": self._current_limit_mode(),
                "auto_recalc": self.chk_auto_recalc.isChecked(),
                "bright_base": self.sp_bright_base.value(),
                "bright_percent_lower": self.sp_bright_percent_lower.value(),
                "bright_percent_upper": self.sp_bright_percent_upper.value(),
                "bright_fixed_min": self.sp_bright_fixed_min.value(),
                "bright_fixed_max": self.sp_bright_fixed_max.value(),
                "sharp_base": self.sp_sharp_base.value(),
                "sharp_percent_lower": self.sp_sharp_percent_lower.value(),
                "sharp_fixed_min": self.sp_sharp_fixed_min.value(),
                "splitter_sizes": self.main_splitter.sizes(),
                "window_geometry": bytes(self.saveGeometry().toBase64()).decode("ascii"),
            }
        )
        try:
            with CONFIG_PATH.open("w", encoding="utf-8") as 檔案:
                json.dump(self.config, 檔案, ensure_ascii=False, indent=2)
        except Exception:
            pass

    def _套用設定(self):
        self._applying_config = True
        控制項設定 = [
            (self.sp_bright_base, "bright_base"),
            (self.sp_bright_percent_lower, "bright_percent_lower"),
            (self.sp_bright_percent_upper, "bright_percent_upper"),
            (self.sp_bright_fixed_min, "bright_fixed_min"),
            (self.sp_bright_fixed_max, "bright_fixed_max"),
            (self.sp_sharp_base, "sharp_base"),
            (self.sp_sharp_percent_lower, "sharp_percent_lower"),
            (self.sp_sharp_fixed_min, "sharp_fixed_min"),
        ]
        for 控制項, 鍵 in 控制項設定:
            控制項.setValue(安全浮點值(self.config.get(鍵), 控制項.value()))

        if str(self.config.get("limit_mode", "percent")).lower() == "fixed":
            self.radio_fixed.setChecked(True)
        else:
            self.radio_percent.setChecked(True)
        self.chk_auto_recalc.setChecked(bool(self.config.get("auto_recalc", False)))

        幾何 = self.config.get("window_geometry", "")
        if 幾何:
            try:
                self.restoreGeometry(QByteArray.fromBase64(幾何.encode("ascii")))
            except Exception:
                pass

        尺寸 = self.config.get("splitter_sizes", [290, 1400])
        if isinstance(尺寸, list) and len(尺寸) == 2:
            self.main_splitter.setSizes([int(x) for x in 尺寸])

        self._applying_config = False
        self.on_mode_changed()
        self._update_threshold_preview()

    def _載入啟動資料(self):
        recipe_path = self.config.get("last_recipe_path", "")
        if recipe_path and os.path.isfile(recipe_path):
            self._load_recipe_from_file(recipe_path, silent=True, trigger_recalc=False)

        csv_dir = self.config.get("last_csv_dir", "")
        csv_path = self.config.get("last_csv_path", "")
        if csv_dir and os.path.isdir(csv_dir):
            self._populate_csv_list(csv_dir)
            if csv_path and os.path.isfile(csv_path):
                self._select_and_load_csv(csv_path)

        if self.is_loaded and self.current_recipe and self.chk_auto_recalc.isChecked():
            self.recalculate_spec()

    # --------------------------------------------------------
    # 模式與規格
    # --------------------------------------------------------
    def _current_limit_mode(self):
        return "percent" if self.radio_percent.isChecked() else "fixed"

    def on_mode_changed(self):
        模式 = self._current_limit_mode()
        self.spec_stack.setCurrentIndex(0 if 模式 == "percent" else 1)
        self._update_threshold_preview()

        if not self._loading_recipe and not self._applying_config:
            self._mark_result_outdated()
            self._maybe_auto_recalculate()

    def _on_spec_changed(self):
        if self._loading_recipe or self._applying_config:
            return
        self._update_threshold_preview()
        self._mark_result_outdated()

    def _maybe_auto_recalculate(self):
        if (
            self.chk_auto_recalc.isChecked()
            and self.is_loaded
            and not self._loading_recipe
            and not self._applying_config
        ):
            QTimer.singleShot(0, self.recalculate_spec)

    def _mark_result_outdated(self):
        if not self.is_loaded:
            return
        self.result_ready = False
        self.btn_export.setEnabled(False)
        預覽 = self._threshold_text()
        self.label_threshold.setText(f"{預覽}　｜　規格已變更，請重新判定")

    def _get_thresholds(self):
        if self._current_limit_mode() == "percent":
            bright_base = self.sp_bright_base.value()
            bright_low = bright_base * (
                1 - self.sp_bright_percent_lower.value() / 100
            )
            bright_high = bright_base * (
                1 + self.sp_bright_percent_upper.value() / 100
            )
            sharp_base = self.sp_sharp_base.value()
            sharp_low = sharp_base * (
                1 - self.sp_sharp_percent_lower.value() / 100
            )
        else:
            bright_low = self.sp_bright_fixed_min.value()
            bright_high = self.sp_bright_fixed_max.value()
            sharp_low = self.sp_sharp_fixed_min.value()
        return bright_low, bright_high, sharp_low

    def _threshold_text(self):
        bright_low, bright_high, sharp_low = self._get_thresholds()
        模式文字 = "百分比" if self._current_limit_mode() == "percent" else "固定上下限"
        return (
            f"模式：{模式文字}　｜　亮度門檻：{bright_low:.3f} ～ {bright_high:.3f}　｜　"
            f"銳利度下限：{sharp_low:.3f}"
        )

    def _update_threshold_preview(self):
        if not hasattr(self, "label_threshold"):
            return
        bright_low, bright_high, _ = self._get_thresholds()
        if bright_low > bright_high:
            self.label_threshold.setObjectName("WarningLabel")
            self.label_threshold.setText(
                "固定值設定錯誤：亮度下限不可大於亮度上限。"
            )
        else:
            self.label_threshold.setObjectName("ThresholdLabel")
            self.label_threshold.setText(self._threshold_text())
        self.label_threshold.style().unpolish(self.label_threshold)
        self.label_threshold.style().polish(self.label_threshold)

    # --------------------------------------------------------
    # Cognex Recipe
    # --------------------------------------------------------
    def load_cognex_recipe(self):
        起始路徑 = (
            os.path.dirname(self.current_recipe_path)
            if self.current_recipe_path
            else str(PROFILE_DIR)
        )
        file_path, _ = QFileDialog.getOpenFileName(
            self,
            "選取 Cognex Recipe",
            起始路徑,
            "JSON 檔案 (*.json);;所有檔案 (*.*)",
        )
        if file_path:
            self._load_recipe_from_file(file_path)

    def _load_recipe_from_file(self, file_path, silent=False, trigger_recalc=True):
        try:
            with open(file_path, "r", encoding="utf-8") as 檔案:
                data = json.load(檔案)
            if not isinstance(data, dict):
                raise ValueError("Recipe JSON 根節點必須是物件格式。")

            self._loading_recipe = True
            self.current_recipe = data
            self.current_recipe_path = str(Path(file_path).resolve())
            self.recipe_name = str(
                data.get("name") or Path(file_path).stem
            ).strip()

            self.sp_bright_base.setValue(
                安全浮點值(data.get("brightness_base_value"), 0)
            )
            self.sp_bright_percent_lower.setValue(
                安全浮點值(data.get("brightness_percent_lower"), 0)
            )
            self.sp_bright_percent_upper.setValue(
                安全浮點值(data.get("brightness_percent_upper"), 0)
            )
            self.sp_bright_fixed_min.setValue(
                安全浮點值(data.get("brightness_fixed_min"), 0)
            )
            self.sp_bright_fixed_max.setValue(
                安全浮點值(data.get("brightness_fixed_max"), 0)
            )

            self.sp_sharp_base.setValue(
                安全浮點值(data.get("sharpness_base_value"), 0)
            )
            self.sp_sharp_percent_lower.setValue(
                安全浮點值(data.get("sharpness_percent_lower"), 0)
            )
            self.sp_sharp_fixed_min.setValue(
                安全浮點值(data.get("sharpness_fixed_min"), 0)
            )

            模式 = str(data.get("brightness_limit_mode", "Percent")).lower()
            if 模式 == "percent":
                self.radio_percent.setChecked(True)
            else:
                self.radio_fixed.setChecked(True)

            self.pattern_roi = {
                "x": 安全浮點值(data.get("patternmatch_roi_x")),
                "y": 安全浮點值(data.get("patternmatch_roi_y")),
                "w": 安全浮點值(data.get("patternmatch_roi_w")),
                "h": 安全浮點值(data.get("patternmatch_roi_h")),
            }
            self.monitor_roi = {
                "x": 安全浮點值(data.get("monitor_roi_x")),
                "y": 安全浮點值(data.get("monitor_roi_y")),
                "w": 安全浮點值(data.get("monitor_roi_w")),
                "h": 安全浮點值(data.get("monitor_roi_h")),
            }
            self.brightness_roi = {
                "x": 安全浮點值(data.get("brightness_roi_x", data.get("monitor_roi_x"))),
                "y": 安全浮點值(data.get("brightness_roi_y", data.get("monitor_roi_y"))),
                "w": 安全浮點值(data.get("brightness_roi_w", data.get("monitor_roi_w"))),
                "h": 安全浮點值(data.get("brightness_roi_h", data.get("monitor_roi_h"))),
            }
            self.sharpness_roi = {
                "x": 安全浮點值(data.get("sharpness_roi_x", data.get("monitor_roi_x"))),
                "y": 安全浮點值(data.get("sharpness_roi_y", data.get("monitor_roi_y"))),
                "w": 安全浮點值(data.get("sharpness_roi_w", data.get("monitor_roi_w"))),
                "h": 安全浮點值(data.get("sharpness_roi_h", data.get("monitor_roi_h"))),
            }
            self.monitor_roi = dict(self.brightness_roi)

            self.pattern_image_path = self._resolve_recipe_image_path(
                data.get("imagePath", ""), self.current_recipe_path
            )
            self._loading_recipe = False

            self.spec_stack.setCurrentIndex(
                0 if self.radio_percent.isChecked() else 1
            )
            self._update_recipe_summary()
            self._update_threshold_preview()
            self.btn_open_recipe_folder.setEnabled(True)
            self._save_config()

            if self.is_loaded:
                if trigger_recalc and self.chk_auto_recalc.isChecked():
                    self.recalculate_spec()
                else:
                    self._mark_result_outdated()

            self.statusBar().showMessage(
                f"Recipe 載入完成：{self.recipe_name}", 5000
            )
            if not silent:
                模板狀態 = (
                    "已找到" if os.path.isfile(self.pattern_image_path) else "未找到"
                )
                QMessageBox.information(
                    self,
                    "Recipe 載入完成",
                    f"名稱：{self.recipe_name}\n"
                    f"模式：{'百分比' if self.radio_percent.isChecked() else '固定上下限'}\n"
                    f"亮度 ROI：{ROI文字(self.brightness_roi)}\n"
                    f"銳利度 ROI：{ROI文字(self.sharpness_roi)}\n"
                    f"定位 ROI：{ROI文字(self.pattern_roi)}\n"
                    f"模板圖：{模板狀態}",
                )
        except Exception as 錯誤:
            self._loading_recipe = False
            QMessageBox.critical(
                self,
                "Recipe 載入失敗",
                f"{file_path}\n\n{錯誤}",
            )

    def _resolve_recipe_image_path(self, 路徑值, recipe_file_path):
        原始 = str(路徑值 or "").strip().strip('"')
        if not 原始:
            return ""

        原始 = os.path.expandvars(os.path.expanduser(原始))
        候選路徑 = []

        if os.path.isabs(原始):
            候選路徑.append(原始)
        else:
            recipe_dir = os.path.dirname(recipe_file_path)
            候選路徑.extend(
                [
                    os.path.join(recipe_dir, 原始),
                    os.path.join(str(APP_DIR), 原始),
                    os.path.join(str(PROFILE_DIR), 原始),
                    os.path.join(str(PROFILE_DIR), os.path.basename(原始)),
                ]
            )

        for 候選 in 候選路徑:
            正規化 = os.path.normpath(候選)
            if os.path.isfile(正規化):
                return os.path.abspath(正規化)

        return os.path.abspath(os.path.normpath(候選路徑[0])) if 候選路徑 else ""

    def _update_recipe_summary(self):
        有Recipe = bool(self.current_recipe)
        模式文字 = "百分比" if self.radio_percent.isChecked() else "固定上下限"

        self.label_recipe_name.setText(
            f"Recipe：{self.recipe_name if 有Recipe else '未載入'}"
        )
        self.label_recipe_name.setObjectName("BadgeBlue" if 有Recipe else "BadgeGray")
        self.label_recipe_mode.setText(f"模式：{模式文字 if 有Recipe else '—'}")
        self.label_recipe_mode.setObjectName("BadgeGreen" if 有Recipe else "BadgeGray")

        模板存在 = bool(self.pattern_image_path and os.path.isfile(self.pattern_image_path))
        if 模板存在:
            self.label_template_state.setText(
                f"模板圖：{os.path.basename(self.pattern_image_path)}"
            )
            self.label_template_state.setObjectName("BadgeGreen")
        elif self.pattern_image_path:
            self.label_template_state.setText("模板圖：找不到檔案")
            self.label_template_state.setObjectName("BadgeGray")
        else:
            self.label_template_state.setText("模板圖：—")
            self.label_template_state.setObjectName("BadgeGray")

        for 標籤 in [
            self.label_recipe_name,
            self.label_recipe_mode,
            self.label_template_state,
        ]:
            標籤.style().unpolish(標籤)
            標籤.style().polish(標籤)

        self.label_monitor_roi.setText(
            f"亮度 {ROI文字(self.brightness_roi)}　｜　銳利度 {ROI文字(self.sharpness_roi)}"
        )
        self.label_pattern_roi.setText(ROI文字(self.pattern_roi))
        self.label_recipe_side.setText(
            f"{self.recipe_name}\n{模式文字}｜"
            f"模板{'可用' if 模板存在 else '未找到'}"
            if 有Recipe
            else "尚未載入 Recipe"
        )
        self.label_recipe_side.setObjectName("BadgeBlue" if 有Recipe else "BadgeGray")
        self.label_recipe_side.style().unpolish(self.label_recipe_side)
        self.label_recipe_side.style().polish(self.label_recipe_side)

        self.btn_view_template.setEnabled(模板存在)
        self.btn_open_recipe_folder.setEnabled(bool(self.current_recipe_path))

    def view_template_roi(self):
        if not self.pattern_image_path or not os.path.isfile(self.pattern_image_path):
            QMessageBox.warning(
                self,
                "模板圖無效",
                "目前 Recipe 的模板圖不存在，請確認 imagePath 與檔案位置。",
            )
            return
        self._show_roi_window(
            self.pattern_image_path,
            self.brightness_roi,
            self.pattern_roi,
            {
                "檔名": os.path.basename(self.pattern_image_path),
                "完整路徑": self.pattern_image_path,
                "類型": "Recipe 模板圖",
            },
            sharpness_roi=self.sharpness_roi,
        )

    # --------------------------------------------------------
    # CSV 載入
    # --------------------------------------------------------
    def load_csv_directory(self):
        起始路徑 = self.current_csv_dir or self.config.get("last_csv_dir", "")
        資料夾 = QFileDialog.getExistingDirectory(
            self, "選取 CSV 資料夾", 起始路徑
        )
        if 資料夾:
            self._populate_csv_list(資料夾)
            self._save_config()

    def _populate_csv_list(self, dir_path):
        try:
            資料夾 = Path(dir_path)
            csv_files = sorted(
                [
                    檔案
                    for 檔案 in 資料夾.iterdir()
                    if 檔案.is_file() and 檔案.suffix.lower() == ".csv"
                ],
                key=lambda p: p.name.lower(),
            )
        except Exception as 錯誤:
            QMessageBox.critical(self, "資料夾讀取失敗", str(錯誤))
            return

        self.current_csv_dir = str(資料夾.resolve())
        self.csv_list.clear()
        for 檔案 in csv_files:
            項目 = QListWidgetItem(檔案.name)
            項目.setData(Qt.UserRole, str(檔案.resolve()))
            項目.setToolTip(str(檔案.resolve()))
            self.csv_list.addItem(項目)

        self.label_csv_count.setText(f"{len(csv_files)} 個")
        self.btn_refresh_csv.setEnabled(True)
        self.btn_open_csv_folder.setEnabled(True)
        self.statusBar().showMessage(
            f"已載入資料夾，共 {len(csv_files)} 個 CSV", 5000
        )

    def refresh_csv_folder(self):
        if self.current_csv_dir and os.path.isdir(self.current_csv_dir):
            目前檔案 = self.current_csv_path
            self._populate_csv_list(self.current_csv_dir)
            if 目前檔案 and os.path.isfile(目前檔案):
                self._select_list_item_only(目前檔案)

    def _select_list_item_only(self, full_path):
        比對路徑 = os.path.normcase(os.path.abspath(full_path))
        for i in range(self.csv_list.count()):
            item = self.csv_list.item(i)
            item_path = os.path.normcase(
                os.path.abspath(str(item.data(Qt.UserRole)))
            )
            if item_path == 比對路徑:
                self.csv_list.setCurrentRow(i)
                self.csv_list.scrollToItem(item)
                return True
        return False

    def _select_and_load_csv(self, full_path):
        self._select_list_item_only(full_path)
        self._process_csv(full_path)

    def on_csv_selected(self, item):
        self._process_csv(item.data(Qt.UserRole))

    def load_single_csv(self):
        起始路徑 = self.current_csv_dir or self.config.get("last_csv_dir", "")
        path, _ = QFileDialog.getOpenFileName(
            self,
            "選取 CSV 報表",
            起始路徑,
            "CSV Files (*.csv)",
        )
        if path:
            self._load_single_csv_path(path)

    def _load_single_csv_path(self, path):
        path = str(Path(path).resolve())
        self.current_csv_dir = str(Path(path).parent)

        if not self._select_list_item_only(path):
            項目 = QListWidgetItem(os.path.basename(path))
            項目.setData(Qt.UserRole, path)
            項目.setToolTip(path)
            self.csv_list.addItem(項目)
            self.csv_list.setCurrentItem(項目)
            self.label_csv_count.setText(f"{self.csv_list.count()} 個")

        self.btn_open_csv_folder.setEnabled(True)
        self.btn_refresh_csv.setEnabled(True)
        self._process_csv(path)

    @staticmethod
    def _read_csv_safely(path):
        最後錯誤 = None
        for 編碼 in ("utf-8-sig", "utf-8", "cp950", "big5"):
            try:
                with open(path, "r", encoding=編碼, newline="") as 檔案:
                    列資料 = list(csv.reader(檔案))

                表頭列 = None
                規格資料 = {}
                必要欄位 = {"檔名", "完整路徑", "亮度", "銳利度"}
                for index, row in enumerate(列資料):
                    欄位集合 = {str(欄).strip() for 欄 in row}
                    if 必要欄位.issubset(欄位集合):
                        表頭列 = index
                        break
                    if len(row) >= 2:
                        key = str(row[0]).strip()
                        value = str(row[1]).strip()
                        if key and key != "檢測規格":
                            規格資料[key] = value

                if 表頭列 is None:
                    return pd.read_csv(path, encoding=編碼, dtype=str), {}

                return (
                    pd.read_csv(path, encoding=編碼, dtype=str, skiprows=表頭列),
                    規格資料,
                )
            except UnicodeDecodeError as 錯誤:
                最後錯誤 = 錯誤
            except Exception:
                raise
        raise 最後錯誤 or RuntimeError("無法辨識 CSV 編碼")

    def _apply_csv_spec_metadata(self, 規格資料):
        if not 規格資料:
            return

        self._applying_config = True
        try:
            模式文字 = str(規格資料.get("亮度判定方式", "")).lower()
            if "fixed" in 模式文字 or "固定" in 模式文字:
                self.radio_fixed.setChecked(True)
            elif "percent" in 模式文字 or "百分" in 模式文字:
                self.radio_percent.setChecked(True)

            控制項設定 = [
                (self.sp_bright_base, "亮度基準值"),
                (self.sp_bright_percent_lower, "亮度下限偏差%"),
                (self.sp_bright_percent_upper, "亮度上限偏差%"),
                (self.sp_bright_fixed_min, "亮度固定下限"),
                (self.sp_bright_fixed_max, "亮度固定上限"),
                (self.sp_sharp_base, "銳利度基準值"),
                (self.sp_sharp_percent_lower, "銳利度下限偏差%"),
                (self.sp_sharp_fixed_min, "銳利度固定下限"),
            ]
            for 控制項, 欄名 in 控制項設定:
                if 欄名 in 規格資料:
                    控制項.setValue(安全浮點值(規格資料.get(欄名), 控制項.value()))
        finally:
            self.spec_stack.setCurrentIndex(
                0 if self.radio_percent.isChecked() else 1
            )
            self._update_threshold_preview()
            self._applying_config = False

    def _process_csv(self, path):
        if not path or not os.path.isfile(path):
            QMessageBox.warning(self, "找不到 CSV", str(path))
            return

        QApplication.setOverrideCursor(Qt.WaitCursor)
        開始時間 = time.perf_counter()
        try:
            raw_df, 規格資料 = self._read_csv_safely(path)
            self._apply_csv_spec_metadata(規格資料)

            必要欄位 = ["檔名", "完整路徑", "亮度", "銳利度"]
            缺少欄位 = [欄 for 欄 in 必要欄位 if 欄 not in raw_df.columns]
            if 缺少欄位:
                QMessageBox.critical(
                    self,
                    "CSV 格式不符",
                    "缺少必要欄位：\n" + "、".join(缺少欄位),
                )
                return

            raw_df["亮度"] = pd.to_numeric(raw_df["亮度"], errors="coerce")
            raw_df["銳利度"] = pd.to_numeric(raw_df["銳利度"], errors="coerce")

            for 欄位 in ["亮度合格", "銳利度合格", "總判定"]:
                if 欄位 not in raw_df.columns:
                    raw_df[欄位] = ""

            self.has_legacy_roi = all(欄位 in raw_df.columns for 欄位 in self.LEGACY_ROI_COLUMNS)
            self.has_brightness_roi = all(欄位 in raw_df.columns for 欄位 in self.BRIGHTNESS_ROI_COLUMNS)
            self.has_sharpness_roi = all(欄位 in raw_df.columns for 欄位 in self.SHARPNESS_ROI_COLUMNS)
            self.has_roi = self.has_legacy_roi or self.has_brightness_roi or self.has_sharpness_roi
            for 欄位群組, 有欄位 in [
                (self.LEGACY_ROI_COLUMNS, self.has_legacy_roi),
                (self.BRIGHTNESS_ROI_COLUMNS, self.has_brightness_roi),
                (self.SHARPNESS_ROI_COLUMNS, self.has_sharpness_roi),
            ]:
                if 有欄位:
                    for 欄位 in 欄位群組:
                        raw_df[欄位] = pd.to_numeric(raw_df[欄位], errors="coerce")

            for 欄位 in ["新亮度判定", "新銳利判定", "新總判定"]:
                if 欄位 not in raw_df.columns:
                    raw_df[欄位] = ""
                else:
                    raw_df[欄位] = raw_df[欄位].fillna("")

            self.raw_df = raw_df
            self.current_csv_path = str(Path(path).resolve())
            self.current_csv_dir = str(Path(path).resolve().parent)
            self.is_loaded = True
            self.result_ready = not (
                self.raw_df["新總判定"].astype(str).str.strip() == ""
            ).all()

            self._refresh_table_model()
            self.table_search.clear()
            self.combo_result_filter.setCurrentText("全部")
            self.label_csv_path.setText(f"已載入：{os.path.basename(path)}")
            self.label_csv_path.setToolTip(self.current_csv_path)

            self.btn_recalc.setEnabled(True)
            self.btn_export.setEnabled(self.result_ready)
            self.btn_view_row_roi.setEnabled(True)
            self.btn_open_csv_folder.setEnabled(True)
            self.btn_refresh_csv.setEnabled(True)

            if self.result_ready:
                self._update_statistics_from_current_result()
                self.label_threshold.setText(
                    f"{self._threshold_text()}　｜　CSV 內含既有重判結果"
                )
            else:
                self._reset_statistics(len(self.raw_df))
                self.label_threshold.setText(
                    f"{self._threshold_text()}　｜　請按「重新判定」"
                )

            self._save_config()
            耗時 = (time.perf_counter() - 開始時間) * 1000
            if self.has_brightness_roi or self.has_sharpness_roi:
                ROI訊息 = "含新版亮度／銳利度 ROI"
            elif self.has_legacy_roi:
                ROI訊息 = "含舊版列 ROI"
            else:
                ROI訊息 = "使用 Recipe ROI 備援"
            self.statusBar().showMessage(
                f"載入完成：{len(self.raw_df)} 筆｜{ROI訊息}｜{耗時:.0f} ms",
                6000,
            )
        except Exception as 錯誤:
            QMessageBox.critical(
                self, "CSV 載入失敗", f"{path}\n\n{錯誤}"
            )
        finally:
            QApplication.restoreOverrideCursor()

    def _refresh_table_model(self):
        顯示資料 = self.raw_df.reindex(columns=self.DISPLAY_COLUMNS).copy()
        self.table_model.set_dataframe(顯示資料)

    # --------------------------------------------------------
    # 重新判定與統計
    # --------------------------------------------------------
    def recalculate_spec(self):
        if not self.is_loaded:
            QMessageBox.warning(self, "尚未載入", "請先載入 CSV 資料。")
            return

        bright_low, bright_high, sharp_low = self._get_thresholds()
        if bright_low > bright_high:
            QMessageBox.warning(
                self,
                "規格錯誤",
                "亮度下限不可大於亮度上限，請修正固定值設定。",
            )
            self.sp_bright_fixed_min.setFocus()
            return

        QApplication.setOverrideCursor(Qt.WaitCursor)
        try:
            bright = pd.to_numeric(self.raw_df["亮度"], errors="coerce")
            sharp = pd.to_numeric(self.raw_df["銳利度"], errors="coerce")

            bright_valid = bright.notna()
            sharp_valid = sharp.notna()

            new_bright = pd.Series(
                "數值無效", index=self.raw_df.index, dtype="object"
            )
            new_bright[bright_valid & (bright < bright_low)] = "下限超規"
            new_bright[bright_valid & (bright > bright_high)] = "上限超規"
            new_bright[
                bright_valid
                & (bright >= bright_low)
                & (bright <= bright_high)
            ] = "OK"

            new_sharp = pd.Series(
                "數值無效", index=self.raw_df.index, dtype="object"
            )
            new_sharp[sharp_valid & (sharp < sharp_low)] = "下限超規"
            new_sharp[sharp_valid & (sharp >= sharp_low)] = "OK"

            new_total = pd.Series("NG", index=self.raw_df.index, dtype="object")
            new_total[(new_bright == "OK") & (new_sharp == "OK")] = "OK"

            self.raw_df["新亮度判定"] = new_bright
            self.raw_df["新銳利判定"] = new_sharp
            self.raw_df["新總判定"] = new_total
            self.result_ready = True

            self._refresh_table_model()
            self._update_statistics_from_current_result()
            self.label_threshold.setObjectName("ThresholdLabel")
            self.label_threshold.setText(
                f"{self._threshold_text()}　｜　已重新判定"
            )
            self.label_threshold.style().unpolish(self.label_threshold)
            self.label_threshold.style().polish(self.label_threshold)
            self.btn_export.setEnabled(True)
            self._save_config()
            self.statusBar().showMessage("重新判定完成", 4000)
        finally:
            QApplication.restoreOverrideCursor()

    def _reset_statistics(self, total=0):
        self.stat_cards["total"].set_value(total)
        self.stat_cards["ok"].set_value(0, "#147a47")
        self.stat_cards["ng"].set_value(0, "#b52e35")
        self.stat_cards["bright_low"].set_value(0, "#a15c00")
        self.stat_cards["bright_high"].set_value(0, "#be354b")
        self.stat_cards["sharp_low"].set_value(0, "#8b5a00")
        self.stat_cards["invalid"].set_value(0, "#7750a8")
        self.stat_cards["rate"].set_value("0.00%", "#b52e35")

    def _update_statistics_from_current_result(self):
        total = len(self.raw_df)
        total_result = self.raw_df["新總判定"].astype(str).str.strip().str.upper()
        bright_result = self.raw_df["新亮度判定"].astype(str).str.strip()
        sharp_result = self.raw_df["新銳利判定"].astype(str).str.strip()

        ok_cnt = int((total_result == "OK").sum())
        ng_cnt = int((total_result == "NG").sum())
        bright_low = int((bright_result == "下限超規").sum())
        bright_high = int((bright_result == "上限超規").sum())
        sharp_low = int((sharp_result == "下限超規").sum())
        invalid = int(
            (
                (bright_result == "數值無效")
                | (sharp_result == "數值無效")
            ).sum()
        )
        rate = ng_cnt / total * 100 if total else 0

        self.stat_cards["total"].set_value(total)
        self.stat_cards["ok"].set_value(ok_cnt, "#147a47")
        self.stat_cards["ng"].set_value(ng_cnt, "#b52e35")
        self.stat_cards["bright_low"].set_value(bright_low, "#a15c00")
        self.stat_cards["bright_high"].set_value(bright_high, "#be354b")
        self.stat_cards["sharp_low"].set_value(sharp_low, "#8b5a00")
        self.stat_cards["invalid"].set_value(invalid, "#7750a8")
        self.stat_cards["rate"].set_value(f"{rate:.2f}%", "#b52e35")

    # --------------------------------------------------------
    # 搜尋、排序與 CSV 導覽
    # --------------------------------------------------------
    def _filter_csv_list(self, 關鍵字):
        關鍵字 = str(關鍵字 or "").strip().lower()
        顯示數量 = 0
        for i in range(self.csv_list.count()):
            項目 = self.csv_list.item(i)
            顯示 = 關鍵字 in 項目.text().lower()
            項目.setHidden(not 顯示)
            if 顯示:
                顯示數量 += 1
        self.label_csv_count.setText(
            f"{顯示數量} / {self.csv_list.count()} 個"
            if 關鍵字
            else f"{self.csv_list.count()} 個"
        )

    def _filter_table(self, 關鍵字):
        self.table_proxy.set_text_filter(關鍵字)

    def _filter_table_result(self, 條件):
        self.table_proxy.set_result_filter(條件)

    def _focus_table_search(self):
        self.table_search.setFocus()
        self.table_search.selectAll()

    def _select_previous_csv(self):
        self._move_csv_selection(-1)

    def _select_next_csv(self):
        self._move_csv_selection(1)

    def _move_csv_selection(self, 步數):
        if self.csv_list.count() == 0:
            return
        目前 = self.csv_list.currentRow()
        if 目前 < 0:
            目前 = 0 if 步數 > 0 else self.csv_list.count() - 1
        else:
            目前 = max(0, min(self.csv_list.count() - 1, 目前 + 步數))

        while 0 <= 目前 < self.csv_list.count() and self.csv_list.item(目前).isHidden():
            目前 += 步數
        if 0 <= 目前 < self.csv_list.count():
            self.csv_list.setCurrentRow(目前)
            項目 = self.csv_list.item(目前)
            self.csv_list.scrollToItem(項目)
            self._process_csv(項目.data(Qt.UserRole))

    def _source_row_from_proxy_index(self, proxy_index):
        if not proxy_index.isValid():
            return None
        source_index = self.table_proxy.mapToSource(proxy_index)
        return self.table_model.dataframe_index_at(source_index.row())

    def _current_dataframe_index(self):
        return self._source_row_from_proxy_index(self.table_view.currentIndex())

    # --------------------------------------------------------
    # ROI 與圖片
    # --------------------------------------------------------
    def _resolve_image_path(self, path_value):
        path = str(path_value or "").strip().strip('"')
        if not path:
            return ""
        path = os.path.expandvars(os.path.expanduser(path))
        if os.path.isfile(path):
            return os.path.abspath(path)
        if self.current_csv_path:
            候選 = os.path.join(os.path.dirname(self.current_csv_path), path)
            if os.path.isfile(候選):
                return os.path.abspath(候選)
        return os.path.abspath(path) if os.path.isabs(path) else path

    def _monitor_roi_from_row(self, row_data):
        return self._brightness_roi_from_row(row_data)

    def _brightness_roi_from_row(self, row_data):
        if self.has_brightness_roi:
            roi = 從欄位取ROI(row_data, self.BRIGHTNESS_ROI_COLUMNS)
            if ROI有效(roi):
                return roi
        if self.has_legacy_roi:
            roi = 從欄位取ROI(row_data, self.LEGACY_ROI_COLUMNS)
            if ROI有效(roi):
                return roi
        return dict(self.brightness_roi)

    def _sharpness_roi_from_row(self, row_data):
        if self.has_sharpness_roi:
            roi = 從欄位取ROI(row_data, self.SHARPNESS_ROI_COLUMNS)
            if ROI有效(roi):
                return roi
        if self.has_legacy_roi:
            roi = 從欄位取ROI(row_data, self.LEGACY_ROI_COLUMNS)
            if ROI有效(roi):
                return roi
        return dict(self.sharpness_roi)

    def _get_selected_monitor_roi(self):
        if self.is_loaded:
            資料索引 = self._current_dataframe_index()
            if 資料索引 is not None:
                return self._monitor_roi_from_row(self.raw_df.loc[資料索引])
        return dict(self.brightness_roi)

    def manual_view_roi(self):
        起始路徑 = self.current_csv_dir or str(APP_DIR)
        img_path, _ = QFileDialog.getOpenFileName(
            self,
            "選取圖片檔",
            起始路徑,
            "圖片檔 (*.png *.jpg *.jpeg *.bmp *.tiff *.tif *.webp)",
        )
        if not img_path:
            return

        預設 = self._get_selected_monitor_roi()
        dialog = ROIInputDialog(
            預設.get("x", 0),
            預設.get("y", 0),
            預設.get("w", 0),
            預設.get("h", 0),
            self,
        )
        if dialog.exec() != QDialog.Accepted:
            return

        x, y, w, h = dialog.get_values()
        manual_roi = {"x": x, "y": y, "w": w, "h": h}
        if not ROI有效(manual_roi):
            QMessageBox.warning(self, "ROI 無效", "ROI 寬度與高度必須大於 0。")
            return

        self._show_roi_window(
            img_path,
            manual_roi,
            self.pattern_roi,
            {
                "檔名": os.path.basename(img_path),
                "完整路徑": img_path,
                "類型": "手動選圖",
            },
            sharpness_roi=manual_roi,
        )

    def view_current_roi(self):
        if not self.is_loaded:
            QMessageBox.warning(self, "尚未載入", "請先載入 CSV 資料。")
            return

        資料索引 = self._current_dataframe_index()
        if 資料索引 is None:
            QMessageBox.information(self, "尚未選取", "請先點選表格中的一列資料。")
            return

        self._open_row_viewer(資料索引)

    def _open_row_viewer(self, 資料索引):
        if 資料索引 not in self.raw_df.index:
            return

        row_data = self.raw_df.loc[資料索引]
        img_path = self._resolve_image_path(row_data.get("完整路徑", ""))
        if not os.path.isfile(img_path):
            QMessageBox.warning(self, "找不到圖片", str(img_path))
            return

        brightness_roi = self._brightness_roi_from_row(row_data)
        sharpness_roi = self._sharpness_roi_from_row(row_data)
        if (
            not ROI有效(brightness_roi)
            and not ROI有效(sharpness_roi)
            and not ROI有效(self.pattern_roi)
        ):
            QMessageBox.warning(
                self,
                "沒有可用 ROI",
                "此列沒有有效的 ROI 欄位，Recipe 也未提供有效的定位、亮度或銳利度 ROI。",
            )
            return

        資訊 = row_data.to_dict()
        資訊["完整路徑"] = img_path
        self._show_roi_window(
            img_path,
            brightness_roi,
            self.pattern_roi,
            資訊,
            sharpness_roi=sharpness_roi,
        )

    def _show_roi_window(
        self,
        img_path,
        monitor_roi=None,
        pattern_roi=None,
        image_info=None,
        sharpness_roi=None,
    ):
        視窗 = ROIImageViewer(
            img_path,
            monitor_roi,
            pattern_roi,
            sharpness_roi=sharpness_roi,
            image_info=image_info,
            threshold_text=self._threshold_text(),
            recipe_name=self.recipe_name,
            parent=self,
        )
        視窗.destroyed.connect(
            lambda _obj=None, w=視窗: self._cleanup_roi_window(w)
        )
        self.roi_windows.append(視窗)
        視窗.show()

    def _cleanup_roi_window(self, 視窗):
        if 視窗 in self.roi_windows:
            self.roi_windows.remove(視窗)

    def open_image(self, index=None):
        """雙擊表格列：開啟含雙 ROI、數值與輸出功能的內建檢視器。"""
        if not self.is_loaded:
            return
        if index is None or not index.isValid():
            index = self.table_view.currentIndex()
        if index is None or not index.isValid():
            return

        self.table_view.selectRow(index.row())
        資料索引 = self._source_row_from_proxy_index(index)
        if 資料索引 is not None:
            self._open_row_viewer(資料索引)

    def _open_image_external(self, index=None):
        """以 Windows 預設程式開啟未加標註的原圖。"""
        if not self.is_loaded:
            return
        if index is None or not index.isValid():
            index = self.table_view.currentIndex()

        資料索引 = self._source_row_from_proxy_index(index)
        if 資料索引 is None:
            return

        path = self._resolve_image_path(self.raw_df.loc[資料索引, "完整路徑"])
        if os.path.isfile(path):
            QDesktopServices.openUrl(QUrl.fromLocalFile(path))
        else:
            QMessageBox.warning(self, "找不到圖片", str(path))

    # --------------------------------------------------------
    # 右鍵選單
    # --------------------------------------------------------
    def _show_table_menu(self, pos):
        index = self.table_view.indexAt(pos)
        if not index.isValid():
            return
        self.table_view.selectRow(index.row())

        menu = QMenu(self)
        檢視分析 = menu.addAction("檢視圖片、雙 ROI 與數值")
        看ROI = menu.addAction("檢視雙 ROI")
        系統開圖 = menu.addAction("使用系統開啟原圖")
        menu.addSeparator()
        複製路徑 = menu.addAction("複製完整路徑")
        開啟資料夾 = menu.addAction("開啟圖片所在資料夾")

        選擇 = menu.exec(self.table_view.viewport().mapToGlobal(pos))
        資料索引 = self._source_row_from_proxy_index(index)
        if 資料索引 is None:
            return

        路徑 = self._resolve_image_path(self.raw_df.loc[資料索引, "完整路徑"])
        if 選擇 == 檢視分析:
            self.open_image(index)
        elif 選擇 == 看ROI:
            self.view_current_roi()
        elif 選擇 == 系統開圖:
            self._open_image_external(index)
        elif 選擇 == 複製路徑:
            QApplication.clipboard().setText(str(路徑))
            self.statusBar().showMessage("已複製完整路徑", 3000)
        elif 選擇 == 開啟資料夾:
            資料夾 = os.path.dirname(路徑)
            if os.path.isdir(資料夾):
                QDesktopServices.openUrl(QUrl.fromLocalFile(資料夾))

    def _show_csv_list_menu(self, pos):
        item = self.csv_list.itemAt(pos)
        if item is None:
            return

        menu = QMenu(self)
        載入 = menu.addAction("載入此 CSV")
        開啟位置 = menu.addAction("開啟所在資料夾")
        複製路徑 = menu.addAction("複製 CSV 路徑")
        選擇 = menu.exec(self.csv_list.viewport().mapToGlobal(pos))

        path = str(item.data(Qt.UserRole))
        if 選擇 == 載入:
            self.csv_list.setCurrentItem(item)
            self._process_csv(path)
        elif 選擇 == 開啟位置:
            QDesktopServices.openUrl(QUrl.fromLocalFile(os.path.dirname(path)))
        elif 選擇 == 複製路徑:
            QApplication.clipboard().setText(path)
            self.statusBar().showMessage("已複製 CSV 路徑", 3000)

    # --------------------------------------------------------
    # 匯出與資料夾
    # --------------------------------------------------------
    def export_new_csv(self):
        if not self.is_loaded:
            QMessageBox.warning(self, "尚未載入", "請先載入 CSV 資料。")
            return
        if not self.result_ready:
            QMessageBox.warning(
                self,
                "尚未重新判定",
                "目前規格已變更或尚未計算，請先按「重新判定」。",
            )
            return

        原檔名 = Path(self.current_csv_path).stem if self.current_csv_path else "分析結果"
        預設路徑 = os.path.join(
            self.current_csv_dir,
            f"{原檔名}_重新規格結果.csv",
        )
        save_path, _ = QFileDialog.getSaveFileName(
            self,
            "匯出新判定報表",
            預設路徑,
            "CSV (*.csv)",
        )
        if not save_path:
            return
        if not save_path.lower().endswith(".csv"):
            save_path += ".csv"

        try:
            # 匯出 raw_df，保留原始 CSV 中所有未顯示欄位。
            self.raw_df.to_csv(save_path, encoding="utf-8-sig", index=False)
            QMessageBox.information(
                self,
                "匯出完成",
                f"報表已儲存：\n{save_path}",
            )
            self.statusBar().showMessage("CSV 報表匯出完成", 5000)
        except Exception as 錯誤:
            QMessageBox.critical(self, "匯出失敗", f"無法匯出 CSV：\n{錯誤}")

    def _open_current_csv_folder(self):
        路徑 = self.current_csv_dir or (
            os.path.dirname(self.current_csv_path) if self.current_csv_path else ""
        )
        if 路徑 and os.path.isdir(路徑):
            QDesktopServices.openUrl(QUrl.fromLocalFile(路徑))

    def _open_profile_folder(self):
        PROFILE_DIR.mkdir(parents=True, exist_ok=True)
        QDesktopServices.openUrl(QUrl.fromLocalFile(str(PROFILE_DIR)))

    def _open_recipe_folder(self):
        if self.current_recipe_path:
            資料夾 = os.path.dirname(self.current_recipe_path)
            if os.path.isdir(資料夾):
                QDesktopServices.openUrl(QUrl.fromLocalFile(資料夾))

    # --------------------------------------------------------
    # 拖放載入
    # --------------------------------------------------------
    def dragEnterEvent(self, event):
        if event.mimeData().hasUrls():
            event.acceptProposedAction()
        else:
            event.ignore()

    def dropEvent(self, event):
        urls = event.mimeData().urls()
        if not urls:
            return

        路徑 = urls[0].toLocalFile()
        if os.path.isdir(路徑):
            self._populate_csv_list(路徑)
            self._save_config()
            event.acceptProposedAction()
            return

        if os.path.isfile(路徑) and 路徑.lower().endswith(".csv"):
            self._load_single_csv_path(路徑)
            event.acceptProposedAction()
            return

        if os.path.isfile(路徑) and 路徑.lower().endswith(".json"):
            self._load_recipe_from_file(路徑)
            event.acceptProposedAction()
            return

        QMessageBox.information(
            self,
            "不支援的檔案",
            "請拖入 CSV、Cognex Recipe JSON 或包含 CSV 的資料夾。",
        )

    # --------------------------------------------------------
    # 關閉
    # --------------------------------------------------------
    def closeEvent(self, event):
        self._save_config()
        event.accept()


# ============================================================
# 程式入口
# ============================================================
if __name__ == "__main__":
    app = QApplication(sys.argv)
    app.setApplicationName("Lumasharp Cognex CSV Analyzer")
    app.setStyle("Fusion")
    app.setStyleSheet(APP_STYLESHEET)

    win = LumasharpCSVAnalyzer()
    win.show()
    sys.exit(app.exec())
