"""解析 Unity 场景里所有 RectTransform，按锚点/偏移逐层算出它们的“屏幕矩形”。

用途：不靠推理，直接看生成出来的 UI 元素实际占多少像素、在哪。
坐标统一换算到 1920×1080 设计分辨率（以左下为原点）。
"""
import re
import sys
import os

UI_W, UI_H = 1920.0, 1080.0


def parse_blocks(path):
    blocks = {}
    cur_id = cur_type = None
    buf = []
    with open(path, "r", encoding="utf-8", errors="replace") as f:
        for line in f:
            m = re.match(r"^--- !u!(\d+) &(\d+)", line)
            if m:
                if cur_id is not None:
                    blocks.setdefault(cur_type, {})[cur_id] = buf
                cur_type, cur_id, buf = m.group(1), m.group(2), []
            elif cur_id is not None:
                buf.append(line.rstrip("\n"))
    if cur_id is not None:
        blocks.setdefault(cur_type, {})[cur_id] = buf
    return blocks


def field(lines, key):
    for l in lines:
        m = re.match(r"^\s*" + re.escape(key) + r":\s*(.*)$", l)
        if m:
            return m.group(1)
    return None


def vec(s, default=(0.0, 0.0)):
    if not s:
        return default
    m = re.search(r"x:\s*(-?[\d.eE+-]+),\s*y:\s*(-?[\d.eE+-]+)", s)
    return (float(m.group(1)), float(m.group(2))) if m else default


def fnum(s, default=0.0):
    if s is None:
        return default
    try:
        return float(s)
    except ValueError:
        return default


def main(path):
    blocks = parse_blocks(path)
    gos = blocks.get("1", {})
    trs = blocks.get("224", {})
    if not trs:
        trs = blocks.get("4", {})

    # GameObject 名字
    names = {}
    for gid, lines in gos.items():
        names[gid] = (field(lines, "m_Name") or "").strip()

    info = {}
    for tid, lines in trs.items():
        go = field(lines, "m_GameObject")
        gov = re.search(r"(\d+)", go).group(1) if go else None

        father_raw = field(lines, "m_Father")
        father = None
        if father_raw:
            mf = re.search(r"(\d+)", father_raw)
            if mf:
                father = mf.group(1)

        info[tid] = dict(
            go=gov,
            name=names.get(gov, "?"),
            father=father,
            amin=vec(field(lines, "m_AnchorMin")),
            amax=vec(field(lines, "m_AnchorMax")),
            apos=vec(field(lines, "m_AnchoredPosition")),
            size=vec(field(lines, "m_SizeDelta")),
            pivot=vec(field(lines, "m_Pivot"), (0.5, 0.5)),
        )

    def rect(tid, depth=0, seen=None):
        if seen is None:
            seen = set()
        if tid in seen or depth > 12:
            return (0.0, 0.0, UI_W, UI_H)
        seen.add(tid)
        d = info.get(tid)
        if d is None:
            return (0.0, 0.0, UI_W, UI_H)

        f = d["father"]
        pr = rect(f, depth + 1, seen) if f and f in info else (0.0, 0.0, UI_W, UI_H)
        pw, ph = pr[2] - pr[0], pr[3] - pr[1]

        ax0, ay0 = d["amin"]
        ax1, ay1 = d["amax"]
        if abs(ax1 - ax0) < 1e-6:
            ax1 = ax0
        if abs(ay1 - ay0) < 1e-6:
            ay1 = ay0

        # 锚点在父矩形中的像素位置
        anc0x = pr[0] + ax0 * pw
        anc1x = pr[0] + ax1 * pw
        anc0y = pr[1] + ay0 * ph
        anc1y = pr[1] + ay1 * ph

        w = (anc1x - anc0x) + d["size"][0]
        h = (anc1y - anc0y) + d["size"][1]

        px, py = d["pivot"]
        cx = (anc0x + anc1x) * 0.5 + d["apos"][0]
        cy = (anc0y + anc1y) * 0.5 + d["apos"][1]

        x0 = cx - w * px
        y0 = cy - h * py
        return (x0, y0, x0 + w, y0 + h)

    rows = []
    for tid, d in info.items():
        r = rect(tid)
        rows.append((d["name"], d["go"], r))

    print(f"=== {os.path.basename(path)} ===")
    print(f"{'名称':<22} {'设计分辨率下的矩形 (x0,y0,x1,y1)':<40} {'尺寸':<14} 判定")
    print("-" * 108)
    for name, go, r in sorted(rows, key=lambda t: t[2][1]):
        w, h = r[2] - r[0], r[3] - r[1]
        if w < 0.5 or h < 0.5:
            continue
        inside = (r[0] >= -0.5 and r[1] >= -0.5 and r[2] <= UI_W + 0.5 and r[3] <= UI_H + 0.5)
        tag = "可见区内" if inside else "★超出可见区★"
        print(f"{name:<22} ({r[0]:7.1f},{r[1]:7.1f},{r[2]:7.1f},{r[3]:7.1f})  "
              f"{w:6.1f}x{h:<6.1f} {tag}")


if __name__ == "__main__":
    for p in sys.argv[1:]:
        main(p)
        print()
