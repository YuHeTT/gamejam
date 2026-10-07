"""把 _new 抠图结果叠在棋盘格上生成预览图，用于肉眼核对：
   - 背景是否干净（是否残白）
   - 文字/线稿内部的白色是否被保留（笔画是否被打穿）
"""
import os
from PIL import Image

BASE = r"D:\ugame\gamejam\Assets\Art"
OUT = r"D:\ugame\gamejam\tools\ui_preview"
JOBS = [
    (r"主界面-1等9项文件", "主界面-2_new.png", "icon_music"),
    (r"主界面-1等9项文件", "主界面-3_new.png", "icon_settings"),
    (r"主界面-1等9项文件", "主界面-5_new.png", "text_quit"),
    (r"主界面-1等9项文件", "主界面-6_new.png", "text_start"),
    (r"主界面-1等9项文件", "主界面-7_new.png", "title"),
    (r"主界面-1等9项文件", "主界面-8_new.png", "deco_gourd"),
    (r"主界面-1等9项文件", "主界面-9_new.png", "deco_cat"),
    (r"音乐设置-1等9项文件", "音乐设置-2_new.png", "back_music"),
    (r"设置-1等5项文件", "设置-5_new.png", "back_settings"),
    (r"选关-1等10项文件", "选关-10_new.png", "back_choose"),
]


def checker(size, cell=24):
    w, h = size
    img = Image.new("RGB", (w, h), (200, 200, 200))
    px = img.load()
    for y in range(h):
        for x in range(w):
            if ((x // cell) + (y // cell)) % 2 == 0:
                px[x, y] = (150, 150, 150)
    return img


def main():
    os.makedirs(OUT, exist_ok=True)
    for sub, fname, tag in JOBS:
        src = os.path.join(BASE, sub, fname)
        if not os.path.isfile(src):
            print("缺少:", src)
            continue
        im = Image.open(src).convert("RGBA")
        bg = checker(im.size).convert("RGBA")
        comp = Image.alpha_composite(bg, im)

        # 裁到内容范围（带 20px 边距）再缩小，方便看细节
        bbox = im.getbbox()
        if bbox:
            x0, y0, x1, y1 = bbox
            m = 30
            box = (max(0, x0 - m), max(0, y0 - m),
                   min(im.width, x1 + m), min(im.height, y1 + m))
            comp = comp.crop(box)
        if comp.width > 700:
            r = 700 / comp.width
            comp = comp.resize((700, max(1, int(comp.height * r))), Image.LANCZOS)
        dst = os.path.join(OUT, tag + ".png")
        comp.convert("RGB").save(dst, "PNG")
        print(f"{tag:<16} -> {dst}  ({comp.width}x{comp.height})")


if __name__ == "__main__":
    main()
