// ±9 ルールエンジン — ゲーム状態と1ターン解決(まず 1v1)
using System;
using System.Collections.Generic;

namespace PlusMinusNine
{

    public enum ItemType { None, CoinFlip, DrawBoost, Reshuffle, AbsBoost, DoubleShift }
    public enum Coin { Odd, Even }
    public enum Phase { Front, Back }
    public enum Direction { None, ToOpponent, ToSelf }

    public static class Items
    {
        // コスト(暫定値)。CoinFlip=6, DrawBoost=0, Reshuffle=3, AbsBoost=4, DoubleShift=4
        public static int Cost(ItemType t) => t switch
        {
            ItemType.CoinFlip => 6,
            ItemType.DrawBoost => 0,
            ItemType.Reshuffle => 3,
            ItemType.AbsBoost => 4,
            ItemType.DoubleShift => 4,
            _ => 0
        };
        // 開始コスト:① CoinFlip のみ 2、その他 1
        public static int StartCost(ItemType t) => t == ItemType.CoinFlip ? 2 : 1;

        public static string Name(ItemType t) => t switch
        {
            ItemType.CoinFlip => "①コイン反転",
            ItemType.DrawBoost => "②序盤ドローブースト",
            ItemType.Reshuffle => "③強制リシャッフル",
            ItemType.AbsBoost => "④絶対値+8",
            ItemType.DoubleShift => "⑤ダブルシフト±7",
            _ => "なし"
        };
    }

    public sealed class Player
    {
        public string Name;
        public int Hp = 100;
        public List<Card> Hand = new();
        public ItemType Item = ItemType.None;
        public int Cost;
        public bool Defeated;     // 多人数用(1v1では即決着)
        public Player(string name) { Name = name; }
    }

    /// <summary>1ターン中の片側プレイヤーの手。前半/後半に分けて作る。</summary>
    public sealed class TurnPlan
    {
        public List<Card> Front = new();
        public List<Card> Back = new();
        public bool UseItem;          // このターンにアイテムを使うか
        public Phase ItemPhase;       // 使うフェイズ
        public int ShiftSign = 1;     // ⑤ の符号(+1/-1)
        public int GoldSign = 1;      // ゴールドの符号(+1/-1)
        public Card Discard;          // 破棄カード(1ターン1枚)
    }

    public sealed class TurnResult
    {
        public Coin TossedCoin;
        public Coin FinalCoin;
        public int[] FinalTotal = new int[2];
        public Direction[] Dir = new Direction[2];
        public int[] HpBefore = new int[2];
        public int[] HpAfter = new int[2];
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
        public int Winner = -1;     // -1=継続, 0/1=勝者, 2=未決(両者敗北→OT移行済)
        public bool Verbose;
        int _pendingReshuffle = -1; // 後半③の予約(次ターン配布後に実行)

        public int PlayerCount => 2;

        public GameState(ItemType item0, ItemType item1, int seed)
        {
            Rng = new Random(seed);
            P[0] = new Player("P0") { Item = item0, Cost = Items.StartCost(item0) };
            P[1] = new Player("P1") { Item = item1, Cost = Items.StartCost(item1) };
            DeckPile = Deck.BuildFull();
            Deck.Shuffle(DeckPile, Rng);
            foreach (var pl in P) DrawTo(pl, 7, force: true);
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
            if (Verbose) LogLine("[山札補充] 捨て札をリシャッフル");
        }

        Card DrawOne()
        {
            EnsureDeck(1);
            if (DeckPile.Count == 0) return null;
            var c = DeckPile[^1];
            DeckPile.RemoveAt(DeckPile.Count - 1);
            return c;
        }

        public void DrawTo(Player pl, int target, bool force = false)
        {
            while (pl.Hand.Count < target)
            {
                var c = DrawOne();
                if (c == null) break;
                pl.Hand.Add(c);
            }
        }

        // ターン頭補充:手札≦6→7まで / ≧7→1枚 (② DrawBoost 中[1..5T]は +1)
        void TurnDraw(Player pl)
        {
            EnsureDeck(2);
            bool boost = pl.Item == ItemType.DrawBoost && Turn <= 5;
            if (pl.Hand.Count <= 6)
            {
                DrawTo(pl, boost ? 8 : 7);
            }
            else
            {
                int n = boost ? 2 : 1;
                for (int i = 0; i < n; i++) { var c = DrawOne(); if (c != null) pl.Hand.Add(c); }
            }
        }

