// ±9 ルールエンジン — ヒューリスティックCPU(v0.2 / 新ルール)
// 方針:相手を近い端へ押し出す向きに火力を作り、公開済みのコイン(当たり偶奇)に合わせて命中先を相手にする。
// 新ルール対応:コイン先出し / 前半2枚以上・後半1枚以上 / チャージ / アイテム3種(①②③)。
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

        // ---- 部分集合の列挙(サイズ minK..maxK)----
        static List<List<Card>> Subsets(List<Card> src, int minK, int maxK)
        {
            var result = new List<List<Card>>();
            if (minK <= 0) result.Add(new List<Card>());
            int n = src.Count;
            for (int k = Math.Max(1, minK); k <= maxK && k <= n; k++)
            {
                var idx = new int[k];
                for (int i = 0; i < k; i++) idx[i] = i;
                while (true)
                {
                    var sub = new List<Card>(k);
                    for (int i = 0; i < k; i++) sub.Add(src[idx[i]]);
                    result.Add(sub);
                    int p = k - 1;
                    while (p >= 0 && idx[p] == n - k + p) p--;
                    if (p < 0) break;
                    idx[p]++;
                    for (int q = p + 1; q < k; q++) idx[q] = idx[q - 1] + 1;
                }
            }
            return result;
        }

        // 自分の plan の最終合計(自分のアイテムのみ考慮、相手の③は不明として無視)
        static int FinalOf(TurnPlan p, Player me)
        {
            int count = p.Front.Count + p.Back.Count;
            bool gold = false;
            foreach (var c in p.Front) if (c.IsGold) gold = true;
            foreach (var c in p.Back) if (c.IsGold) gold = true;

            int shiftF = p.UseItem && me.Item == ItemType.DoubleShift && p.ItemPhase == Phase.Front ? p.ShiftSign * 7 : 0;
            int shiftB = p.UseItem && me.Item == ItemType.DoubleShift && p.ItemPhase == Phase.Back ? p.ShiftSign * 7 : 0;

            int total;
            if (gold) total = p.GoldSign * 9 * count + shiftF + shiftB;
            else if (GameState.FiveMatch(p, out int ms)) total = ms * 45 + shiftF + shiftB;
            else
            {
                int fr = GameState.ComputePhase(p.Front, 0, out bool fb);
                int bk = GameState.ComputePhase(p.Back, 0, out bool bb);
                if (fb) fr += shiftF;
                if (bb) bk += shiftB;
                total = GameState.ApplyPlus5(fr + bk, GameState.Plus5Count(p));
            }
            if (p.UseItem && me.Item == ItemType.SignFlip) total = -total;
            return total;
        }

        static int Parity(int v) => ((v % 2) + 2) % 2;

        public TurnPlan DecideFront(GameState g, int me, Coin coin)
        {
            var pl = g.P[me];
            int sign = DesiredSign(g, me);
            var plan = new TurnPlan { GoldSign = sign, ShiftSign = sign };

            // チャージ:アイテム持ちでコスト不足なら最弱札を1枚チャージしてコストを貯める
            if (pl.Item != ItemType.None && pl.Cost < Items.Cost(pl.Item) && pl.Hand.Count >= 5)
                plan.Charge = WeakestBlue(pl.Hand);

            // 前半:符号方向に強い 2..3 枚(全部は出し切らない)。最低2枚は強制。
            var hand = new List<Card>(pl.Hand);
            if (plan.Charge != null) hand.Remove(plan.Charge);
            // 後半に最低1枚残すため、前半は最大 hand.Count-1 枚まで
            int frontMax = Math.Min(3, Math.Max(2, hand.Count - 1));
            var best = new List<Card>();
            int bestScore = int.MinValue;
            foreach (var sub in Subsets(hand, 2, frontMax))
            {
                int tot = GameState.ComputePhase(sub, 0, out _);
                int sc = sign * tot;
                if (sc > bestScore) { bestScore = sc; best = sub; }
            }
            // 念のため(手札が極端に少ない場合)最低2枚を確保
            if (best.Count < 2)
            {
                best = new List<Card>();
                foreach (var c in hand) { best.Add(c); if (best.Count >= 2) break; }
            }
            plan.Front = best;
            return plan;
        }

        public void DecideBack(GameState g, int me, Coin coin, TurnPlan plan)
        {
            var pl = g.P[me];
            int sign = DesiredSign(g, me);

            // 後半に使う札は、前半・チャージを除いた残り手札から
            var rest = new List<Card>(pl.Hand);
            foreach (var c in plan.Front) rest.Remove(c);
            if (plan.Charge != null) rest.Remove(plan.Charge);

            ItemType item = pl.Item;
            bool canPay = pl.Cost >= Items.Cost(item) && item != ItemType.None;

            var bestBack = new List<Card>();
            bool bestUseItem = false;
            int bestScore = int.MinValue;

            // 後半カード候補(1..2枚)× アイテム使用(なし/あり)を評価
            foreach (var backSub in Subsets(rest, 1, 2))
            {
                for (int useItem = 0; useItem < 2; useItem++)
                {
                    bool use = useItem == 1;
                    if (use)
                    {
                        if (!canPay) continue;
                        if (!Items.CanUseInPhase(item, Phase.Back)) continue; // 後半で使えるか(①も後半OK)
                    }
                    var trial = new TurnPlan
                    {
                        Front = plan.Front,
                        Back = backSub,
                        UseItem = use,
                        ItemPhase = Phase.Back,
                        ShiftSign = sign,
                        GoldSign = sign
                    };
                    int final = FinalOf(trial, pl);
                    Coin effCoin = coin;
                    if (use && item == ItemType.CoinFlip) effCoin = effCoin == Coin.Odd ? Coin.Even : Coin.Odd;
                    int ecPar = effCoin == Coin.Odd ? 1 : 0;

                    int score;
                    if (final == 0) score = -50;
                    else if (Parity(final) == ecPar) score = 1000 + sign * final; // 相手に命中
                    else score = -Math.Abs(final);                                // 自分に自爆

                    if (use) score -= 1; // アイテムの無駄打ち抑制

                    if (score > bestScore) { bestScore = score; bestBack = backSub; bestUseItem = use; }
                }
            }

            // 後半は最低1枚。候補が無ければ残りから1枚出す
            if (bestBack.Count == 0 && rest.Count > 0) bestBack = new List<Card> { rest[0] };

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
