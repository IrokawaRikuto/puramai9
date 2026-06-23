// ±9 ルールエンジン — エージェント(プレイヤー操作の抽象)
// Unity では人間入力が IAgent を実装し、CPU は GreedyAi を使う。
// 新ルール: コインはラウンド頭に公開されるため、前半の決定時に当たり偶奇を渡す。
namespace PlusMinusNine
{

    public interface IAgent
    {
        /// <summary>前半の手を決めて TurnPlan を返す(コインは公開済み)。前半は2枚以上。</summary>
        TurnPlan DecideFront(GameState g, int me, Coin coin);
        /// <summary>後半(1枚以上)の手を、前半で作った plan に追記する。</summary>
        void DecideBack(GameState g, int me, Coin coin, TurnPlan plan);
    }

}
