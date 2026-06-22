// ±9 ルールエンジン — エージェント(プレイヤー操作の抽象)
// Unity では人間入力が IAgent を実装し、CPU は GreedyAi を使う。
namespace PlusMinusNine
{

    public interface IAgent
    {
        /// <summary>前半(コイン前)の手を決めて TurnPlan を返す。</summary>
        TurnPlan DecideFront(GameState g, int me);
        /// <summary>後半(コインを見た後)の手を、前半で作った plan に追記する。</summary>
        void DecideBack(GameState g, int me, Coin coin, TurnPlan plan);
    }

}