        void LogLine(string s) { /* placeholder; per-turn log goes to TurnResult */ }

        // ---- フェイズ合計の計算 ----
        // cards: そのフェイズに出した札。abs8: ④を使ったか。shift7: ⑤の±7(無ければ0)。
        // 「ブルー必須」: ブルーが1枚も無ければ 0(レッド/④/⑤は乗らない)。
        public static int ComputePhase(List<Card> cards, bool abs8, int shift7, out bool hadBlue)
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
            // ④ 絶対値+8(符号保持)
            if (abs8) val = val >= 0 ? val + 8 : val - 8;
            // ⑤ ±7(ブルーがあるので乗る)
            val += shift7;
            return val;
        }

        static int Parity(int v) => ((v % 2) + 2) % 2; // 0=偶,1=奇

        // ---- ステップ駆動API(Unityの対話プレイ用)----
        // 使い方: TurnHeadDraw() → 各自で前半TurnPlan作成 → TossCoin() → 各自で後半追記 → Resolve(p0,p1,coin)
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
            RunPendingReshuffle();              // 前ターンの後半③をここで実行
            var p0 = a0.DecideFront(this, 0);
            var p1 = a1.DecideFront(this, 1);
            var coin = TossCoin();
            CommitFront(p0, p1);                // 前半③はここで即実行 → 後半は新しい手札から
            a0.DecideBack(this, 0, coin, p0);
            a1.DecideBack(this, 1, coin, p1);
            return ResolveAfterFront(p0, p1, coin);
        }

        // ---- 前半コミット:破棄・前半アイテム・前半札を反映。前半③はここで即リシャッフル ----
        public void CommitFront(TurnPlan p0, TurnPlan p1)
        {
            var plans = new[] { p0, p1 };
            for (int i = 0; i < 2; i++) ApplyDiscardAndFrontItem(P[i], plans[i]);
            for (int i = 0; i < 2; i++)
                if (plans[i].UseItem && plans[i].ItemPhase == Phase.Front && P[i].Item == ItemType.Reshuffle)
                    DoReshuffle(i, null);   // 前半使用 → 後半カードを出す前に手札を入れ替える
        }

        // 次ターン頭(配布後)に予約された後半③を実行
        public void RunPendingReshuffle()
        {
            if (_pendingReshuffle < 0) return;
            DoReshuffle(_pendingReshuffle, null);
            _pendingReshuffle = -1;
        }

        // 後半に確定した破棄を即時適用(UI用。前半破棄は CommitFront が処理)
        public void ApplyDiscardNow(int idx, Card c)
        {
            if (c == null) return;
            if (P[idx].Hand.Remove(c)) { Discard.Add(c); P[idx].Cost += 1; }
        }

        // ---- 後半解決:後半アイテム・スコア計算・HP反映・勝敗。後半③は次ターンへ予約 ----
        public TurnResult ResolveAfterFront(TurnPlan p0, TurnPlan p1, Coin tossed)
        {
            var res = new TurnResult();
            var plans = new[] { p0, p1 };
            for (int i = 0; i < 2; i++) res.HpBefore[i] = P[i].Hp;
            res.TossedCoin = tossed;

            // 後半:後半アイテム・後半札を反映
            for (int i = 0; i < 2; i++) ApplyBackItem(P[i], plans[i]);

            var coin = tossed;
            // ① コイン反転(後半に使用)→ 偶数回で相殺
            int flips = 0;
            for (int i = 0; i < 2; i++)
                if (plans[i].UseItem && plans[i].ItemPhase == Phase.Back && P[i].Item == ItemType.CoinFlip) flips++;
            var finalCoin = (flips % 2 == 1) ? (coin == Coin.Odd ? Coin.Even : Coin.Odd) : coin;
            res.FinalCoin = finalCoin;

            // ⑤ シフト量(フェイズ別、敵味方全員に作用)
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

            // 方向判定
            int coinPar = finalCoin == Coin.Odd ? 1 : 0;
            for (int i = 0; i < 2; i++)
            {
                int t = res.FinalTotal[i];
                if (t == 0) res.Dir[i] = Direction.None;
                else res.Dir[i] = (Parity(t) == coinPar) ? Direction.ToOpponent : Direction.ToSelf;
            }

            // HP反映(同時着弾)
            var delta = new int[2];
            for (int i = 0; i < 2; i++)
            {
                if (res.Dir[i] == Direction.ToOpponent) delta[1 - i] += res.FinalTotal[i];
                else if (res.Dir[i] == Direction.ToSelf) delta[i] += res.FinalTotal[i];
            }
            for (int i = 0; i < 2; i++)
                P[i].Hp = Math.Clamp(P[i].Hp + delta[i], HpMin, HpMax);

            // 使った札・破棄を捨て札へ
            for (int i = 0; i < 2; i++) MoveToDiscard(P[i], plans[i]);

            // ③ 強制リシャッフル(後半使用)→ 次ターンの配布後に予約
            for (int i = 0; i < 2; i++)
                if (plans[i].UseItem && plans[i].ItemPhase == Phase.Back && P[i].Item == ItemType.Reshuffle)
                    _pendingReshuffle = i;

            for (int i = 0; i < 2; i++) res.HpAfter[i] = P[i].Hp;

            // 勝敗判定(1v1)
            bool d0 = P[0].Hp <= HpMin || P[0].Hp >= HpMax;
            bool d1 = P[1].Hp <= HpMin || P[1].Hp >= HpMax;
            if (d0 && d1) { StartOvertime(res); }
            else if (d0) Winner = 1;
            else if (d1) Winner = 0;

            return res;
        }

        int ComputeFinal(Player pl, TurnPlan plan, int frontShift, int backShift)
        {
            bool goldThis = false;
            int count = 0;
            foreach (var c in plan.Front) { count++; if (c.IsGold) goldThis = true; }
            foreach (var c in plan.Back) { count++; if (c.IsGold) goldThis = true; }

            if (goldThis)
            {
                int total = plan.GoldSign * 9 * count;
                if (plan.UseItem && pl.Item == ItemType.AbsBoost) total = total >= 0 ? total + 8 : total - 8;
                total += frontShift + backShift; // 簡略:ゴールド時はまとめて加算
                return total;
            }

            bool absFront = plan.UseItem && pl.Item == ItemType.AbsBoost && plan.ItemPhase == Phase.Front;
            bool absBack = plan.UseItem && pl.Item == ItemType.AbsBoost && plan.ItemPhase == Phase.Back;
            int fr = ComputePhase(plan.Front, absFront, 0, out bool fb);
            int bk = ComputePhase(plan.Back, absBack, 0, out bool bb);
            // ⑤ は「ブルーのあるフェイズ」だけに乗る
            if (fb) fr += frontShift;
            if (bb) bk += backShift;
            return fr + bk;
        }

        // 破棄(コスト+1)と前半アイテムのコスト処理＋前半札を手札から除去
        void ApplyDiscardAndFrontItem(Player pl, TurnPlan plan)
        {
            if (plan.Discard != null)
            {
                pl.Hand.Remove(plan.Discard);
                Discard.Add(plan.Discard);
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

        void DoReshuffle(int casterIdx, List<string> log)
        {
            for (int i = 0; i < 2; i++)
            {
                DeckPile.AddRange(P[i].Hand);
                P[i].Hand.Clear();
            }
            Deck.Shuffle(DeckPile, Rng);
            DrawTo(P[casterIdx], 6, force: true);
            DrawTo(P[1 - casterIdx], 4, force: true);
            log?.Add($"[③強制リシャッフル] {P[casterIdx].Name} 発動");
        }

        void StartOvertime(TurnResult res)
        {
            // 手札・捨て札を山札へ戻す → 両者HP50・可動域0〜100・先に100か0で負け
            Overtime = true;
            res.Overtime = true;
            for (int i = 0; i < 2; i++) { DeckPile.AddRange(P[i].Hand); P[i].Hand.Clear(); }
            DeckPile.AddRange(Discard); Discard.Clear();
            Deck.Shuffle(DeckPile, Rng);
            HpMin = 0; HpMax = 100; HpStart = 50;
            for (int i = 0; i < 2; i++) { P[i].Hp = 50; DrawTo(P[i], 7, force: true); }
            res.Log.Add("[オーバータイム] 両者HP50・先に100か0で負け");
        }
    }

}