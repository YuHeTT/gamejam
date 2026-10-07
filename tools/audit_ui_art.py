"""重新核对：四组 UI 素材的真实像素尺寸 + 各元素包围盒，
并把包围盒换算成 1920x1080 参考分辨率下的坐标（用于 Layout 生成）。

之前的假设"全部 2500x1500"只对主界面成立，必须逐张实测。
"""
import os
from PIL import Image
import numpy as np

BASE = r"D:\ugame\gamejam\Assets\Art"
GROUPS = {
    "主界面": r"主界面-1等9项文件",
    "音乐设置": r"音乐设置-1等9项文件",
    "设置": r"设置-1等5项文件",
    "选关": r"选关-1等10项文件",
}
WHITE = 244


def content_bbox(path):
    im = Image.open(path).convert("RGBA")
    a = np.array(im)
    h, w = a.shape[:2]
    rgb = a[:, :, :3].astype(np.int16)
    alpha = a[:, :, 3]
    white = (rgb[:, :, 0] >= WHITE) & (rgb[:, :, 1] >= WHITE) & (rgb[:, :, 2] >= WHITE)
    nonwhite = ~white
    if not nonwhite.any():
        return (w, h), None
    ys, xs = np.where(nonwhite)
    return (w, h), (int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1)


def main():
    print("=" * 100)
    for label, sub in GROUPS.items():
        folder = os.path.join(BASE, sub)
        files = sorted([f for f in os.listdir(folder) if f.lower().endswith(".png")],
                       key=lambda s: (len(s), s))
        print(f"\n### {label}  ({sub})")
        for f in files:
            p = os.path.join(folder, f)
            (w, h), bb = content_bbox(p)
            ratio = f"{w / h:.4f}" if h else "-"
            bb_txt = "全幅（无白边）" if bb is None else str(bb)
            print(f"  {f:<22} {w}x{h}  ratio={ratio}  bbox={bb_txt}")


if __name__ == "__main__":
    main()
