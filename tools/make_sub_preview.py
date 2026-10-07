"""子界面抠图结果的棋盘格预览，用于核对是否误伤内部白色。"""
import os
from PIL import Image

BASE = r"D:\ugame\gamejam\Assets\Art"
OUT = r"D:\ugame\gamejam\tools\ui_preview"
JOBS = [
    (r"音乐设置-1等9项文件", "音乐设置-3_new.png", "sub_music_labels"),
    (r"音乐设置-1等9项文件", "音乐设置-4_new.png", "sub_music_track"),
    (r"音乐设置-1等9项文件", "音乐设置-5_new.png", "sub_music_knob"),
    (r"设置-1等5项文件", "设置-2_new.png", "sub_settings_row2"),
    (r"设置-1等5项文件", "设置-3_new.png", "sub_settings_row3"),
    (r"设置-1等5项文件", "设置-4_new.png", "sub_settings_row4"),
    (r"选关-1等10项文件", "选关-2_new.png", "sub_choose_2"),
    (r"选关-1等10项文件", "选关-9_new.png", "sub_choose_9"),
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
        comp = Image.alpha_composite(checker(im.size).convert("RGBA"), im)

        bbox = im.getbbox()
        if bbox:
            x0, y0, x1, y1 = bbox
            m = 30
            comp = comp.crop((max(0, x0 - m), max(0, y0 - m),
                              min(im.width, x1 + m), min(im.height, y1 + m)))
        if comp.width > 700:
            r = 700 / comp.width
            comp = comp.resize((700, max(1, int(comp.height * r))), Image.LANCZOS)
        dst = os.path.join(OUT, tag + ".png")
        comp.convert("RGB").save(dst, "PNG")
        print(f"{tag:<22} -> {comp.width}x{comp.height}")


if __name__ == "__main__":
    main()
