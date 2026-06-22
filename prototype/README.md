# ±9(プラマイ・ナイン) プロトタイプ

ルールエンジン(純C#)を共有し、(1) .NETでバランス検証、(2) Unityで人 vs CPU 実プレイ、の2つで使う。

```
prototype/
├─ Sim/                         ← .NET 9 検証プロジェクト
│  ├─ Core/                     ← ★共有ルールエンジン(Unityへ流用するのはココ)
│  │  ├─ Cards.cs   … カード/デッキ(115枚)
│  │  ├─ Game.cs    … ゲーム状態・1ターン解決・コスト・オーバータイム
│  │  ├─ Agent.cs   … IAgent(操作の抽象)
│  │  └─ GreedyAi.cs… CPU(簡易ヒューリスティック)
│  ├─ Program.cs               … 検証ランナー(Unityには入れない)
│  └─ Sim.csproj
└─ Unity/
   └─ PlusMinusNineGame.cs      … Unity人vsCPU(uGUIをコード生成)
```

---

## 1) バランス検証(.NET・すぐ動く)

```
cd prototype/Sim
dotnet run -c Release
```

- サンプル1試合のログ + アイテム別 勝率マトリクス(14,400戦)が出る。
- `Program.cs` の `M`(試行数)や `Items.Cost(...)`(`Core/Game.cs`)を変えて、コスト調整→勝率変化を即確認できる。
- 現状の所見:**⑤±7・①コイン反転が強い / ②③が弱い**(※AIが簡易なため暫定)。

---

## 2) Unity 人 vs CPU(実プレイ版)

### セットアップ
1. **Unity Hub → New Project → Unity 6 (6000.0.70f1)** → テンプレートは **「2D (Built-In Render Pipeline)」** か「Universal 2D」。名前は任意(例 PlusMinusNine)。
2. **Project Settings → Player → Other Settings → Active Input Handling = 「Both」**(または Input Manager (Old))。
   - ※ uGUIのクリックと `Input`(Rキー)に必要。新Input System単独だと反応しません。
3. プロジェクトの `Assets/Scripts/` に、次をコピー:
   - **`Sim/Core/` フォルダ一式**(Cards.cs / Game.cs / Agent.cs / GreedyAi.cs)
   - **`Unity/PlusMinusNineGame.cs`**
   - ⚠ `Program.cs` と `Sim.csproj` は **入れない**(Unity用ではない)。
4. **▶ 再生するだけ**。`[RuntimeInitializeOnLoadMethod]` で自動起動するので、シーンにGameObjectを置く必要はありません(置いてもOK)。最初にアイテムを選んで対戦開始。

> ※ kamige- プロジェクトでは `puramai9/Assets/Scripts/` に配置済み。Unityを開いてコンパイルが通ったら再生するだけ。

### 操作
- **開始時**:持ち込みアイテムを選択(CPUはランダム)。
- **前半**:手札カードをクリックで選択(最大3枚)→「前半 確定」。
- **コイン公開**。
- **後半**:カード選択(最大2枚)→「後半 確定」。コインを見て偶奇を調整。
- 「アイテム使用」=このターンに発動(①は後半・③は前半のみ)。「符号±」=⑤/ゴールドの符号。
- 「破棄モード」ON→カードをクリックで破棄(1ターン1枚、コスト+1)。
- 画面右に直近ログ、左上/下にHPバー(0〜端で敗北)。
- 決着後 **R キー**でもう一度。

---

## 既知の簡略化(プロト v0.1)
- **③強制リシャッフル**はターン終了時に解決(本来はフェイズ中)。
- ドラッグ&ドロップ未対応(クリック選択)。
- CPUは簡易AI(②③の搦め手を活かしきれない)→ バランス数値は暫定。
- 多人数(2v2/バトロワ)・オンラインは未実装(エンジンはまず1v1)。
- C# は file-scoped namespace を使用(Unity 2021.2+/Unity 6 で対応)。万一エラーなら `namespace PlusMinusNine;` を `namespace PlusMinusNine { … }` に変えるだけ。

## 次の改善候補
- AI強化(②③を使う方策)→ 再測定でアイテム調整
- ドラッグ&ドロップ・演出・SE
- 多人数ルール、オンライン(Photon / Unity Gaming Services)
