# -*- coding: utf-8 -*-
"""±9 プレイ画面イメージ(モックアップ)を生成して mockup.png に出力。(新ルール)"""
import os
from PIL import Image, ImageDraw, ImageFont

W, H = 1680, 1120
BG      = (18, 24, 38)        # 濃紺背景
PANEL   = (30, 40, 60)
PANEL2  = (38, 50, 74)
BLUE    = (54, 122, 220)
BLUE_D  = (32, 78, 150)
RED     = (214, 64, 72)
RED_D   = (150, 36, 44)
GOLD    = (226, 180, 60)
GOLD_D  = (160, 120, 28)
WHITE   = (236, 240, 248)
GREY    = (150, 162, 184)
GREEN   = (88, 196, 130)
BACK    = (44, 58, 86)        # カード裏面

img = Image.new("RGB", (W, H), BG)
d = ImageDraw.Draw(img)

# フォントは環境差を吸収(Windows=游ゴシック / Linux=IPAゴシック等にフォールバック)
_FONT_CANDIDATES = {
    True: ["C:/Windows/Fonts/YuGothB.ttc",
           "/usr/share/fonts/opentype/ipafont-gothic/ipagp.ttf",
           "/usr/share/fonts/truetype/fonts-japanese-gothic.ttf"],
    False: ["C:/Windows/Fonts/YuGothM.ttc",
            "/usr/share/fonts/opentype/ipafont-gothic/ipag.ttf",
            "/usr/share/fonts/truetype/fonts-japanese-gothic.ttf"],
}

def F(sz, bold=False):
    for path in _FONT_CANDIDATES[bool(bold)]:
        if os.path.exists(path):
            try:
                return ImageFont.truetype(path, sz)
            except Exception:
                pass
    return ImageFont.load_default()

def rr(xy, r, fill, outline=None, width=2):
    d.rounded_rectangle(xy, radius=r, fill=fill, outline=outline, width=width)

def ctext(cx, y, s, font, fill, anchor="mm"):
    d.text((cx, y), s, font=font, fill=fill, anchor=anchor)

# ---- 上部タイトルバー ----
rr((0, 0, W, 70), 0, PANEL)
d.text((28, 35), "±9  プラマイ・ナイン", font=F(34, True), fill=GOLD, anchor="lm")
d.text((W-28, 35), "対戦中  /  Turn 4", font=F(24), fill=GREY, anchor="rm")

