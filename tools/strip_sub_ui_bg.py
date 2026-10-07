"""抠掉三个子界面素材的白底（边缘洪水填充，保留笔画内部封闭白区）。

输出同文件夹下的 原名_new.png，不改动原文件。
"""
import os
from collections import deque
from PIL import Image
import numpy as np

BASE = r"D:\ugame\gamejam\Assets\Art"

# 各子界面需要抠图的素材（背景 -1 一律不抠）
JOBS = {
    r"音乐设置-1等9项文件": ["音乐设置-2.png", "音乐设置-3.png", "音乐设置-4.png",
                            "音乐设置-5.png", "音乐设置-6.png", "音乐设置-7.png",
                            "音乐设置-8.png", "音乐设置-9.png"],
    r"设置-1等5项文件":     ["设置-2.png", "设置-3.png", "设置-4.png", "设置-5.png"],
    r"选关-1等10项文件":    ["选关-2.png", "选关-3.png", "选关-4.png", "选关-5.png",
                            "选关-6.png", "选关-7.png", "选关-8.png", "选关-9.png",
                            "选关-10.png"],
}

WHITE = 244


def remove_edge_white(path, out_path, white_thr=WHITE):
    im = Image.open(path).convert("RGBA")
    a = np.array(im)
    h, w = a.shape[:2]

    rgb = a[:, :, :3].astype(np.int16)
    alpha = a[:, :, 3]
    is_white = ((rgb[:, :, 0] >= white_thr) &
                (rgb[:, :, 1] >= white_thr) &
                (rgb[:, :, 2] >= white_thr))

    visited = np.zeros((h, w), dtype=bool)
    q = deque()
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

    ys, xs = np.where(~visited)
    bbox = (int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1) if removed else None
    return 100.0 * removed / (w * h), bbox


def main():
    print(f"{'文件':<22} {'删除背景':<10} 内容包围盒")
    print("-" * 72)
    for sub, files in JOBS.items():
        folder = os.path.join(BASE, sub)
        for f in files:
            src = os.path.join(folder, f)
            if not os.path.isfile(src):
                print(f"{f:<22} 不存在")
                continue
            stem, ext = os.path.splitext(f)
            dst = os.path.join(folder, stem + "_new" + ext)
            pct, bbox = remove_edge_white(src, dst)
            print(f"{f:<22} {pct:>7.2f}%   {bbox}")
    print("\n完成。")


if __name__ == "__main__":
    main()
