// ±9(プラマイ・ナイン) Unity 人 vs CPU プロト
// ・uGUIをコードから生成(シーン/プレハブ不要)。空のGameObjectにこの1枚を付けて再生するだけ。
// ・ルール処理は Core/(PlusMinusNine名前空間)を使用。Coreフォルダを同じプロジェクトに入れること。
// ・Project Settings > Player > Active Input Handling は "Both" か "Input Manager (Old)" に。
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PlusMinusNine;

public class PlusMinusNineGame : MonoBehaviour
{
    Font _font;
    GameState _g;
    GreedyAi _cpu;
    const int Me = 0, Cpu = 1, MaxTurns = 300;

    // UI参照
    Canvas _canvas;
    Text _oppHpText, _oppItemText, _youHpText, _youItemText, _youCostText;
    Image _oppFill, _youFill;
    Text _coinText, _infoText, _logText, _stageText, _previewText, _dirBanner;
    RectTransform _handRow;
    Button _confirmBtn, _itemBtn, _discardBtn, _discardConfirmBtn, _signBtn;
    Text _confirmLabel, _itemLabel, _signLabel;

    // 選択状態
    UiPhase _phase;
    readonly List<Card> _front = new();
    readonly List<Card> _back = new();
    bool _useItem; Phase _itemPhase; int _sign = 1; Card _discard; bool _discardMode; Card _discardCandidate;
    Coin _coin; bool _coinKnown;
    bool _confirmClicked;
    TurnPlan _myPlan;
    ItemType _myItem = ItemType.AbsBoost;
    readonly List<string> _log = new();

    enum UiPhase { Front, Back }