# ---- HPバー描画関数(0〜200, 100中心) ----
def hp_bar(x, y, w, h, val, label, danger_hi=False):
    rr((x, y, x+w, y+h), h//2, PANEL2, outline=(70,84,112), width=2)
    # 中央100ライン
    midx = x + w*100/200
    d.line((midx, y+4, midx, y+h-4), fill=GREY, width=2)
    # 値ゲージ(100からの差分)。HP非公開:色は＋寄り=青/−寄り=赤、数値は出さない
    px = x + w*val/200
    col = BLUE if val >= 100 else RED
    if val >= 100:
        rr((midx, y+5, px, y+h-5), (h-10)//2, col)
    else:
        rr((px, y+5, midx, y+h-5), (h-10)//2, col)
    d.text((x, y-10), label, font=F(22, True), fill=WHITE, anchor="lb")
    d.text((x+w, y-10), "HP非公開(ゲージ＋色のみ)", font=F(16), fill=GREY, anchor="rb")
    d.text((x-4, y+h+6), "0 敗北", font=F(15), fill=RED, anchor="lt")
    d.text((x+w+4, y+h+6), "200 敗北", font=F(15), fill=RED, anchor="rt")

# ---- カード描画 ----
def card(x, y, w, h, kind, txt, face_up=True):
    if not face_up:
        rr((x, y, x+w, y+h), 10, BACK, outline=(90,104,140), width=2)
        # 裏面マーク
        d.text((x+w/2, y+h/2), "±9", font=F(int(h*0.26), True), fill=(90,104,140), anchor="mm")
        return
    col = {"blue":(BLUE,BLUE_D), "red":(RED,RED_D), "gold":(GOLD,GOLD_D)}[kind]
    rr((x, y, x+w, y+h), 10, col[0], outline=col[1], width=3)
    rr((x+5, y+5, x+w-5, y+h-5), 7, None, outline=(255,255,255,60), width=1)
    fcol = (40,30,0) if kind=="gold" else WHITE
    d.text((x+w/2, y+h/2), txt, font=F(int(h*0.30), True), fill=fcol, anchor="mm")

# ================= 相手エリア =================
rr((24, 90, 1180, 250), 14, PANEL, outline=(54,68,96), width=2)
hp_bar(60, 140, 760, 30, 128, "相手プレイヤー")
# 相手の伏せカード(前半2枚以上+後半1枚以上・最大5枚)
ox = 880
d.text((ox, 118), "場(伏せ)", font=F(18), fill=GREY, anchor="lm")
for i in range(5):
    card(ox + i*58, 138, 50, 74, "blue", "", face_up=False)
# 相手の持ち込みアイテム(開始時公開)
d.text((60, 212), "相手の持ち込み(公開):", font=F(15), fill=GREY, anchor="lm")
d.text((300, 212), "① コイン反転", font=F(16, True), fill=GOLD, anchor="lm")

# ================= 中央フィールド =================
rr((24, 268, 1180, 600), 14, PANEL2, outline=(60,76,108), width=2)
ctext(602, 296, "対戦フィールド", F(20, True), GREY)

# --- コイントス ---
coin_cx, coin_cy = 602, 430
d.ellipse((coin_cx-78, coin_cy-78, coin_cx+78, coin_cy+78), fill=GOLD, outline=GOLD_D, width=5)
d.ellipse((coin_cx-60, coin_cy-60, coin_cx+60, coin_cy+60), outline=GOLD_D, width=2)
ctext(coin_cx, coin_cy-14, "奇", F(54, True), (60,44,0))
ctext(coin_cx, coin_cy+34, "ODD", F(20, True), (90,70,0))
ctext(coin_cx, coin_cy+108, "コイントス(共通)", F(20, True), WHITE)
ctext(coin_cx, coin_cy+138, "ラウンド頭に公開(当たり偶奇)", F(16), GREY)

# --- 左:相手の出し札 / 右:自分の出し札(前半・後半) ---
def phase_slot(x, y, label, cards):
    d.text((x, y-26), label, font=F(17), fill=GREY, anchor="lm")
    for i,(k,t) in enumerate(cards):
        card(x + i*60, y, 52, 76, k, t)

phase_slot(120, 330, "相手 前半", [("blue","7"),("blue","9"),("red","×2")])
phase_slot(120, 470, "相手 後半", [("blue","-3"),("blue","6")])
phase_slot(770, 330, "あなた 前半", [("blue","9"),("blue","9"),("red","×3")])
phase_slot(770, 470, "あなた 後半", [("blue","8"),("blue","-2")])

# 計算結果の吹き出し
rr((360, 560, 844, 600), 12, (24,32,50), outline=GREEN, width=2)
ctext(602, 580, "あなたの最終合計 = (9+9)×3 + (8-2) = 60  →  偶数=コインと不一致 → 自分へ", F(17, True), GREEN)

# ================= 自分エリア =================
rr((24, 618, 1180, 1090), 14, PANEL, outline=(54,68,96), width=2)
hp_bar(60, 668, 760, 30, 92, "あなた")

# --- アイテム & コスト(自分・開始時公開) ---
rr((852, 660, 1168, 752), 10, PANEL2, outline=(70,84,112), width=2)
d.text((868, 678), "持ち込みアイテム(公開)", font=F(13), fill=GREY, anchor="lm")
d.text((868, 702), "② 符号反転(向き)", font=F(18, True), fill=GOLD, anchor="lm")
d.text((868, 731), "チャージ", font=F(15), fill=WHITE, anchor="lm")
cgx = 960
for i in range(3):
    rr((cgx + i*24, 722, cgx + i*24 + 18, 742), 4, GREEN, outline=(70,84,112), width=1)
d.text((cgx + 3*24 + 8, 731), "3 / 3  発動可", font=F(15, True), fill=GREEN, anchor="lm")

# 手札
d.text((60, 740), "手札", font=F(20, True), fill=WHITE, anchor="lm")
hand = [("blue","9"),("blue","-5"),("blue","2"),("red","×-1"),("red","±5"),("gold","±9")]
hx = 60
for i,(k,t) in enumerate(hand):
    y = 770 + (8 if i%2 else 0)
    card(hx + i*112, y, 96, 138, k, t)
d.text((60, 940), "ドラッグ&ドロップで場に配置(前半2枚以上→後半1枚以上) / チャージでコストを貯めてアイテム発動", font=F(17), fill=GREY, anchor="lm")

# 凡例
ly = 1010
def legend(x, col, label):
    rr((x, ly, x+26, ly+26), 6, col)
    d.text((x+34, ly+13), label, font=F(18), fill=WHITE, anchor="lm")
legend(60,  BLUE, "数字 -9〜9")
legend(280, RED,  "特殊 ×N / ±5")
legend(560, GOLD, "±9(切り札)")
legend(820, BACK, "伏せ札(種別不可視)")

# ================= 右:カード残数トラッカー =================
rr((1204, 90, 1656, 1090), 14, PANEL2, outline=(60,76,108), width=2)
ctext(1430, 122, "カード残数 一覧", F(24, True), WHITE)
ctext(1430, 152, "クリックで明暗トグル", F(16), GREY)

# 数字グリッド -9〜9
gx, gy = 1228, 190
cw, ch, gap = 40, 34, 4
nums = list(range(-9, 10))
used = {-9, -5, 7, 9, 0, 3}   # 例:使用済み(暗)
for idx, n in enumerate(nums):
    col = idx % 5
    row = idx // 5
    x = gx + col*(cw+gap)
    y = gy + row*(ch+gap)
    on = n not in used
    fill = BLUE if on else (40,48,68)
    rr((x, y, x+cw, y+ch), 6, fill, outline=BLUE_D, width=1)
    d.text((x+cw/2, y+ch/2), str(n), font=F(17, True),
           fill=WHITE if on else (90,100,124), anchor="mm")

# レッド/ゴールド
ry = gy + 4*(ch+gap) + 24
ctext(1430, ry, "── 特殊カード ──", F(16), GREY)
reds = ["×2","×-2","×3","×-3","×-1","±5","±9"]
rused = {"×-2","±5"}
ry2 = ry + 26
for i, r in enumerate(reds):
    col = i % 4
    row = i // 4
    x = gx + col*(58+gap)
    y = ry2 + row*(40+gap)
    on = r not in rused
    isg = (r == "±9")
    base = GOLD if isg else RED
    fill = base if on else (52,40,40)
    rr((x, y, x+58, y+36), 7, fill, outline=GOLD_D if isg else RED_D, width=2)
    d.text((x+29, y+18), r, font=F(17, True),
           fill=(40,30,0) if (on and isg) else (WHITE if on else (108,90,90)), anchor="mm")

# デッキ残数
ry3 = ry2 + 2*44 + 30
rr((1228, ry3, 1632, ry3+120), 12, PANEL, outline=(70,84,112), width=2)
ctext(1430, ry3+30, "山札 共通デッキ", F(20, True), WHITE)
ctext(1430, ry3+66, "残 88 / 115 枚", F(26, True), GOLD)
ctext(1430, ry3+98, "ブルー95 + レッド19 + ゴールド1", F(15), GREY)

img.save("mockup.png")
print("saved mockup.png", img.size)
