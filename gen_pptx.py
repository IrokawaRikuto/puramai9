# -*- coding: utf-8 -*-
"""±9(プラマイ・ナイン)企画書 資料版を生成する。pptx と txt を出力。"""
from pptx import Presentation
from pptx.util import Inches, Pt
from pptx.dml.color import RGBColor
from pptx.enum.text import PP_ALIGN, MSO_ANCHOR
from pptx.enum.shapes import MSO_SHAPE
from pptx.oxml.ns import qn

# ---- カラーパレット ----
INDIGO = RGBColor(0x1F, 0x2D, 0x5A)
INDIGO2 = RGBColor(0x2E, 0x45, 0x8C)
BLUE = RGBColor(0x1E, 0x5B, 0xC6)
RED = RGBColor(0xC6, 0x28, 0x28)
GOLD = RGBColor(0xC9, 0xA2, 0x27)
COIN = RGBColor(0xE8, 0xA8, 0x00)
DARK = RGBColor(0x23, 0x27, 0x33)
GRAY = RGBColor(0xEE, 0xF1, 0xF6)
GRAY2 = RGBColor(0xD9, 0xDF, 0xEA)
WHITE = RGBColor(0xFF, 0xFF, 0xFF)
GREEN = RGBColor(0x1B, 0x7F, 0x4B)
FONT = 'Meiryo'

prs = Presentation()
prs.slide_width = Inches(13.333)
prs.slide_height = Inches(7.5)
BLANK = prs.slide_layouts[6]
SW, SH = prs.slide_width, prs.slide_height


def set_run(run, size=18, bold=False, color=DARK, name=FONT):
    run.font.size = Pt(size)
    run.font.bold = bold
    run.font.color.rgb = color
    run.font.name = name
    rPr = run._r.get_or_add_rPr()
    for tag in ('a:ea', 'a:cs'):
        el = rPr.find(qn(tag))
        if el is None:
            el = rPr.makeelement(qn(tag), {})
            rPr.append(el)
        el.set('typeface', name)


def add_text(slide, x, y, w, h, text, size=18, bold=False, color=DARK,
             align=PP_ALIGN.LEFT, anchor=MSO_ANCHOR.TOP):
    tb = slide.shapes.add_textbox(x, y, w, h)
    tf = tb.text_frame
    tf.word_wrap = True
    tf.vertical_anchor = anchor
    for i, ln in enumerate(text.split('\n')):
        p = tf.paragraphs[0] if i == 0 else tf.add_paragraph()
        p.alignment = align
        r = p.add_run()
        r.text = ln
        set_run(r, size, bold, color)
    return tb


def add_box(slide, x, y, w, h, text='', fill=GRAY, font_color=DARK, size=16,
            bold=False, align=PP_ALIGN.CENTER, shape=MSO_SHAPE.ROUNDED_RECTANGLE,
            line=None, anchor=MSO_ANCHOR.MIDDLE):
    sp = slide.shapes.add_shape(shape, x, y, w, h)
    sp.fill.solid()
    sp.fill.fore_color.rgb = fill
    if line is None:
        sp.line.fill.background()
    else:
        sp.line.color.rgb = line
        sp.line.width = Pt(1.25)
    sp.shadow.inherit = False
    tf = sp.text_frame
    tf.word_wrap = True
    tf.vertical_anchor = anchor
    tf.margin_left = Inches(0.06)
    tf.margin_right = Inches(0.06)
    tf.margin_top = Inches(0.03)
    tf.margin_bottom = Inches(0.03)
    for i, ln in enumerate(text.split('\n')):
        p = tf.paragraphs[0] if i == 0 else tf.add_paragraph()
        p.alignment = align
        r = p.add_run()
        r.text = ln
        set_run(r, size, bold, font_color)
    return sp


def arrow(slide, x, y, w, h, color=GOLD, shape=MSO_SHAPE.RIGHT_ARROW):
    return add_box(slide, x, y, w, h, '', fill=color, shape=shape)


def bg(slide, color=WHITE):
    r = slide.shapes.add_shape(MSO_SHAPE.RECTANGLE, 0, 0, SW, SH)
    r.fill.solid()
    r.fill.fore_color.rgb = color
    r.line.fill.background()
    r.shadow.inherit = False
    spTree = slide.shapes._spTree
    spTree.remove(r._element)
    spTree.insert(2, r._element)
    return r


