// ±9 ルールエンジン — ゲーム状態と1ターン解決(新ルール / まず 1v1)
// 新ルール: コイン先出し / 手札6・前半2後半1出し / ゾロ目ドローボーナス / 5枚一致±45
//           エスカレーション / チャージ(旧破棄) / アイテム3種(①コイン反転 ②符号反転 ③ダブルシフト±7)
using System;
using System.Collections.Generic;

namespace PlusMinusNine
{

    // アイテム3種(新ルール): ①コイン反転 / ②符号反転 / ③ダブルシフト±7
    public enum ItemType { None, CoinFlip, SignFlip, DoubleShift }
    public enum Coin { Odd, Even }
    public enum Phase { Front, Back }
    public enum Direction { None, ToOpponent, ToSelf }

    public static class Items
    {
        // コスト(暫定値): ①6 / ②3 / ③2
        public static int Cost(ItemType t) => t switch
        {
            ItemType.CoinFlip => 6,
            ItemType.SignFlip => 3,
            ItemType.DoubleShift => 2,
            _ => 0
        };

        // 開始コスト: ① のみ 2、その他 1
        public static int StartCost(ItemType t) => t == ItemType.CoinFlip ? 2 : 1;

        public static string Name(ItemType t) => t switch
        {
            ItemType.CoinFlip => "①コイン反転",
            ItemType.SignFlip => "②符号反転",
            ItemType.DoubleShift => "③ダブルシフト±7",
            _ => "なし"
        };

        // 使用可能フェイズ(①は後半のみ。②③は前半/後半どちらでも)
        public static bool CanUseInPhase(ItemType t, Phase ph) =>
            t != ItemType.CoinFlip || ph == Phase.Back;
    }

    public sealed class Player
    {
        public string Name;
        public int Hp = 100;
        public List<Card> Hand = new();
        public ItemType Item = ItemType.None;
        public int Cost;            // チャージ累計(非公開運用)
        public int ZoroBonus;       // 次ラウンドのゾロ目ドローボーナス(0..3)
        public bool Defeated;       // 多人数用(1v1では即決着)
        public Player(string name) { Name = name; }
    }

    /// <summary>1ターン中の片側プレイヤーの手。前半/後半に分けて作る。</summary>
    public sealed class TurnPlan
    {
        public List<Card> Front = new();
        public List<Card> Back = new();
        public bool UseItem;          // このターンにアイテムを使うか
        public Phase ItemPhase;       // 使うフェイズ
        public int ShiftSign = 1;     // ③ダブルシフトの符号(+1/-1)
        public int GoldSign = 1;      // ゴールド/5枚一致の符号(+1/-1)
        public Card Charge;           // チャージ(旧「破棄」)。1ラウンド1枚
    }

    public sealed class TurnResult
    {
        public Coin TossedCoin;       // ラウンド頭に公開した当たり偶奇
        public Coin FinalCoin;        // ①コイン反転後の最終当たり偶奇
        public int[] FinalTotal = new int[2];
        public Direction[] Dir = new Direction[2];
        public int[] HpBefore = new int[2];
        public int[] HpAfter = new int[2];
        public int Escalation = 1;    // このラウンドのエスカレーション倍率
        public bool Overtime;
        public List<string> Log = new();
    }

    public sealed class GameState
    {
        public Player[] P = new Player[2];
        public List<Card> DeckPile = new();
        public List<Card> Discard = new();
        public Random Rng;
        public int Turn;            // 1始まり
        public int HpMin = 0, HpMax = 200, HpStart = 100;
        public bool Overtime;       // オーバータイム中か
        public int Winner = -1;     // -1=継続, 0/1=勝者
        public bool Verbose;

        public const int HandBase = 6;   // 通常の補充目標
        public const int HandCap = 8;    // ゾロ目上乗せ込みの上限

        public int PlayerCount => 2;

        public GameState(ItemType item0, ItemType item1, int seed)
        {
            Rng = new Random(seed);
            P[0] = new Player("P0") { Item = item0, Cost = Items.StartCost(item0) };
            P[1] = new Player("P1") { Item = item1, Cost = Items.StartCost(item1) };
            DeckPile = Deck.BuildFull();
            Deck.Shuffle(DeckPile, Rng);
            foreach (var pl in P) DrawTo(pl, HandBase);
        }