    // 再生するだけで自動起動(シーンにGameObjectを置く必要なし)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindFirstObjectByType<PlusMinusNineGame>() != null) return;
        var go = new GameObject("PlusMinusNineGame");
        go.AddComponent<PlusMinusNineGame>();
    }

    void Start()
    {
        _font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic UI", "Meiryo UI", "Yu Gothic", "Arial" }, 20);
        BuildUi();
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        yield return PickItem();              // アイテム選択
        var cpuItem = (ItemType)Random.Range(1, 6);
        _g = new GameState(_myItem, cpuItem, Random.Range(1, 999999));
        _cpu = new GreedyAi(Random.Range(1, 999999));
        AddLog($"あなた:{Items.Name(_myItem)} / CPU:{Items.Name(cpuItem)}");
        RefreshAll();

        while (_g.Winner == -1 && _g.Turn < MaxTurns)
            yield return Turn();

        // 決着
        string msg = _g.Winner == Me ? "あなたの勝ち！" : _g.Winner == Cpu ? "CPUの勝ち…" : "引き分け";
        _infoText.text = $"=== {msg} ===  (R=もう一度)";
        while (!Input.GetKeyDown(KeyCode.R)) yield return null;
        // リスタート
        foreach (Transform c in _canvas.transform) Destroy(c.gameObject);
        ResetState();
        StartCoroutine(Run());
    }

    void ResetState()
    {
        _front.Clear(); _back.Clear(); _useItem = false; _discard = null; _discardMode = false;
        _discardCandidate = null; _coinKnown = false; _myPlan = null; _log.Clear();
        BuildPlayArea();
    }

    IEnumerator Turn()
    {
        _g.TurnHeadDraw();
        _g.RunPendingReshuffle();                     // 前ターンの後半③をここで実行
        _front.Clear(); _back.Clear(); _useItem = false; _discard = null; _discardMode = false; _sign = 1;
        _discardCandidate = null; _coinKnown = false;
        _myPlan = new TurnPlan();
        var cpuPlan = _cpu.DecideFront(_g, Cpu);       // CPUの前半を先に決定(前半③が前半中に解決されるように)

        // --- 前半(あなた) ---
        _phase = UiPhase.Front;
        _infoText.text = $"ターン{_g.Turn} 前半:カードを最大3枚 → 確定";
        _coinText.text = "コイン:??";
        RefreshAll();
        yield return WaitConfirm();
        _myPlan.Front = new List<Card>(_front);
        _myPlan.Discard = _discard;
        _myPlan.ShiftSign = _sign; _myPlan.GoldSign = _sign;
        if (_useItem) { _myPlan.UseItem = true; _myPlan.ItemPhase = Phase.Front; }

        // --- コイン → 前半コミット(前半③はここでリシャッフル) ---
        var coin = _g.TossCoin();
        _coin = coin; _coinKnown = true;
        _g.CommitFront(_myPlan, cpuPlan);
        _useItem = false;                              // 後半のアイテム意思はリセット(前半使用なら下で弾く)

        // --- 後半 ---
        _phase = UiPhase.Back;
        _coinText.text = coin == Coin.Odd ? "コイン:奇(ODD)" : "コイン:偶(EVEN)";
        _infoText.text = $"ターン{_g.Turn} 後半:最大2枚・コインを見て調整 → 確定";
        RefreshAll();
        yield return WaitConfirm();
        _myPlan.Back = new List<Card>(_back);
        if (_useItem && !_myPlan.UseItem) { _myPlan.UseItem = true; _myPlan.ItemPhase = Phase.Back; _myPlan.ShiftSign = _sign; _myPlan.GoldSign = _sign; }
        if (_discard != null && _myPlan.Discard == null) _g.ApplyDiscardNow(Me, _discard);  // 後半で破棄した場合

        // --- CPU後半 → 後半解決 ---
        _cpu.DecideBack(_g, Cpu, coin, cpuPlan);
        var r = _g.ResolveAfterFront(_myPlan, cpuPlan, coin);
        string Dir(Direction d) => d == Direction.ToOpponent ? "→相手" : d == Direction.ToSelf ? "→自分" : "無効";
        AddLog($"T{_g.Turn} {(r.FinalCoin == Coin.Odd ? "奇" : "偶")}|あなた {r.FinalTotal[Me]}{Dir(r.Dir[Me])} / CPU {r.FinalTotal[Cpu]}{Dir(r.Dir[Cpu])}");
        foreach (var l in r.Log) AddLog("  " + l);
        if (r.Overtime) AddLog("★オーバータイム!(HP50・先に100/0で負け)");
        RefreshAll();
        yield return new WaitForSeconds(0.6f);
    }

    IEnumerator WaitConfirm() { _confirmClicked = false; while (!_confirmClicked) yield return null; }

    // ============ UI構築 ============
    void BuildUi()
    {
        // Canvas
        var cgo = new GameObject("Canvas");
        _canvas = cgo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        cgo.AddComponent<GraphicRaycaster>();
        // EventSystem
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }
        BuildPlayArea();
    }

    void BuildPlayArea()
    {
        // 背景
        var bg = MakePanel(_canvas.transform, 0, 0, 1280, 720, new Color(0.07f, 0.09f, 0.15f));
        // 相手パネル
        MakePanel(bg, 20, 20, 1240, 120, new Color(0.12f, 0.16f, 0.24f));
        _oppHpText = MakeText(bg, 40, 35, 600, 30, "相手 HP", 22, TextAnchor.MiddleLeft);
        _oppFill = MakeBar(bg, 40, 70, 760, 26, new Color(0.84f, 0.25f, 0.28f));
        _oppItemText = MakeText(bg, 820, 35, 400, 30, "相手アイテム", 18, TextAnchor.MiddleLeft);

        // 中央
        _coinText = MakeText(bg, 40, 160, 400, 40, "コイン:??", 26, TextAnchor.MiddleLeft);
        _infoText = MakeText(bg, 40, 205, 1200, 30, "", 20, TextAnchor.MiddleLeft);
        _previewText = MakeText(bg, 820, 160, 420, 40, "", 20, TextAnchor.MiddleLeft);
        _stageText = MakeText(bg, 40, 240, 1200, 30, "場(あなた):", 18, TextAnchor.MiddleLeft);
        var logBg = MakePanel(bg, 820, 285, 440, 230, new Color(0.10f, 0.13f, 0.20f));
        _logText = MakeText(bg, 835, 290, 415, 220, "", 15, TextAnchor.UpperLeft);

        // 自分パネル
        MakePanel(bg, 20, 525, 1240, 175, new Color(0.12f, 0.16f, 0.24f));
        _youHpText = MakeText(bg, 40, 535, 600, 30, "あなた HP", 22, TextAnchor.MiddleLeft);
        _youFill = MakeBar(bg, 40, 570, 760, 26, new Color(0.35f, 0.77f, 0.51f));
        _youItemText = MakeText(bg, 820, 535, 300, 30, "", 18, TextAnchor.MiddleLeft);
        _youCostText = MakeText(bg, 1050, 535, 190, 30, "", 20, TextAnchor.MiddleLeft);

        // 命中先バナー(手札の上に大きく)
        _dirBanner = MakeText(bg, 40, 476, 760, 46, "", 30, TextAnchor.MiddleLeft);

        // 手札行
        var handGo = MakePanel(bg, 40, 605, 760, 90, new Color(0, 0, 0, 0));
        _handRow = handGo;

        // 操作ボタン
        _confirmBtn = MakeButton(bg, 820, 605, 200, 40, "確定", () => _confirmClicked = true, out _confirmLabel);
        _itemBtn = MakeButton(bg, 820, 650, 200, 40, "アイテム使用", ToggleItem, out _itemLabel);
        _discardBtn = MakeButton(bg, 1040, 605, 130, 40, "破棄モード", ToggleDiscard, out _);
        _discardConfirmBtn = MakeButton(bg, 1040, 650, 130, 40, "破棄確定", ConfirmDiscard, out _);
        _signBtn = MakeButton(bg, 1180, 605, 80, 40, "符号+", ToggleSign, out _signLabel);
    }

    IEnumerator PickItem()
    {
        var panel = MakePanel(_canvas.transform, 340, 200, 600, 320, new Color(0.12f, 0.16f, 0.24f));
        MakeText(panel, 20, 15, 560, 40, "持ち込むアイテムを選択", 24, TextAnchor.MiddleCenter);
        var opts = new (string, ItemType)[]
        {
            ("①コイン反転(後半のみ・コスト6)", ItemType.CoinFlip),
            ("②序盤ドローブースト(無料)", ItemType.DrawBoost),
            ("③強制リシャッフル(コスト3)", ItemType.Reshuffle),
            ("④絶対値+8(コスト4)", ItemType.AbsBoost),
            ("⑤ダブルシフト±7(コスト4)", ItemType.DoubleShift),
        };
        bool picked = false;
        for (int i = 0; i < opts.Length; i++)
        {
            var it = opts[i].Item2;
            MakeButton(panel, 30, 60 + i * 48, 540, 42, opts[i].Item1, () => { _myItem = it; picked = true; }, out _);
        }
        while (!picked) yield return null;
        Destroy(panel.gameObject);
    }

    // ============ 入力ハンドラ ============
    void OnCardClicked(Card c)
    {
        if (_discardMode)
        {
            if (_discard != null) return;                                 // 既に破棄済み
            _discardCandidate = (_discardCandidate == c) ? null : c;      // 選ぶだけ。確定は「破棄確定」
            RefreshAll(); return;
        }
        var sel = _phase == UiPhase.Front ? _front : _back;
        int max = _phase == UiPhase.Front ? 3 : 2;
        if (sel.Contains(c)) sel.Remove(c);
        else if (sel.Count < max && c != _discard) sel.Add(c);
        RefreshAll();
    }

    void ToggleItem()
    {
        var item = _myItem;
        if (item == ItemType.DrawBoost) { AddLog("②は自動発動(使用不要)"); return; }
        if (_myPlan != null && _myPlan.UseItem) { AddLog("アイテムは1ターン1回(前半で使用済み)"); return; }
        if (item == ItemType.CoinFlip && _phase != UiPhase.Back) { AddLog("①は後半のみ使用可"); return; }
        if (item == ItemType.Reshuffle && _phase != UiPhase.Front) { AddLog("③は前半に使用"); return; }
        if (!_useItem)
        {
            if (_g.P[Me].Cost < Items.Cost(item)) { AddLog($"コスト不足(必要{Items.Cost(item)})"); return; }
            if (item == ItemType.Reshuffle)
            {
                int total = _g.P[0].Hand.Count + _g.P[1].Hand.Count;
                if (total < _g.PlayerCount * 7) { AddLog($"③は全手札{_g.PlayerCount * 7}枚以上で発動(現在{total})"); return; }
            }
        }
        _useItem = !_useItem;
        if (_useItem) _itemPhase = _phase == UiPhase.Front ? Phase.Front : Phase.Back;
        RefreshAll();
    }

    void ToggleDiscard()
    {
        if (_discard != null) { AddLog("このラウンドは破棄済み(前後半で1枚のみ)"); RefreshAll(); return; }
        _discardMode = !_discardMode;
        if (!_discardMode) _discardCandidate = null;
        RefreshAll();
    }

    void ConfirmDiscard()
    {
        if (!_discardMode || _discardCandidate == null) { AddLog("先に捨てるカードを選んでください"); return; }
        _discard = _discardCandidate; _discardCandidate = null; _discardMode = false;
        AddLog($"破棄:{_discard}(コスト+1)");
        RefreshAll();
    }

    void ToggleSign() { _sign = -_sign; RefreshAll(); }

    // ============ 表示更新 ============
    void RefreshAll()
    {
        if (_g == null) return;
        var me = _g.P[Me]; var opp = _g.P[Cpu];
        _oppHpText.text = $"相手(CPU) HP {opp.Hp} / {_g.HpMax}";
        _youHpText.text = $"あなた HP {me.Hp} / {_g.HpMax}";
        UpdateBar(_oppFill, opp.Hp);
        UpdateBar(_youFill, me.Hp);
        bool itemDone = _myPlan != null && _myPlan.UseItem;
        _oppItemText.text = $"相手アイテム(公開):{Items.Name(opp.Item)}";
        _youItemText.text = $"アイテム:{Items.Name(me.Item)}{(itemDone || _useItem ? " [使用]" : "")}";
        _youCostText.text = $"コスト:{me.Cost}";
        string discInfo = _discard != null ? "  破棄:" + _discard
                        : _discardMode ? (_discardCandidate != null ? "  破棄候補:" + _discardCandidate + " →「破棄確定」" : "  ◀ どれを捨てますか?(カードを選択)")
                        : "";
        _stageText.text = "場(あなた) 前半:" + Join(_front) + "  後半:" + Join(_back) + discInfo;
        _signLabel.text = _sign > 0 ? "符号+" : "符号-";
        _confirmLabel.text = _phase == UiPhase.Front ? "前半 確定" : "後半 確定";
        _itemLabel.text = itemDone ? "使用済" : (_useItem ? "アイテム[ON]" : "アイテム使用");
        _previewText.text = $"あなたの合計(予測):{PreviewFinal()}";
        UpdateDirBanner();
        BuildHand();
        ClampLog();
        _logText.text = string.Join("\n", _log);
    }

    void UpdateDirBanner()
    {
        Color blue = new Color(0.35f, 0.62f, 1f);    // 相手に与える
        Color red = new Color(0.97f, 0.42f, 0.42f);  // 自分に与える(自爆)
        Color neutral = new Color(0.8f, 0.83f, 0.88f);
        if (!_coinKnown)
        {
            _dirBanner.text = "コイン未公開 — 後半で命中先が決まる";
            _dirBanner.color = neutral; _previewText.color = neutral;
            return;
        }
        int f = PreviewFinal();
        if (f == 0)
        {
            _dirBanner.text = "合計 0 — 何も起きない";
            _dirBanner.color = neutral; _previewText.color = neutral;
            return;
        }
        int cp = _coin == Coin.Odd ? 1 : 0;
        bool hitsOpp = (((f % 2) + 2) % 2) == cp;
        _dirBanner.text = hitsOpp ? $"▶ 相手に攻撃！(合計 {f})" : $"▶ 自分に自爆！(合計 {f})";
        Color col = hitsOpp ? blue : red;
        _dirBanner.color = col; _previewText.color = col;   // 右上の合計も同色(相手=青/自分=赤)
    }

    void BuildHand()
    {
        foreach (Transform t in _handRow) Destroy(t.gameObject);
        if (_g == null) return;
        var hand = _g.P[Me].Hand;
        var sel = _phase == UiPhase.Front ? _front : _back;
        float x = 0;
        foreach (var c in hand)
        {
            if (c == _discard) continue;                                  // 破棄確定済みは手札から消す
            if (_phase == UiPhase.Back && _front.Contains(c)) continue;   // 前半で出した札は手札から消す
            bool isCand = _discardMode && c == _discardCandidate;
            bool isSel = _discardMode ? isCand : sel.Contains(c);
            var captured = c;
            MakeCard(_handRow, x, isSel ? -12 : 0, c, isCand, isSel, () => OnCardClicked(captured));
            x += 70;
        }
    }

    int PreviewFinal()
    {
        // 前半アイテム:確定済み(_myPlan)＋前半中の意思 / 後半アイテム:後半中の意思
        bool frontItem = (_myPlan != null && _myPlan.UseItem && _myPlan.ItemPhase == Phase.Front)
                       || (_phase == UiPhase.Front && _useItem && _itemPhase == Phase.Front);
        bool backItem = _phase == UiPhase.Back && _useItem && _itemPhase == Phase.Back;
        bool gold = false; int count = _front.Count + _back.Count;
        foreach (var c in _front) if (c.IsGold) gold = true;
        foreach (var c in _back) if (c.IsGold) gold = true;
        bool absF = frontItem && _myItem == ItemType.AbsBoost;
        bool absB = backItem && _myItem == ItemType.AbsBoost;
        int shiftF = frontItem && _myItem == ItemType.DoubleShift ? _sign * 7 : 0;
        int shiftB = backItem && _myItem == ItemType.DoubleShift ? _sign * 7 : 0;
        if (gold)
        {
            int t = _sign * 9 * count;
            if (absF || absB) t = t >= 0 ? t + 8 : t - 8;
            return t + shiftF + shiftB;
        }
        int fr = GameState.ComputePhase(_front, absF, 0, out bool fb);
        int bk = GameState.ComputePhase(_back, absB, 0, out bool bb);
        if (fb) fr += shiftF;
        if (bb) bk += shiftB;
        return fr + bk;
    }

    static string Join(List<Card> cs) { if (cs.Count == 0) return "—"; var s = ""; foreach (var c in cs) s += c + " "; return s.Trim(); }
    void AddLog(string s) { _log.Add(s); }
    void ClampLog() { while (_log.Count > 13) _log.RemoveAt(0); }
    void UpdateBar(Image fill, int hp)
    {
        float r = _g.HpMax > _g.HpMin ? (float)(hp - _g.HpMin) / (_g.HpMax - _g.HpMin) : 0;
        var rt = fill.rectTransform; rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(Mathf.Clamp01(r), 1);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    // ============ uGUI生成ヘルパー(左上原点・ピクセル指定)============
    RectTransform MakePanel(Transform parent, float x, float y, float w, float h, Color col)
    {
        var go = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = col;
        return Place(go, x, y, w, h);
    }

    Text MakeText(Transform parent, float x, float y, float w, float h, string s, int size, TextAnchor anchor)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = _font; t.text = s; t.fontSize = size; t.color = Color.white; t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        Place(go, x, y, w, h);
        return t;
    }

    Button MakeButton(Transform parent, float x, float y, float w, float h, string label, UnityEngine.Events.UnityAction onClick, out Text lbl, Color? col = null)
    {
        var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = col ?? new Color(0.18f, 0.27f, 0.45f);
        var b = go.GetComponent<Button>();
        b.onClick.AddListener(onClick);
        Place(go, x, y, w, h);
        var tgo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        tgo.transform.SetParent(go.transform, false);
        lbl = tgo.GetComponent<Text>();
        lbl.font = _font; lbl.text = label; lbl.fontSize = 18; lbl.color = Color.white; lbl.alignment = TextAnchor.MiddleCenter;
        lbl.horizontalOverflow = HorizontalWrapMode.Wrap;
        var rt = tgo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return b;
    }

    // カード:白地 + 色の枠 + 色の数字/文字(ブルー=青/レッド=赤/ゴールド=金)
    void MakeCard(Transform parent, float x, float yRaise, Card c, bool candidate, bool selected, UnityEngine.Events.UnityAction onClick)
    {
        Color accent = c.IsBlue ? new Color(0.13f, 0.40f, 0.82f)
                     : c.IsGold ? new Color(0.80f, 0.61f, 0.10f)
                     : new Color(0.82f, 0.20f, 0.24f);
        // 外枠(=ボタン本体・色)
        var go = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = accent;
        go.GetComponent<Button>().onClick.AddListener(onClick);
        Place(go, x, yRaise, 64, 84);
        // 中身(白)。選択中は枠を太く見せる
        float pad = selected ? 7f : 4f;
        var inner = new GameObject("Inner", typeof(RectTransform), typeof(Image));
        inner.transform.SetParent(go.transform, false);
        var ii = inner.GetComponent<Image>();
        ii.color = candidate ? new Color(0.86f, 0.86f, 0.9f) : Color.white;
        ii.raycastTarget = false;
        var irt = inner.GetComponent<RectTransform>();
        irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
        irt.offsetMin = new Vector2(pad, pad); irt.offsetMax = new Vector2(-pad, -pad);
        // 数字/文字(色)
        var tgo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        tgo.transform.SetParent(go.transform, false);
        var lbl = tgo.GetComponent<Text>();
        lbl.font = _font; lbl.text = c.ToString(); lbl.fontSize = 24; lbl.fontStyle = FontStyle.Bold;
        lbl.color = accent; lbl.alignment = TextAnchor.MiddleCenter; lbl.raycastTarget = false;
        var lrt = tgo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
    }

    Image MakeBar(Transform parent, float x, float y, float w, float h, Color fillColor)
    {
        var bgRt = MakePanel(parent, x, y, w, h, new Color(0.15f, 0.2f, 0.3f));
        var fgo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fgo.transform.SetParent(bgRt, false);
        fgo.GetComponent<Image>().color = fillColor;
        var rt = fgo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = new Vector2(0.5f, 1); rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return fgo.GetComponent<Image>();
    }

    // 左上(0,0)起点で x は右、y は下方向(ピクセル)。1280x720基準。
    RectTransform Place(GameObject go, float x, float y, float w, float h)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }
}
