"""从图像边缘做洪水填充，抠掉与边缘连通的纯白背景。

为什么要"从边缘填充"而不是"把所有白像素变透明"：
  前者只删除外部背景，文字/线稿内部的封闭白区（例如"开"字笔画里的空隙）会被保留；
  后者会把字打穿。

输出：同文件夹下 原名_new.png，不改动原文件。
"""
import os
from collections import deque
from PIL import Image
import numpy as np

BASE = r"D:\ugame\gamejam\Assets\Art"
JOBS = {
    r"主界面-1等9项文件": ["主界面-2.png", "主界面-3.png", "主界面-5.png",
                          "主界面-6.png", "主界面-7.png", "主界面-8.png",
                          "主界面-9.png"],
    # 主界面-1 是最终背景，明确不抠
    r"音乐设置-1等9项文件": ["音乐设置-2.png"],
    r"设置-1等5项文件": ["设置-5.png"],
    r"选关-1等10项文件": ["选关-10.png"],
}

WHITE = 244          # RGB 三通道都 >= 该值视为白
ALPHA_TOL = 4        # 与原图 alpha 的容差


def remove_edge_white(path, out_path, white_thr=WHITE):
    im = Image.open(path).convert("RGBA")
    a = np.array(im)
    h, w = a.shape[0], a.shape[1]

    rgb = a[:, :, :3].astype(np.int16)
    alpha = a[:, :, 3]

    is_white = ((rgb[:, :, 0] >= white_thr) &
                (rgb[:, :, 1] >= white_thr) &
                (rgb[:, :, 2] >= white_thr))

    visited = np.zeros((h, w), dtype=bool)
    q = deque()

    # 把四条边上所有白像素作为种子
    for x in range(w):
        for y in (0, h - 1):
            if is_white[y, x] and not visited[y, x]:
                visited[y, x] = True
                q.append((y, x))
    for y in range(h):
        for x in (0, w - 1):
            if is_white[y, x] and not visited[y, x]:
                visited[y, x] = True
                q.append((y, x))

    # 四连通洪水填充
    while q:
        y, x = q.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and not visited[ny, nx] and is_white[ny, nx]:
                visited[ny, nx] = True
                q.append((ny, nx))

    removed = int(visited.sum())
    a[:, :, 3] = np.where(visited, 0, alpha)
    Image.fromarray(a, "RGBA").save(out_path, "PNG", optimize=True)

    pct = 100.0 * removed / (w * h)
    ys, xs = np.where(~visited)
    bbox = (int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1) if removed else None
    return dict(size=(w, h), removed_pct=pct, bbox=bbox)


def main():
    print(f"{'文件':<20} {'尺寸':<12} {'删除背景占比':<12} 透明后内容包围盒")
    print("-" * 84)
    for sub, files in JOBS.items():
        folder = os.path.join(BASE, sub)
        for f in files:
            src = os.path.join(folder, f)
            if not os.path.isfile(src):
                print(f"{f:<20} 不存在: {src}")
                continue
            stem, ext = os.path.splitext(f)
            dst = os.path.join(folder, stem + "_new" + ext)
            r = remove_edge_white(src, dst)
            print(f"{f:<20} {r['size'][0]}x{r['size'][1]:<7} {r['removed_pct']:>9.2f}%   {r['bbox']}")
    print("\n完成。已生成 _new 文件，原文件未改动。")


if __name__ == "__main__":
    main()