        // ---- 山札・ドロー ----
        void EnsureDeck(int need)
        {
            // 山札が P×5 枚以下になったら捨て札をリシャッフルして補充
            if (DeckPile.Count > PlayerCount * 5 && DeckPile.Count >= need) return;
            if (Discard.Count == 0) return;
            DeckPile.AddRange(Discard);
            Discard.Clear();
            Deck.Shuffle(DeckPile, Rng);
        }

        Card DrawOne()
        {
            EnsureDeck(1);
            if (DeckPile.Count == 0) return null;
            var c = DeckPile[^1];
            DeckPile.RemoveAt(DeckPile.Count - 1);
            return c;
        }

        public void DrawTo(Player pl, int target)
        {
            while (pl.Hand.Count < target)
            {
                var c = DrawOne();
                if (c == null) break;
                pl.Hand.Add(c);
            }
        }

        // ターン頭補充: 手札を「6 + 前ラウンドのゾロ目ボーナス」まで(上限8)
        void TurnDraw(Player pl)
        {
            EnsureDeck(2);
            int target = Math.Min(HandBase + pl.ZoroBonus, HandCap);
            DrawTo(pl, target);
            pl.ZoroBonus = 0;
        }

        // ---- エスカレーション倍率(5Rまで×1・以降3Rごと+×1)----
        // 1..5→1, 6..8→2, 9..11→3, ...
        public static int EscalationMult(int turn) => turn <= 5 ? 1 : 2 + (turn - 6) / 3;

        // ---- フェイズ合計の計算 ----
        // cards: そのフェイズに出した札。shift7: ③ダブルシフトの±7(無ければ0)。
        // 「ブルー必須」: ブルーが1枚も無ければ 0(レッド/③は乗らない)。
        public static int ComputePhase(List<Card> cards, int shift7, out bool hadBlue)
        {
            hadBlue = false;
            if (cards == null || cards.Count == 0) return 0;
            int blueSum = 0; bool anyBlue = false;
            foreach (var c in cards) if (c.IsBlue) { blueSum += c.Blue; anyBlue = true; }
            if (!anyBlue) { hadBlue = false; return 0; }
            hadBlue = true;

            // 足し算(ブルー合計)を先に処理 → レッドを左から適用
            int val = blueSum;
            int blueSign = Math.Sign(blueSum);
            foreach (var c in cards)
            {
                if (c.Kind != CardKind.Red) continue;
                if (c.IsMultiplier) val *= c.Multiplier;
                else if (c.IsPlus5) val += 5 * (blueSign == 0 ? 0 : blueSign);
            }
            // ③ ±7(ブルーがあるので乗る)
            val += shift7;
            return val;
        }

        static int Parity(int v) => ((v % 2) + 2) % 2; // 0=偶,1=奇

        /// <summary>同じブルー数字を5枚以上出していれば true(5枚一致→±45)。揃った数字の符号を返す。</summary>
        public static bool FiveMatch(TurnPlan plan, out int matchSign)
        {
            matchSign = 1;
            var counts = new Dictionary<int, int>();
            void Tally(List<Card> cs)
            {
                foreach (var c in cs)
                    if (c.IsBlue) { counts.TryGetValue(c.Blue, out int n); counts[c.Blue] = n + 1; }
            }
            Tally(plan.Front); Tally(plan.Back);
            foreach (var kv in counts)
                if (kv.Value >= 5) { matchSign = kv.Key < 0 ? -1 : 1; return true; }
            return false;
        }

        // ゾロ目ドローボーナス: 同数字の最大枚数 2→+1 / 3→+2 / 4以上→+3
        static int ZoroDrawBonus(TurnPlan plan)
        {
            var counts = new Dictionary<int, int>();
            void Tally(List<Card> cs)
            {
                foreach (var c in cs)
                    if (c.IsBlue) { counts.TryGetValue(c.Blue, out int n); counts[c.Blue] = n + 1; }
            }
            Tally(plan.Front); Tally(plan.Back);
            int max = 0;
            foreach (var kv in counts) if (kv.Value > max) max = kv.Value;
            if (max >= 4) return 3;
            if (max == 3) return 2;
            if (max == 2) return 1;
            return 0;
        }

