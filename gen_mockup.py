# -*- coding: utf-8 -*-
"""±9 プレイ画面イメージ(モックアップ)を生成して mockup.png に出力。(新ルール / 手描きレイアウト版)

レイアウトは手描きスケッチを参照:
- 左右に「縦型HPゲージ＋金色の台座リング」(HP非公開=ゲージ＋色のみ。＋寄り=青/−寄り=赤)
- 中央に大きなコイン＋送り先を示す矢印
- 対戦カードは中央の上(相手)・下(あなた)に前半/後半で配置
- 手札は最下段の横一列
- 右側に「カード残数トラッカー / 山札 / 持ち込みアイテム＋チャージ」
"""
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

# ---- カード描画 ----
def card(x, y, w, h, kind, txt, face_up=True):
    if not face_up:
        rr((x, y, x+w, y+h), 10, BACK, outline=(90,104,140), width=2)
        d.text((x+w/2, y+h/2), "±9", font=F(int(h*0.26), True), fill=(90,104,140), anchor="mm")
        return
    col = {"blue":(BLUE,BLUE_D), "red":(RED,RED_D), "gold":(GOLD,GOLD_D)}[kind]
    rr((x, y, x+w, y+h), 10, col[0], outline=col[1], width=3)
    rr((x+5, y+5, x+w-5, y+h-5), 7, None, outline=(255,255,255,60), width=1)
    fcol = (40,30,0) if kind=="gold" else WHITE
    d.text((x+w/2, y+h/2), txt, font=F(int(h*0.30), True), fill=fcol, anchor="mm")

