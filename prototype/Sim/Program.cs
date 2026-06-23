// ±9 プロト — 動作確認 & バランス検証ランナー(1v1 / CPU vs CPU / 新ルール)
using System;
using System.Collections.Generic;
using PlusMinusNine;

const int MaxTurns = 300;

(int winner, int turns, int overtimes) RunGame(ItemType i0, ItemType i1, int seed, bool verbose = false)
{
    var g = new GameState(i0, i1, seed) { Verbose = verbose };
    var a0 = new GreedyAi(seed * 2 + 1);
    var a1 = new GreedyAi(seed * 2 + 2);
    int ot = 0;
    while (g.Winner == -1 && g.Turn < MaxTurns)
    {
        var r = g.PlayTurn(a0, a1);
        if (r.Overtime) ot++;
        if (verbose) PrintTurn(g, r);
    }
    return (g.Winner, g.Turn, ot);
}

void PrintTurn(GameState g, TurnResult r)
{
    string Dir(Direction d) => d switch { Direction.ToOpponent => "→相手", Direction.ToSelf => "→自分", _ => "無効" };
    string esc = r.Escalation > 1 ? $"  [×{r.Escalation}]" : "";
    Console.WriteLine($"T{g.Turn,-3} コイン:{(r.FinalCoin == Coin.Odd ? "奇" : "偶")}" +
        $"  P0合計={r.FinalTotal[0],4}({Dir(r.Dir[0])})  P1合計={r.FinalTotal[1],4}({Dir(r.Dir[1])})" +
        $"  HP {r.HpBefore[0]}->{r.HpAfter[0]} / {r.HpBefore[1]}->{r.HpAfter[1]}{esc}");
    foreach (var l in r.Log) Console.WriteLine("      " + l);
}

// ---- 1) 1試合の詳細ログ ----
Console.WriteLine("==== サンプル1試合(②符号反転 vs ①コイン反転) ====");
RunGame(ItemType.SignFlip, ItemType.CoinFlip, seed: 12345, verbose: true);

// ---- 2) バランス検証(アイテム別 勝率マトリクス) ----
Console.WriteLine("\n==== バランス検証 (各マッチアップ M=400戦) ====");
var items = new[] { ItemType.None, ItemType.CoinFlip, ItemType.SignFlip, ItemType.DoubleShift };
string Short(ItemType t) => t switch
{
    ItemType.None => "無",
    ItemType.CoinFlip => "①反転",
    ItemType.SignFlip => "②符号",
    ItemType.DoubleShift => "③±7",
    _ => "?"
};

int M = 400;
long totalTurns = 0, totalGames = 0, p0wins = 0, draws = 0, otGames = 0;

// 行=自分のアイテム, 値=その行アイテムを持った側(P0)の勝率
Console.Write("行＼列".PadRight(8));
foreach (var c in items) Console.Write(Short(c).PadLeft(7));
Console.WriteLine("   |  行平均");

foreach (var rowItem in items)
{
    double rowSum = 0; int rowCount = 0;
    Console.Write(Short(rowItem).PadRight(8));
    foreach (var colItem in items)
    {
        int win = 0, valid = 0;
        for (int s = 0; s < M; s++)
        {
            var (w, turns, ot) = RunGame(rowItem, colItem, seed: 1000 + s);
            totalGames++; totalTurns += turns;
            if (ot > 0) otGames++;
            if (w == 0) { win++; p0wins++; }
            else if (w == 1) { /* p1 win */ }
            else { draws++; } // タイムアウト(-1 のまま MaxTurns 到達)
            if (w == 0 || w == 1) valid++;
        }
        double wr = valid > 0 ? 100.0 * win / valid : 0;
        rowSum += wr; rowCount++;
        Console.Write($"{wr,6:0.0}%");
    }
    Console.WriteLine($"   | {rowSum / rowCount,6:0.0}%");
}

Console.WriteLine();
Console.WriteLine($"総ゲーム数 : {totalGames}");
Console.WriteLine($"先手(P0)勝率: {100.0 * p0wins / totalGames:0.0}%  (≈50%なら先手有利なし)");
Console.WriteLine($"平均ターン数: {(double)totalTurns / totalGames:0.0}");
Console.WriteLine($"OT発生率   : {100.0 * otGames / totalGames:0.0}%");
Console.WriteLine($"時間切れ(>{MaxTurns}T): {100.0 * draws / totalGames:0.0}%");