def header(slide, title, no):
    bar = slide.shapes.add_shape(MSO_SHAPE.RECTANGLE, 0, 0, SW, Inches(1.0))
    bar.fill.solid()
    bar.fill.fore_color.rgb = INDIGO
    bar.line.fill.background()
    bar.shadow.inherit = False
    add_text(slide, Inches(0.55), Inches(0.1), Inches(11.3), Inches(0.8),
             title, 27, True, WHITE, PP_ALIGN.LEFT, MSO_ANCHOR.MIDDLE)
    acc = slide.shapes.add_shape(MSO_SHAPE.RECTANGLE, 0, Inches(1.0), SW, Inches(0.07))
    acc.fill.solid()
    acc.fill.fore_color.rgb = GOLD
    acc.line.fill.background()
    acc.shadow.inherit = False
    add_text(slide, Inches(12.2), Inches(0.1), Inches(0.95), Inches(0.8),
             no, 15, True, COIN, PP_ALIGN.RIGHT, MSO_ANCHOR.MIDDLE)


def table(slide, x, y, w, h, data, col_w=None, header_fill=INDIGO2, size=14, fcb=False):
    rows, cols = len(data), len(data[0])
    g = slide.shapes.add_table(rows, cols, x, y, w, h).table
    if col_w:
        for i, cw in enumerate(col_w):
            g.columns[i].width = cw
    for ri, row in enumerate(data):
        for ci, val in enumerate(row):
            c = g.cell(ri, ci)
            c.margin_left = Inches(0.08)
            c.margin_right = Inches(0.06)
            c.margin_top = Inches(0.02)
            c.margin_bottom = Inches(0.02)
            c.vertical_anchor = MSO_ANCHOR.MIDDLE
            tf = c.text_frame
            tf.word_wrap = True
            p = tf.paragraphs[0]
            p.alignment = PP_ALIGN.CENTER if (ri == 0 or (ci > 0 and not fcb)) else PP_ALIGN.LEFT
            r = p.add_run()
            r.text = str(val)
            if ri == 0:
                set_run(r, size, True, WHITE)
                c.fill.solid()
                c.fill.fore_color.rgb = header_fill
            else:
                set_run(r, size, (fcb and ci == 0), DARK)
                c.fill.solid()
                c.fill.fore_color.rgb = WHITE if ri % 2 else GRAY
    return g


I = Inches

# ============ Slide 1 : タイトル ============
s = prs.slides.add_slide(BLANK)
bg(s, INDIGO)
band = s.shapes.add_shape(MSO_SHAPE.RECTANGLE, 0, I(3.05), SW, I(0.09))
band.fill.solid(); band.fill.fore_color.rgb = GOLD; band.line.fill.background(); band.shadow.inherit = False
add_text(s, I(0.8), I(1.4), I(11.7), I(1.6), "±9", 96, True, WHITE, PP_ALIGN.CENTER, MSO_ANCHOR.MIDDLE)
add_text(s, I(0.8), I(3.2), I(11.7), I(0.8), "プラマイ・ナイン", 40, True, COIN, PP_ALIGN.CENTER)
add_text(s, I(1.2), I(4.4), I(10.9), I(1.2),
         "コイントスの「運」、数字の「計算」、相手との「読み合い」。\n"
         "一手で天国と地獄が入れ替わる、二人対戦の心理戦カードバトル。",
         20, False, GRAY2, PP_ALIGN.CENTER)
add_text(s, I(0.8), I(6.5), I(11.7), I(0.5),
         "神ゲー創造主エボリューション 企画書  /  チーム Noad", 16, True, WHITE, PP_ALIGN.CENTER)

