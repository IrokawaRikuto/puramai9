// ±9 ルールエンジン — ヒューリスティックCPU(v0.1)
// 方針:相手を近い端へ押し出す方向に火力を作り、後半でコインに合わせて命中先を相手にする。
// ※ まず動かすための簡易AI。バランス用に後で強化する。
using System;
using System.Collections.Generic;

namespace PlusMinusNine
{

    public sealed class GreedyAi : IAgent
    {
        readonly Random _rng;
        public GreedyAi(int seed) { _rng = new Random(seed); }

        int DesiredSign(GameState g, int me)
        {
            var opp = g.P[1 - me];
            int mid = (g.HpMin + g.HpMax) / 2;
            // 相手が中央より上 → さらに上(+)へ押して上端で勝ち / 下なら下(-)端へ
            return opp.Hp >= mid ? +1 : -1;
        }

        // ---- 部分集合の列挙(サイズ 0..maxK)----
        static List<List<Card>> Subsets(List<Card> src, int maxK)
        {
            var result = new List<List<Card>> { new List<Card>() };
            int n = src.Count;
            for (int k = 1; k <= maxK; k++)
            {
                var idx = new int[k];
                for (int i = 0; i < k; i++) idx[i] = i;
                while (true)
                {
                    if (idx[k - 1] < n)
                    {
                        var sub = new List<Card>(k);
                        for (int i = 0; i < k; i++) sub.Add(src[idx[i]]);
                        result.Add(sub);
                    }
                    // 次の組み合わせ
                    int p = k - 1;
                    while (p >= 0 && idx[p] == n - k + p) p--;
                    if (p < 0) break;
                    idx[p]++;
                    for (int q = p + 1; q < k; q++) idx[q] = idx[q - 1] + 1;
                }
            }
            return result;
        }

        // 自分の plan の最終合計(自分の⑤のみ考慮、相手の⑤は不明として無視)
        static int FinalOf(TurnPlan p, Player me)
        {
            bool gold = false; int count = 0;
            foreach (var c in p.Front) { count++; if (c.IsGold) gold = true; }
            foreach (var c in p.Back) { count++; if (c.IsGold) gold = true; }
            if (gold)
            {
                int t = p.GoldSign * 9 * count;
                if (p.UseItem && me.Item == ItemType.AbsBoost) t = t >= 0 ? t + 8 : t - 8;
                return t;
            }
            bool absF = p.UseItem && me.Item == ItemType.AbsBoost && p.ItemPhase == Phase.Front;
            bool absB = p.UseItem && me.Item == ItemType.AbsBoost && p.ItemPhase == Phase.Back;
            int shiftF = p.UseItem && me.Item == ItemType.DoubleShift && p.ItemPhase == Phase.Front ? p.ShiftSign * 7 : 0;
            int shiftB = p.UseItem && me.Item == ItemType.DoubleShift && p.ItemPhase == Phase.Back ? p.ShiftSign * 7 : 0;
            int fr = GameState.ComputePhase(p.Front, absF, 0, out bool fb);
            int bk = GameState.ComputePhase(p.Back, absB, 0, out bool bb);
            if (fb) fr += shiftF;
            if (bb) bk += shiftB;
            return fr + bk;
        }

        static int Parity(int v) => ((v % 2) + 2) % 2;

        public TurnPlan DecideFront(GameState g, int me)
        {
            var pl = g.P[me];
            int sign = DesiredSign(g, me);
            var plan = new TurnPlan { GoldSign = sign, ShiftSign = sign };

            // 破棄:手札過多 or アイテム用コスト不足ならコスト稼ぎに最弱札を1枚
            if (pl.Item != ItemType.None && pl.Item != ItemType.DrawBoost &&
                pl.Cost < Items.Cost(pl.Item) && pl.Hand.Count >= 5)
            {
                plan.Discard = WeakestBlue(pl.Hand);
            }

            // 前半:符号方向に強い 0..2 枚(全部出し切らない)
            var hand = new List<Card>(pl.Hand);
            if (plan.Discard != null) hand.Remove(plan.Discard);
            var best = new List<Card>(); int bestScore = 0;
            foreach (var sub in Subsets(hand, 2))
            {
                int tot = GameState.ComputePhase(sub, false, 0, out _);
                int sc = sign * tot;
                if (sc > bestScore) { bestScore = sc; best = sub; }
            }
            plan.Front = best;

            // ③強制リシャッフル:両者手札が多いとき前半に発動
            if (pl.Item == ItemType.Reshuffle && pl.Cost >= Items.Cost(ItemType.Reshuffle))
            {
                int totalHands = g.P[0].Hand.Count + g.P[1].Hand.Count;
                if (totalHands >= g.PlayerCount * 7)   // 合計14枚以上(2人)で発動可
                {
                    plan.UseItem = true; plan.ItemPhase = Phase.Front;
                }
            }
            return plan;
        }

        public void DecideBack(GameState g, int me, Coin coin, TurnPlan plan)
        {
            var pl = g.P[me];
            int sign = DesiredSign(g, me);
            int coinPar = coin == Coin.Odd ? 1 : 0;

            // 後半に使う札は、前半・破棄を除いた残り手札から
            var rest = new List<Card>(pl.Hand);
            foreach (var c in plan.Front) rest.Remove(c);
            if (plan.Discard != null) rest.Remove(plan.Discard);

            bool itemFree = !plan.UseItem; // 前半で未使用ならアイテム候補
            ItemType item = pl.Item;
            bool canPay = pl.Cost >= Items.Cost(item);

            var bestBack = new List<Card>();
            bool bestUseItem = false; int bestScore = int.MinValue; Coin bestCoin = coin;

            // 後半カード候補 × アイテム使用(なし/あり)を評価
            foreach (var backSub in Subsets(rest, 2))
            {
                for (int useItem = 0; useItem < 2; useItem++)
                {
                    bool use = useItem == 1;
                    if (use)
                    {
                        if (!itemFree || !canPay) continue;
                        if (item == ItemType.DrawBoost || item == ItemType.Reshuffle) continue; // 後半ではこの2つは使わない
                    }
                    var trial = new TurnPlan
                    {
                        Front = plan.Front, Back = backSub,
                        UseItem = use, ItemPhase = Phase.Back,
                        ShiftSign = sign, GoldSign = sign
                    };
                    int final = FinalOf(trial, pl);
                    Coin effCoin = coin;
                    if (use && item == ItemType.CoinFlip) effCoin = coin == Coin.Odd ? Coin.Even : Coin.Odd;
                    int ecPar = effCoin == Coin.Odd ? 1 : 0;

                    int score;
                    if (final == 0) score = -50;
                    else if (Parity(final) == ecPar) // 相手に命中
                        score = 1000 + sign * final;
                    else // 自分に自爆
                        score = -Math.Abs(final);

                    // アイテムは少しコストペナルティ(無駄打ち抑制)
                    if (use) score -= 1;

                    if (score > bestScore) { bestScore = score; bestBack = backSub; bestUseItem = use; bestCoin = effCoin; }
                }
            }

            plan.Back = bestBack;
            if (bestUseItem) { plan.UseItem = true; plan.ItemPhase = Phase.Back; }
        }

        static Card WeakestBlue(List<Card> hand)
        {
            Card best = null; int bestAbs = int.MaxValue;
            foreach (var c in hand)
            {
                if (!c.IsBlue) continue;
                int a = Math.Abs(c.Blue);
                if (a < bestAbs) { bestAbs = a; best = c; }
            }
            return best ?? (hand.Count > 0 ? hand[0] : null);
        }
    }

}