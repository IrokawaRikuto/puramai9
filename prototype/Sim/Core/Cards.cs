// ±9(プラマイ・ナイン) ルールエンジン — カード/デッキ
// ※ このフォルダ(Core)は engine 非依存の純C#。Unity/サーバへそのまま流用する。
using System;
using System.Collections.Generic;

namespace PlusMinusNine
{

    public enum CardKind { Blue, Red, Gold }

    public enum RedOp { None, Mul2, MulNeg2, Mul3, MulNeg3, MulNeg1, Plus5 }

    /// <summary>1枚のカード。</summary>
    public sealed class Card
    {
        public CardKind Kind;
        public int Blue;     // Blue のとき -9..9
        public RedOp Op;     // Red のとき

        public bool IsBlue => Kind == CardKind.Blue;
        public bool IsGold => Kind == CardKind.Gold;
        public bool IsMultiplier => Op is RedOp.Mul2 or RedOp.MulNeg2 or RedOp.Mul3 or RedOp.MulNeg3 or RedOp.MulNeg1;
        public bool IsPlus5 => Op == RedOp.Plus5;

        public static Card NewBlue(int v) => new Card { Kind = CardKind.Blue, Blue = v };
        public static Card NewRed(RedOp o) => new Card { Kind = CardKind.Red, Op = o };
        public static Card NewGold() => new Card { Kind = CardKind.Gold };

        /// <summary>×N の倍率(±1/±2/±3)。Plus5 は 0 を返す。</summary>
        public int Multiplier => Op switch
        {
            RedOp.Mul2 => 2,
            RedOp.MulNeg2 => -2,
            RedOp.Mul3 => 3,
            RedOp.MulNeg3 => -3,
            RedOp.MulNeg1 => -1,
            _ => 0
        };

        public override string ToString()
        {
            if (Kind == CardKind.Blue) return Blue >= 0 ? "+" + Blue : Blue.ToString();
            if (Kind == CardKind.Gold) return "G±9";
            return Op switch
            {
                RedOp.Mul2 => "x2",
                RedOp.MulNeg2 => "x-2",
                RedOp.Mul3 => "x3",
                RedOp.MulNeg3 => "x-3",
                RedOp.MulNeg1 => "x-1",
                RedOp.Plus5 => "±5",
                _ => "?"
            };
        }
    }

    public static class Deck
    {
        /// <summary>共通デッキ115枚(ブルー95 + レッド19 + ゴールド1)を生成。</summary>
        public static List<Card> BuildFull()
        {
            var list = new List<Card>(115);
            // ブルー -9..9 を各5枚 = 95
            for (int v = -9; v <= 9; v++)
                for (int n = 0; n < 5; n++)
                    list.Add(Card.NewBlue(v));
            // レッド 19
            Add(list, RedOp.Mul2, 3);
            Add(list, RedOp.MulNeg2, 3);
            Add(list, RedOp.Mul3, 2);
            Add(list, RedOp.MulNeg3, 2);
            Add(list, RedOp.MulNeg1, 5);
            Add(list, RedOp.Plus5, 4);
            // ゴールド 1
            list.Add(Card.NewGold());
            return list;
        }

        static void Add(List<Card> list, RedOp op, int count)
        {
            for (int i = 0; i < count; i++) list.Add(Card.NewRed(op));
        }

        public static void Shuffle(List<Card> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }

}