# ============ Slide 2 : ゲーム概要 ============
s = prs.slides.add_slide(BLANK); bg(s)
header(s, "1. ゲーム概要", "01")
table(s, I(0.7), I(1.5), I(7.2), I(4.6), [
    ["項目", "内容"],
    ["タイトル", "±9(プラマイ・ナイン)"],
    ["ジャンル", "二人〜四人対戦 心理戦カードバトル"],
    ["プレイ人数", "2〜4人(オンライン対戦想定)"],
    ["1試合の長さ", "5〜15分(想定)"],
    ["プレイ環境", "PC / スマホ(オンライン)"],
    ["HP", "初期100 / 可動域 0〜200"],
    ["デッキ", "全115枚(共通デッキ・裏面共通)"],
], col_w=[I(2.4), I(4.8)], size=15, fcb=True)
add_box(s, I(8.2), I(1.6), I(4.45), I(2.05),
        "核となる体験\n\n「強い一手ほど、自分に返ってくると痛い」\n"
        "── 運の二面性をコインで突きつける緊張感。",
        fill=GRAY, size=16, bold=False, align=PP_ALIGN.LEFT, line=GOLD)
add_box(s, I(8.2), I(3.85), I(4.45), I(2.25),
        "なぜ斬新か\n\n相手を 0 か 200 の両端へ押し出して勝つ\n"
        "“綱引き”構造 × コインの偶奇に賭ける読み合い。\n"
        "計算・運・心理戦が一手に同居する。",
        fill=INDIGO2, font_color=WHITE, size=16, align=PP_ALIGN.LEFT)