# ---- 縦型HPゲージ(台座リング付き)----
# HP非公開:数値は出さず、ゲージ＋色(＋寄り=青/−寄り=赤)のみ。
def vgauge(cx, top, bottom, val, who):
    w = 60
    x0, x1 = cx - w/2, cx + w/2
    h = bottom - top
    rr((x0, top, x1, bottom), w//2, PANEL2, outline=(70,84,112), width=2)
    # 中央(100=開始)ライン
    mid_y = bottom - h * 100/200
    d.line((x0+5, mid_y, x1-5, mid_y), fill=GREY, width=2)
    # 値ゲージ(色=どちらの端に寄っているか)
    col = BLUE if val >= 100 else RED
    fy = bottom - h * val/200
    if val >= 100:
        rr((x0+6, fy, x1-6, mid_y), (w-12)//2, col)
    else:
        rr((x0+6, mid_y, x1-6, fy), (w-12)//2, col)
    # 金色の台座リング(スケッチ参照)
    d.ellipse((cx-52, bottom-6, cx+52, bottom+34), outline=GOLD, width=6)
    d.ellipse((cx-34, bottom+4, cx+34, bottom+26), outline=GOLD_D, width=2)
    # ラベル
    ctext(cx, top-30, who, F(22, True), WHITE)
    ctext(cx, top-8, "HP非公開", F(13), GREY)
    ctext(cx, bottom+54, "0 敗北", F(14), RED)
    ctext(cx, top+14, "200", F(13), GREY)

# ================= 上部タイトルバー =================
rr((0, 0, W, 70), 0, PANEL)
d.text((28, 35), "±9  プラマイ・ナイン", font=F(34, True), fill=GOLD, anchor="lm")
d.text((W-28, 35), "対戦中  /  Turn 4", font=F(24), fill=GREY, anchor="rm")

# ================= 左右の縦型HPゲージ =================
vgauge(cx=82,   top=170, bottom=980, val=92,  who="あなた")        # ＜100 → 赤(−寄り)
vgauge(cx=1598, top=170, bottom=980, val=128, who="相手(CPU)")     # ＞100 → 青(＋寄り)

# ================= 中央フィールド枠 =================
CX0, CX1 = 175, 1065
rr((CX0, 90, CX1, 1000), 16, PANEL, outline=(54,68,96), width=2)

# --- 相手の持ち込み(上部ストリップ) ---
d.text((CX0+24, 118), "相手の持ち込み(公開):", font=F(15), fill=GREY, anchor="lm")
d.text((CX0+250, 118), "① コイン反転", font=F(17, True), fill=GOLD, anchor="lm")

# --- カード配置ヘルパー(前半/後半) ---
def phase_slot(x, y, label, cards):
    d.text((x, y-24), label, font=F(16), fill=GREY, anchor="lm")
    for i, (k, t) in enumerate(cards):
        card(x + i*58, y, 50, 74, k, t)

# --- 相手の出し札(中央フィールドの上側) ---
phase_slot(220, 200, "相手 前半", [("blue","7"),("blue","9"),("red","×2")])
phase_slot(470, 200, "相手 後半", [("blue","-3"),("blue","6")])
# 相手の伏せ札
d.text((700, 176), "場(伏せ)", font=F(14), fill=GREY, anchor="lm")
for i in range(3):
    card(700 + i*56, 200, 48, 74, "blue", "", face_up=False)

# --- 中央コイン ---
coin_cx, coin_cy = 620, 470
d.ellipse((coin_cx-74, coin_cy-74, coin_cx+74, coin_cy+74), fill=GOLD, outline=GOLD_D, width=5)
d.ellipse((coin_cx-56, coin_cy-56, coin_cx+56, coin_cy+56), outline=GOLD_D, width=2)
ctext(coin_cx, coin_cy-12, "奇", F(50, True), (60,44,0))
ctext(coin_cx, coin_cy+30, "ODD", F(18, True), (90,70,0))
ctext(coin_cx, coin_cy+96, "コイントス(共通)", F(18, True), WHITE)
ctext(coin_cx, coin_cy+122, "ラウンド頭に公開(当たり偶奇)", F(14), GREY)

# --- 送り先の矢印(コイン右の余白に大きく) ---
ax = coin_cx + 150
d.polygon([(ax, coin_cy-26), (ax+70, coin_cy-26), (ax+70, coin_cy-46),
           (ax+118, coin_cy-6), (ax+70, coin_cy+34), (ax+70, coin_cy+14), (ax, coin_cy+14)],
          fill=RED_D, outline=RED)
ctext(ax+58, coin_cy+58, "偶数=不一致 → 自分へ", F(15, True), RED)

# --- あなたの出し札(中央フィールドの下側) ---
phase_slot(220, 660, "あなた 前半", [("blue","9"),("blue","9"),("red","×3")])
phase_slot(470, 660, "あなた 後半", [("blue","8"),("blue","-2")])

# 計算結果の吹き出し
rr((200, 780, 1040, 826), 12, (24,32,50), outline=GREEN, width=2)
ctext(620, 803, "あなたの最終合計 = (9+9)×3 + (8-2) = 60  →  偶数=コインと不一致 → 自分へ", F(16, True), GREEN)

# --- 手札(中央フィールド最下段) ---
d.text((CX0+24, 848), "手札", font=F(20, True), fill=WHITE, anchor="lm")
hand = [("blue","9"),("blue","-5"),("blue","2"),("red","×-1"),("red","±5"),("gold","±9")]
hx = CX0 + 24
for i,(k,t) in enumerate(hand):
    card(hx + i*138, 868, 120, 90, k, t)
d.text((CX0+24, 980), "ドラッグ&ドロップで配置(前半2枚以上→後半1枚以上) / チャージでコストを貯めてアイテム発動",
       font=F(14), fill=GREY, anchor="lm")

# ================= 右:残数トラッカー / 山札 / アイテム =================
RX0, RX1 = 1090, 1510
rr((RX0, 90, RX1, 1000), 16, PANEL2, outline=(60,76,108), width=2)
ctext((RX0+RX1)//2, 120, "カード残数 一覧", F(22, True), WHITE)
ctext((RX0+RX1)//2, 146, "クリックで明暗トグル", F(14), GREY)

# 数字グリッド -9〜9
gx, gy = RX0+22, 178
cw, ch, gap = 40, 34, 5
nums = list(range(-9, 10))
used = {-9, -5, 7, 9, 0, 3}
for idx, n in enumerate(nums):
    col = idx % 5
    row = idx // 5
    x = gx + col*(cw+gap)
    y = gy + row*(ch+gap)
    on = n not in used
    fill = BLUE if on else (40,48,68)
    rr((x, y, x+cw, y+ch), 6, fill, outline=BLUE_D, width=1)
    d.text((x+cw/2, y+ch/2), str(n), font=F(16, True),
           fill=WHITE if on else (90,100,124), anchor="mm")

# レッド/ゴールド
ry = gy + 4*(ch+gap) + 22
ctext((RX0+RX1)//2, ry, "── 特殊カード ──", F(15), GREY)
reds = ["×2","×-2","×3","×-3","×-1","±5","±9"]
rused = {"×-2","±5"}
ry2 = ry + 24
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
    d.text((x+29, y+18), r, font=F(16, True),
           fill=(40,30,0) if (on and isg) else (WHITE if on else (108,90,90)), anchor="mm")

# 山札
ry3 = ry2 + 2*45 + 24
rr((RX0+22, ry3, RX1-22, ry3+110), 12, PANEL, outline=(70,84,112), width=2)
ctext((RX0+RX1)//2, ry3+28, "山札 共通デッキ", F(19, True), WHITE)
ctext((RX0+RX1)//2, ry3+62, "残 88 / 115 枚", F(24, True), GOLD)
ctext((RX0+RX1)//2, ry3+92, "ブルー95 + レッド19 + ゴールド1", F(13), GREY)

# あなたの持ち込みアイテム＋チャージ
iy = ry3 + 130
rr((RX0+22, iy, RX1-22, iy+150), 12, PANEL, outline=(70,84,112), width=2)
d.text((RX0+40, iy+22), "持ち込みアイテム(公開)", font=F(13), fill=GREY, anchor="lm")
d.text((RX0+40, iy+52), "② 符号反転(向き)", font=F(19, True), fill=GOLD, anchor="lm")
d.text((RX0+40, iy+92), "チャージ", font=F(15), fill=WHITE, anchor="lm")
cgx = RX0+140
for i in range(3):
    rr((cgx + i*26, iy+82, cgx + i*26 + 20, iy+104), 4, GREEN, outline=(70,84,112), width=1)
d.text((cgx + 3*26 + 10, iy+92), "3 / 3  発動可", font=F(15, True), fill=GREEN, anchor="lm")
d.text((RX0+40, iy+126), "アイテム①コイン反転・②符号反転・③ダブルシフト±7", font=F(12), fill=GREY, anchor="lm")

# ================= 凡例(最下部) =================
ly = 1040
def legend(x, col, label):
    rr((x, ly, x+26, ly+26), 6, col)
    d.text((x+34, ly+13), label, font=F(17), fill=WHITE, anchor="lm")
legend(200,  BLUE, "数字 -9〜9")
legend(420,  RED,  "特殊 ×N / ±5")
legend(700,  GOLD, "±9(切り札)")
legend(960,  BACK, "伏せ札(種別不可視)")

img.save("mockup.png")
print("saved mockup.png", img.size)