        // ---- ステップ駆動API(Unityの対話プレイ用)----
        // 使い方: TurnHeadDraw() → TossCoin()公開 → 各自で前半TurnPlan作成 → CommitFront() → 各自で後半追記 → ResolveAfterFront()
        public void TurnHeadDraw()
        {
            Turn++;
            foreach (var pl in P) TurnDraw(pl);
        }

        public Coin TossCoin() => Rng.Next(2) == 0 ? Coin.Odd : Coin.Even;

        // ---- CPU vs CPU 用の一括解決(検証ランナー)----
        public TurnResult PlayTurn(IAgent a0, IAgent a1)
        {
            TurnHeadDraw();
            var coin = TossCoin();                 // ★ラウンド頭にコインを投げ当たり偶奇を公開
            var p0 = a0.DecideFront(this, 0, coin);
            var p1 = a1.DecideFront(this, 1, coin);
            CommitFront(p0, p1);
            a0.DecideBack(this, 0, coin, p0);
            a1.DecideBack(this, 1, coin, p1);
            return ResolveAfterFront(p0, p1, coin);
        }

        // ---- 前半コミット:チャージ・前半アイテム・前半札を反映 ----
        public void CommitFront(TurnPlan p0, TurnPlan p1)
        {
            var plans = new[] { p0, p1 };
            for (int i = 0; i < 2; i++) ApplyChargeAndFrontItem(P[i], plans[i]);
        }

        // 後半に確定したチャージを即時適用(UI用。前半チャージは CommitFront が処理)
        public void ApplyChargeNow(int idx, Card c)
        {
            if (c == null) return;
            if (P[idx].Hand.Remove(c)) { Discard.Add(c); P[idx].Cost += 1; }
        }

        // ---- 後半解決:後半アイテム・スコア計算・HP反映・勝敗 ----
        public TurnResult ResolveAfterFront(TurnPlan p0, TurnPlan p1, Coin tossed)
        {
            var res = new TurnResult();
            var plans = new[] { p0, p1 };
            for (int i = 0; i < 2; i++) res.HpBefore[i] = P[i].Hp;
            res.TossedCoin = tossed;

            // 後半:後半アイテム・後半札を反映
            for (int i = 0; i < 2; i++) ApplyBackItem(P[i], plans[i]);

            // ① コイン反転(後半に使用)→ 後半終了後にここで判明。偶数回で相殺
            int flips = 0;
            for (int i = 0; i < 2; i++)
                if (plans[i].UseItem && plans[i].ItemPhase == Phase.Back && P[i].Item == ItemType.CoinFlip) flips++;
            var finalCoin = (flips % 2 == 1) ? Flip(tossed) : tossed;
            res.FinalCoin = finalCoin;

            // ③ ダブルシフト ±7(フェイズ別・敵味方全員に作用)
            int frontShift = 0, backShift = 0;
            for (int i = 0; i < 2; i++)
                if (plans[i].UseItem && P[i].Item == ItemType.DoubleShift)
                {
                    if (plans[i].ItemPhase == Phase.Front) frontShift += plans[i].ShiftSign * 7;
                    else backShift += plans[i].ShiftSign * 7;
                }

            // 各プレイヤーの最終合計
            for (int i = 0; i < 2; i++)
                res.FinalTotal[i] = ComputeFinal(P[i], plans[i], frontShift, backShift);

            // 送り先判定(当たり偶奇と最終合計の偶奇)
            int coinPar = finalCoin == Coin.Odd ? 1 : 0;
            for (int i = 0; i < 2; i++)
            {
                int t = res.FinalTotal[i];
                if (t == 0) res.Dir[i] = Direction.None;
                else res.Dir[i] = (Parity(t) == coinPar) ? Direction.ToOpponent : Direction.ToSelf;
            }

            // エスカレーション倍率
            int mult = EscalationMult(Turn);
            res.Escalation = mult;

            // HP反映(同時着弾・倍率込み)
            var delta = new int[2];
            for (int i = 0; i < 2; i++)
            {
                if (res.Dir[i] == Direction.ToOpponent) delta[1 - i] += res.FinalTotal[i];
                else if (res.Dir[i] == Direction.ToSelf) delta[i] += res.FinalTotal[i];
            }
            for (int i = 0; i < 2; i++)
                P[i].Hp = Math.Clamp(P[i].Hp + delta[i] * mult, HpMin, HpMax);

            // ゾロ目ドローボーナス(次ラウンドへ予約)
            for (int i = 0; i < 2; i++) P[i].ZoroBonus = ZoroDrawBonus(plans[i]);

            // 使った札・チャージを捨て札へ
            for (int i = 0; i < 2; i++) MoveToDiscard(P[i], plans[i]);

            for (int i = 0; i < 2; i++) res.HpAfter[i] = P[i].Hp;

            // 勝敗判定(1v1)
            bool d0 = P[0].Hp <= HpMin || P[0].Hp >= HpMax;
            bool d1 = P[1].Hp <= HpMin || P[1].Hp >= HpMax;
            if (d0 && d1) StartOvertime(res);
            else if (d0) Winner = 1;
            else if (d1) Winner = 0;

            return res;
        }