# ============ Slide 3 : セールスポイント ============
s = prs.slides.add_slide(BLANK); bg(s)
header(s, "2. セールスポイント", "02")
pts = [
    ("シンプルな操作、深い読み合い", "数字を足してコインに賭けるだけ。\nルールは簡単、駆け引きは無限。", BLUE),
    ("一手で天国と地獄", "コインの偶奇が外れれば、放った一撃が\nそのまま自分のHPを動かす。", RED),
    ("裏面共通・共通デッキ", "場に伏せた瞬間、種別すら読めない。\n枚数だけが手がかりのブラフ戦。", INDIGO2),
    ("カード残数の管理メタ", "使われた札を一覧表で追う記憶戦。\n“次に何が来るか”を読む。", GOLD),
]
for i, (t, d, col) in enumerate(pts):
    x = I(0.7 + (i % 2) * 6.15)
    y = I(1.5 + (i // 2) * 2.55)
    add_box(s, x, y, I(0.65), I(2.25), str(i + 1), fill=col, font_color=WHITE, size=34, bold=True)
    add_box(s, x + I(0.65), y, I(5.4), I(2.25), "", fill=GRAY, line=col)
    add_text(s, x + I(0.85), y + I(0.2), I(5.0), I(0.8), t, 18, True, col)
    add_text(s, x + I(0.85), y + I(1.0), I(5.0), I(1.1), d, 15, False, DARK)

# ============ Slide 4 : 勝利条件 ============
s = prs.slides.add_slide(BLANK); bg(s)
header(s, "3. 勝利条件 ── 0 と 200 の綱引き", "03")
add_text(s, I(0.7), I(1.35), I(12), I(0.6),
         "HPは初期100・可動域0〜200。相手のHPを 0 または 200 にした方の勝利。", 19, True, INDIGO)
# HP bar
bx, by, bw, bh = I(1.3), I(2.7), I(10.7), I(1.0)
add_box(s, bx, by, I(1.6), bh, "0\n敗北", fill=RED, font_color=WHITE, size=16, bold=True, shape=MSO_SHAPE.RECTANGLE)
add_box(s, bx + I(1.6), by, I(7.5), bh, "← −カードで下げる    安全圏 (1〜199)    +カードで上げる →",
        fill=GRAY, size=14, shape=MSO_SHAPE.RECTANGLE)
add_box(s, bx + I(9.1), by, I(1.6), bh, "200\n敗北", fill=RED, font_color=WHITE, size=16, bold=True, shape=MSO_SHAPE.RECTANGLE)
# start marker
add_box(s, bx + I(4.75), by - I(0.55), I(1.2), I(0.5), "100 開始", fill=GOLD, font_color=WHITE, size=13, bold=True)
mark = s.shapes.add_shape(MSO_SHAPE.ISOSCELES_TRIANGLE, bx + I(5.15), by - I(0.08), I(0.4), I(0.3))
mark.rotation = 180; mark.fill.solid(); mark.fill.fore_color.rgb = GOLD; mark.line.fill.background(); mark.shadow.inherit = False
add_box(s, I(0.7), I(4.5), I(5.9), I(2.1),
        "勝ち方は2通り\n\n"
        "● 相手を 200 へ → +カードで押し上げる\n"
        "● 相手を 0 へ → −カードで削り落とす\n\n"
        "状況で「どちらの端を狙うか」を切り替える。",
        fill=GRAY, size=16, align=PP_ALIGN.LEFT, line=GREEN)
add_box(s, I(6.85), I(4.5), I(5.75), I(2.1),
        "自爆のルール\n\n"
        "コインの偶奇が外れると、合計が\n自分のHPに作用してしまう。\n"
        "自分が 0 / 200 に達したら自分の負け。",
        fill=INDIGO2, font_color=WHITE, size=16, align=PP_ALIGN.LEFT)
add_box(s, I(0.7), I(6.78), I(11.92), I(0.62),
        "両者同時着弾(1v1):HP反映は同時。両者が同時に端へ達したら引き分け → "
        "オーバータイム(手札・捨て札を山札へ戻し、両者HP50・先に100か0で負け)。",
        fill=GOLD, font_color=WHITE, size=14, bold=True)

# ============ Slide 5 : デッキ構成 ============
s = prs.slides.add_slide(BLANK); bg(s)
header(s, "4. デッキ構成 ── 全115枚(共通・裏面共通)", "04")
table(s, I(0.7), I(1.45), I(6.5), I(5.1), [
    ["種別", "内訳", "枚数"],
    ["ブルー(数字)", "-9〜9 の19種 × 5枚", "95"],
    ["レッド ×2", "数字を2倍(同フェイズ)", "3"],
    ["レッド ×-2", "2倍+符号反転", "3"],
    ["レッド ×3", "数字を3倍", "2"],
    ["レッド ×-3", "3倍+符号反転", "2"],
    ["レッド ×-1", "符号だけ反転", "5"],
    ["レッド ±5", "ブルー合計の符号方向に+5", "4"],
    ["ゴールド ±9", "全カードを±9化(1枚限定)", "1"],
    ["合計", "", "115"],
], col_w=[I(2.1), I(3.4), I(1.0)], size=13.5, fcb=True)
add_box(s, I(7.5), I(1.55), I(5.15), I(1.5),
        "ブルーカード\n-9 〜 +9(各5枚)\n足してHPの増減量をつくる",
        fill=BLUE, font_color=WHITE, size=15, align=PP_ALIGN.LEFT)
add_box(s, I(7.5), I(3.2), I(5.15), I(1.6),
        "レッドカード(特殊・19枚)\n×N / ±5 で合計を加工。\n反転系(×-1/×-2/×-3)が10枚 →\n"
        "「向きの奪い合い」が主戦場。",
        fill=RED, font_color=WHITE, size=15, align=PP_ALIGN.LEFT)
add_box(s, I(7.5), I(4.95), I(5.15), I(1.55),
        "ゴールドカード(1枚)\nそのターン出した全札を±9に変化。\n最大±45。一発逆転の切り札。",
        fill=GOLD, font_color=WHITE, size=15, align=PP_ALIGN.LEFT)

# ============ Slide 6 : 1ラウンドの流れ ============
s = prs.slides.add_slide(BLANK); bg(s)
header(s, "5. 1ラウンドの流れ", "05")
steps = [
    ("①コイン", "ラウンド頭に\n当たり偶奇公開", COIN),
    ("②前半", "2枚以上\nを伏せる", INDIGO2),
    ("③後半", "1枚以上\nを伏せる", INDIGO2),
    ("④オープン", "各自の\n合計を算出", BLUE),
    ("⑤送り先", "コイン×\n偶奇で決定", RED),
    ("⑥HP反映", "倍率込み\n0/200で決着", GREEN),
]
x = I(0.55); y = I(1.9); bw = I(1.78); bh = I(1.7); aw = I(0.26)
for i, (t, d, col) in enumerate(steps):
    add_box(s, x, y, bw, bh, t + "\n\n" + d, fill=col,
            font_color=WHITE, size=15, bold=True)
    if i < len(steps) - 1:
        arrow(s, x + bw + I(0.01), y + I(0.6), aw, I(0.5), color=GOLD)
    x = x + bw + aw + I(0.04)
add_box(s, I(0.7), I(4.1), I(5.85), I(2.2),
        "ポイント①:コインは先出し\n\n"
        "ラウンド頭に当たり偶奇を公開。\n"
        "それを見て「狙った火力・向きのまま\n当たり偶奇を作れるか」を考えて手を組む。",
        fill=GRAY, size=16, align=PP_ALIGN.LEFT, line=COIN)
add_box(s, I(6.75), I(4.1), I(5.85), I(2.2),
        "ポイント②:チャージとアイテム\n\n"
        "チャージ(旧・破棄)=1ラウンド1枚(別枠)。\n"
        "アイテムはカード出し中・1ラウンド1回。\n"
        "手札6・前半2後半1出し・溜め込みは廃止。",
        fill=GRAY, size=16, align=PP_ALIGN.LEFT, line=COIN)
add_box(s, I(0.7), I(6.5), I(11.92), I(0.6),
        "ドロー:手札6へ補充＋ゾロ目ドローボーナス(同数字2枚→+1/3枚→+2、上限8)。山札が「人数×5枚」以下なら先に捨て札をリシャッフル。",
        fill=INDIGO2, font_color=WHITE, size=14, bold=True)

# ============ Slide 7 : 方向判定 ============
s = prs.slides.add_slide(BLANK); bg(s)
header(s, "6. 送り先判定 ── コイン × 偶奇(パリティ)", "06")
add_text(s, I(0.7), I(1.3), I(12), I(0.6),
         "ラウンド頭に公開した当たり偶奇と、自分の最終合計の偶奇が「一致→相手 / 不一致→自分(自爆)」。", 18, True, INDIGO)
gx, gy, cw, ch = I(2.2), I(2.2), I(3.4), I(1.15)
add_box(s, gx, gy, cw, ch, "", fill=WHITE, line=GRAY2)
add_box(s, gx + cw, gy, cw, ch, "コイン:奇数", fill=INDIGO, font_color=WHITE, size=16, bold=True)
add_box(s, gx + 2 * cw, gy, cw, ch, "コイン:偶数", fill=INDIGO, font_color=WHITE, size=16, bold=True)
rows = [("合計:奇数", "相手へ\n(押し付け)", "自分へ\n(自爆)", GREEN, RED),
        ("合計:偶数", "自分へ\n(自爆)", "相手へ\n(押し付け)", RED, GREEN)]
for ri, (lab, a, b, ca, cb) in enumerate(rows):
    yy = gy + ch * (ri + 1)
    add_box(s, gx, yy, cw, ch, lab, fill=INDIGO, font_color=WHITE, size=16, bold=True)
    add_box(s, gx + cw, yy, cw, ch, a, fill=ca, font_color=WHITE, size=16, bold=True)
    add_box(s, gx + 2 * cw, yy, cw, ch, b, fill=cb, font_color=WHITE, size=16, bold=True)
add_box(s, I(2.2), I(6.0), I(8.9), I(0.95),
        "相手と同じ偶奇 → 2人とも同じ向き(連動) / 逆の偶奇 → 必ず片方だけ命中(割れる)",
        fill=GRAY, size=16, line=GOLD)

# ============ Slide 8 : 計算ルール ============
s = prs.slides.add_slide(BLANK); bg(s)
header(s, "7. 計算ルール", "07")
add_box(s, I(0.7), I(1.45), I(7.0), I(0.7), "最終合計 = 前半合計 + 後半合計",
        fill=INDIGO, font_color=WHITE, size=20, bold=True)
add_text(s, I(0.7), I(2.3), I(7.2), I(4.3),
         "● 各フェイズで「数字を合計 → 特殊カードを左から適用」\n\n"
         "● 乗算(×N)は同じフェイズの数字にのみ作用。別フェイズは単純加算\n\n"
         "● ③ダブルシフト±7も使ったフェイズの合計に作用(②符号反転は最終合計の符号のみ反転=偶奇不変)\n\n"
         "● 足し算を先に処理してから掛け算(優先順位は無視)\n\n"
         "● 除算は不採用 → 小数は発生しない\n\n"
         "● ブルー無しのフェイズ合計は0(レッド・③も作用対象が必要)\n\n"
         "● 結果が + は対象を上げ、− は対象を下げる(終盤はエスカレーション倍率が乗る)",
         15, False, DARK)
add_box(s, I(8.1), I(1.45), I(4.55), I(2.4),
        "計算例\n\n前半 [9][9][×3] → 18×3 = 54\n後半 [9][9]   → 18\n──────────────\n最終合計 = 72(偶数)",
        fill=GRAY, size=15, align=PP_ALIGN.LEFT, line=BLUE)
add_box(s, I(8.1), I(4.05), I(4.55), I(2.1),
        "ゴールド例\n\n5枚出して + を選択\n→ 全部 +9 に変化\n→ +9 × 5 = +45(奇数)",
        fill=GOLD, font_color=WHITE, size=15, align=PP_ALIGN.LEFT)

# ============ Slide 9 : アイテム(一覧表) ============
s = prs.slides.add_slide(BLANK); bg(s)
header(s, "8. アイテムシステム(3種・チャージ制 / 開始時に1個公開)", "08")
# チャージ制の説明バー
add_box(s, I(0.35), I(1.25), I(12.63), I(0.62),
        "◆ チャージ制:カードを1枚チャージするたび コスト+1(チャージは1ラウンド1枚・別枠)／ 開始コスト=①は2・他は1 ／ "
        "規定コストを払ってアイテム発動",
        fill=INDIGO2, font_color=WHITE, size=15, bold=True)
item_rows = [
    ["#", "アイテム名", "軸", "コスト", "開始", "タイミング", "効果"],
    ["①", "コイン反転", "送り先", "6", "2", "後半のみ", "当たり偶奇を奇⇄偶へ反転(後半終了後に判明=後出しで覆す)"],
    ["②", "符号反転", "向き", "3", "1", "前半/後半", "自分の最終合計の符号を反転(偶奇は不変=0狙い⇄200狙いの取り直し)"],
    ["③", "ダブルシフト±7", "全体かく乱", "2", "1", "カード出し中", "使ったフェイズの全員(敵味方)の合計に同符号+7/−7(多人数で本領)"],
]
table(s, I(0.35), I(2.0), I(12.63), I(2.7), item_rows,
      col_w=[I(0.4), I(1.85), I(1.35), I(0.8), I(0.65), I(1.7), I(5.88)],
      size=12.5, fcb=True)
add_box(s, I(0.35), I(4.95), I(12.63), I(0.95),
        "◆ 旧5種(コイン反転/序盤ドローブースト/強制リシャッフル/絶対値ブースト/ダブルシフト)から3種に再編。\n"
        "駆け引きの3軸(送り先・向き・全体かく乱)に1本ずつ対応させ、被りを排した。①への対抗=弱い手・逆偶奇で受け流す(ローリスク選択がそのまま防御)。",
        fill=INDIGO2, font_color=WHITE, size=13, bold=True)
add_box(s, I(0.35), I(6.0), I(12.63), I(1.1),
        "共通:試合前に1個だけ持ち込み・開始時に互いに公開／使用は「前半・後半中・1ラウンド1回」。所持アイテムは種類のみ公開、チャージ累計は非表示(数えれば読める)。\n"
        "※ コスト額(①6/②3/③2)・開始コスト(①2・他1)・③の±7は暫定値 → プレイテストで調整予定。",
        fill=GOLD, font_color=WHITE, size=12)

# ============ Slide 10 : 戦略の核 ============
s = prs.slides.add_slide(BLANK); bg(s)
header(s, "9. 戦略の核", "09")
cols = [
    ("偶奇の賭け", "合計の偶奇＝コインへの賭け。\n相手と合わせるか外すかで、\n命中の連動/分断をコントロール。", BLUE),
    ("向きの奪い合い", "0狙いと200狙いを切り替える。\n反転カードや②符号反転で相手の\n押し上げを一瞬で削りに変える。", RED),
    ("賭け金の選択", "出す枚数で賭け金を自分で決める。\n大きく撃てばハイリスク・ハイリターン、\n小さく撃てば安全(自爆の二面性)。", GREEN),
]
for i, (t, d, col) in enumerate(cols):
    x = I(0.7 + i * 4.1)
    add_box(s, x, I(1.7), I(3.85), I(0.8), t, fill=col, font_color=WHITE, size=20, bold=True)
    add_box(s, x, I(2.6), I(3.85), I(2.6), d, fill=GRAY, size=16, align=PP_ALIGN.LEFT, line=col)
add_box(s, I(0.7), I(5.5), I(11.95), I(1.1),
        "「強いカードほど、自分に返ってきたら痛い」── この運の二面性こそ、±9 の体験の中心。",
        fill=INDIGO, font_color=WHITE, size=19, bold=True)

# ============ Slide 11 : モード構成 ============
s = prs.slides.add_slide(BLANK); bg(s)
header(s, "10. モード構成(3種)", "10")
modes = [
    ("カスタム", "自由設定で遊ぶ\nルームコードで招待\nホストが全設定", BLUE),
    ("カジュアル", "気軽に自動マッチング\nバトロワ形式\n決まったルール", GREEN),
    ("ガチ対戦", "レートをかけた1v1\n近レート同士で対戦\n初期1000P", RED),
]
for i, (t, d, col) in enumerate(modes):
    x = I(0.7 + i * 4.1)
    add_box(s, x, I(1.5), I(3.85), I(0.8), t, fill=col, font_color=WHITE, size=21, bold=True)
    add_box(s, x, I(2.35), I(3.85), I(1.45), d, fill=GRAY, size=15, align=PP_ALIGN.LEFT, line=col)
table(s, I(0.7), I(3.85), I(11.95), I(1.9), [
    ["項目", "カスタム", "カジュアル", "ガチ対戦"],
    ["参加", "ルームコード(ホスト作成)", "自動マッチング", "自動マッチング(レート)"],
    ["対戦形式", "1v1 / 2v2 / 1v1v1 / 1v1v1v1", "バトロワ 1v1 / 1v1v1 / 1v1v1v1", "1v1のみ"],
    ["アイテム / HP", "ON・OFF / 設定可", "ON / 人数準拠", "ON / 100"],
    ["捨て札の開示", "ON・OFF", "ON", "OFF"],
    ["ターン時間 / コスト", "設定可 / 設定可", "50秒 / 既定", "30秒 / 既定"],
], col_w=[I(2.0), I(4.3), I(3.4), I(2.25)], size=12, fcb=True)
add_box(s, I(0.7), I(5.78), I(11.95), I(0.86),
        "多人数:攻撃は敵全員へ / HP人数準拠(1v1=100・1v1v1=150・4人=200) / コストは個人別 / ③ダブルシフト±7は敵味方全員\n"
        "敗北→チーム(2v2)は2人とも負けて敗北・バトロワは最後の1人まで。敗北者は手札を捨て、毎ターンコスト+1でアイテムのみ使用可(妨害役)",
        fill=INDIGO2, font_color=WHITE, size=11, bold=True)
add_box(s, I(0.7), I(6.72), I(11.95), I(0.55),
        "ガチ対戦レート(暫定):初期1000P / 同レート(差100内)勝ち+20 / 格上勝ち最大+40・負け-10 / "
        "格下勝ち+10・負け-40 / 差100超は50Pごと1〜3P・HP差±10補正",
        fill=GOLD, font_color=WHITE, size=11, bold=True)

# ============ Slide 12 : まとめ ============
s = prs.slides.add_slide(BLANK); bg(s, INDIGO)
add_text(s, I(0.8), I(0.7), I(11.7), I(0.9), "まとめ", 34, True, COIN, PP_ALIGN.LEFT)
acc = s.shapes.add_shape(MSO_SHAPE.RECTANGLE, I(0.85), I(1.55), I(3.0), I(0.06))
acc.fill.solid(); acc.fill.fore_color.rgb = GOLD; acc.line.fill.background(); acc.shadow.inherit = False
add_text(s, I(0.85), I(1.9), I(11.6), I(3.0),
         "● 運(コイン)・計算(数字)・心理戦(読み合い)が一手に同居\n\n"
         "● 相手を 0 か 200 へ押し出す“綱引き”という新しい勝利構造\n\n"
         "● コインの偶奇に賭け、後半で微調整する独自の駆け引き\n\n"
         "● 共通デッキ115枚・裏面共通・残数管理メタで深い読み合い",
         20, False, WHITE)
add_box(s, I(0.85), I(5.4), I(11.6), I(1.4),
        "一手で天国と地獄が入れ替わる ── ±9 (プラマイ・ナイン)",
        fill=GOLD, font_color=INDIGO, size=22, bold=True)

out = "プラマイナイン_企画書資料.pptx"
prs.save(out)
print("saved:", out, "slides:", len(prs.slides._sldIdLst))
