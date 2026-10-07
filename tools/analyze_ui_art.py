import os, sys
from PIL import Image
import numpy as np

base = r"D:\ugame\gamejam\Assets\Art"
folders = {
    "主界面": os.path.join(base, "主界面-1等9项文件"),
    "音乐设置": os.path.join(base, "音乐设置-1等9项文件"),
    "设置": os.path.join(base, "设置-1等5项文件"),
    "选关": os.path.join(base, "选关-1等10项文件"),
}

def analyze(path, white_thr=245):
    im = Image.open(path).convert("RGBA")
    a = np.array(im)
    h, w = a.shape[0], a.shape[1]
    rgb = a[:, :, :3].astype(np.int16)
    alpha = a[:, :, 3]
    # 纯白像素：RGB 都 >= thr 且 alpha > 0
    is_white = (rgb[:, :, 0] >= white_thr) & (rgb[:, :, 1] >= white_thr) & (rgb[:, :, 2] >= white_thr)
    has_alpha = bool((alpha < 250).any())
    nonwhite = ~is_white
    if nonwhite.any():
        ys, xs = np.where(nonwhite)
        bbox = (int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1)
    else:
        bbox = None
    white_pct = 100.0 * is_white.sum() / (w * h)
    return dict(size=(w, h), has_alpha=has_alpha, white_pct=white_pct, bbox=bbox, nonwhite=int(nonwhite.sum()))

print(f"{'文件夹':<8} {'文件':<16} {'尺寸':<12} {'白底占比':<9} {'已有透明':<8} 非白内容包围盒(x0,y0,x1,y1)")
print("-" * 110)
for label, folder in folders.items():
    if not os.path.isdir(folder):
        print(f"{label}: 目录不存在 {folder}")
        continue
    files = sorted([f for f in os.listdir(folder) if f.lower().endswith((".png", ".jpg", ".jpeg"))],
                   key=lambda s: (len(s), s))
    for f in files:
        p = os.path.join(folder, f)
        try:
            r = analyze(p)
        except Exception as e:
            print(f"{label:<8} {f:<16} 读取失败: {e}")
            continue
        size = f"{r['size'][0]}x{r['size'][1]}"
        bb = "无（全白）" if r['bbox'] is None else str(r['bbox'])
        print(f"{label:<8} {f:<16} {size:<12} {r['white_pct']:>6.1f}%  {str(r['has_alpha']):<8} {bb}")