        static Coin Flip(Coin c) => c == Coin.Odd ? Coin.Even : Coin.Odd;

        int ComputeFinal(Player pl, TurnPlan plan, int frontShift, int backShift)
        {
            int count = plan.Front.Count + plan.Back.Count;
            bool gold = false;
            foreach (var c in plan.Front) if (c.IsGold) gold = true;
            foreach (var c in plan.Back) if (c.IsGold) gold = true;

            int total;
            if (gold)
            {
                // ゴールド: そのターンの全札が±9化(最大±45)。簡略でシフトはまとめて加算
                total = plan.GoldSign * 9 * count + frontShift + backShift;
            }
            else if (FiveMatch(plan, out int ms))
            {
                // 5枚一致: 絶対値9化 = ±45
                total = ms * 45 + frontShift + backShift;
            }
            else
            {
                int fr = ComputePhase(plan.Front, 0, out bool fb);
                int bk = ComputePhase(plan.Back, 0, out bool bb);
                if (fb) fr += frontShift;
                if (bb) bk += backShift;
                total = fr + bk;
            }

            // ② 符号反転(偶奇は不変)
            if (plan.UseItem && pl.Item == ItemType.SignFlip) total = -total;
            return total;
        }

        // チャージ(コスト+1)と前半アイテムのコスト処理＋前半札を手札から除去
        void ApplyChargeAndFrontItem(Player pl, TurnPlan plan)
        {
            if (plan.Charge != null)
            {
                pl.Hand.Remove(plan.Charge);
                Discard.Add(plan.Charge);
                pl.Cost += 1;
            }
            if (plan.UseItem && plan.ItemPhase == Phase.Front) PayItem(pl, plan);
            foreach (var c in plan.Front) pl.Hand.Remove(c);
        }

        void ApplyBackItem(Player pl, TurnPlan plan)
        {
            if (plan.UseItem && plan.ItemPhase == Phase.Back) PayItem(pl, plan);
            foreach (var c in plan.Back) pl.Hand.Remove(c);
        }

        void PayItem(Player pl, TurnPlan plan)
        {
            int cost = Items.Cost(pl.Item);
            if (pl.Cost < cost) { plan.UseItem = false; return; } // 払えないなら不発
            pl.Cost -= cost;
        }

        void MoveToDiscard(Player pl, TurnPlan plan)
        {
            foreach (var c in plan.Front) Discard.Add(c);
            foreach (var c in plan.Back) Discard.Add(c);
        }

        void StartOvertime(TurnResult res)
        {
            // 手札・捨て札を山札へ戻す → 両者HP50・可動域0〜100・先に100か0で負け
            Overtime = true;
            res.Overtime = true;
            for (int i = 0; i < 2; i++) { DeckPile.AddRange(P[i].Hand); P[i].Hand.Clear(); P[i].ZoroBonus = 0; }
            DeckPile.AddRange(Discard); Discard.Clear();
            Deck.Shuffle(DeckPile, Rng);
            HpMin = 0; HpMax = 100; HpStart = 50;
            for (int i = 0; i < 2; i++) { P[i].Hp = 50; DrawTo(P[i], HandBase); }
            res.Log.Add("[オーバータイム] 両者HP50・先に100か0で負け");
        }
    }

}
