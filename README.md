# ローカルAI 3DチャットUIシステム

## 📋 プロジェクト概要

このプロジェクトは、**Unity 3Dフロントエンドアプリケーション**です。ローカルAIバックエンドと連携し、3Dキャラクターがリアルタイムで対話するチャットシステムを提供します。

### 🔗 バックエンドシステム

このUnityアプリケーションは、以下のバックエンドシステムと連携します：

- **GitHubリポジトリ**: https://github.com/masu-katsu/local-ai-chatUI-system.git
- バックエンドの詳細なセットアップ手順やアーキテクチャについては、上記リポジトリを参照してください

### 🎯 主な特徴

- **3Dキャラクター対話**: Unityで実装された自律的な3DキャラクターがAI応答に合わせて行動
- **リアルタイム通信**: HTTP APIによるUnity-バックエンド間通信
- **自律的なキャラクターAI**: wait/walk/look/sitの状態遷移による自然な動き
- **モダンなチャットUI**: メッセージバブル、スクロール、日本語フォント対応

---

## 🏗️ システムアーキテクチャ

### Unityフロントエンド構成

```
┌─────────────────────────────────────────────────────────────┐
│                    Unity 3D フロントエンド                     │
│  (d:\local-ai-3DchatUI-system)                              │
│                                                              │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │ ChatUIManager│  │ ChatManager  │  │  ApiClient   │      │
│  │   (UI表示)    │  │  (チャット)   │  │  (HTTP通信)   │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
│         │                  │                  │              │
│         └──────────────────┼──────────────────┘              │
│                            │                                 │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │CharacterBrain│  │  3Dキャラクター│  │NetworkMonitor│      │
│  │  (行動制御)   │  │  (アニメーション)│ │ (接続監視)    │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└────────────────────────────┬────────────────────────────────┘
                             │ HTTP (ポート8000)
                             ▼
                    ┌─────────────────┐
                    │  バックエンドAPI  │
                    │ (GitHub参照)     │
                    └─────────────────┘
```

### コンポーネント詳細

**Unityアプリケーション構成**

```
Assets/
├── Scripts/
│   ├── API/
│   │   ├── ApiClient.cs           # HTTP通信クライアント
│   │   └── ApiModels.cs           # APIデータモデル
│   ├── Chat/
│   │   └── ChatManager.cs         # チャットロジック管理
│   ├── Character/
│   │   ├── CharacterBrain.cs      # キャラクター行動制御
│   │   ├── AnimationController.cs # アニメーション制御
│   │   ├── MovementController.cs  # 移動制御
│   │   └── CharacterStateMachine.cs # 状態管理
│   ├── UI/
│   │   ├── ChatUIManager.cs       # チャットUI管理
│   │   ├── SettingsUIManager.cs    # 設定UI管理
│   │   └── MessageBubble.cs        # メッセージ吹き出し
│   ├── Network/
│   │   └── NetworkMonitor.cs      # ネットワーク監視
│   ├── Core/
│   │   ├── AutonomousPlayBootstrap.cs # シーン自動構築
│   │   └── NavMeshRuntimeBuilder.cs    # NavMesh実行時生成
│   └── Navigation/
│       ├── WaypointManager.cs     # ウェイポイント管理
│       └── Waypoint.cs            # ウェイポイント定義
├── Scenes/
│   └── SampleScene.unity          # メインシーン
└── Prefabs/
    ├── AIMessageBubble.prefab      # AIメッセージ吹き出し
    └── UserMessageBubble.prefab    # ユーザーメッセージ吹き出し
```

**主要スクリプト説明**

- **ApiClient.cs**: FastAPIバックエンドとのHTTP通信を担当。認証、タイムアウト、エラーハンドリングを実装
- **ChatManager.cs**: チャットのロジックを管理。メッセージの送受信、イベント通知
- **ChatUIManager.cs**: チャットUIの表示制御。メッセージバブルの動的生成、スクロール管理
- **CharacterBrain.cs**: 3Dキャラクターの自律行動制御。wait/walk/look/sitの状態遷移
- **NetworkMonitor.cs**: バックエンドとの接続状態を定期的に監視
- **SettingsUIManager.cs**: サーバーURL、APIキー、ユーザーIDの設定管理

---

## 🔄 通信フロー

### ユーザー入力からAI応答まで

```
1. ユーザーがUnityでメッセージを入力
   ↓
2. ChatUIManagerが入力を受信
   ↓
3. ChatManagerがメッセージを処理
   ↓
4. ApiClientがHTTPリクエストをバックエンドAPIへ送信
   POST /api/chat
   {
     "message": "Pythonで階乗を計算する関数を作って",
     "user_id": "player_123"
   }
   ↓
5. バックエンドがAI処理を実行
   ↓
6. バックエンドが応答を返送
    {
      "response": "def factorial(n):\n    return 1 if n <= 1 else n * factorial(n-1)",
      "model_used": "qwen",
      "processing_time": 2.34,
      "context_used": true
    }
   ↓
7. ApiClientが応答を受信
   ↓
8. ChatManagerがメッセージを追加
   ↓
9. ChatUIManagerがメッセージバブルを表示
   ↓
10. CharacterBrainがキャラクターの行動を更新
```

---

## 🚀 セットアップ手順

### 前提条件

- Unity 2022.3以降
- バックエンドシステムが稼働していること
  - 詳細は: https://github.com/masu-katsu/local-ai-chatUI-system.git

### Unityプロジェクトのセットアップ

```bash
# Unityプロジェクトを開く
# d:\local-ai-3DchatUI-system

# 必要なUnityパッケージを確認
# - TextMeshPro (UI表示用)
# - Unity AI Navigation (キャラクター移動用)
```

### Unityでの設定

1. Unityプロジェクトを開く
2. `SampleScene` を開く
3. `LocalAI → Auto Wire References` を実行してUI参照を自動設定
4. `LocalAI → 3D model → Build` を実行して3D環境を構築
5. 実行モードでPlay

### 接続設定

Unity内の設定画面から以下を設定：
- **Server URL**: バックエンドのURL（例: `http://localhost:8000`）
- **API Key**: バックエンドで設定したAPIキー
- **User ID**: ユーザー識別子（任意）

---

## 🎮 Unityアプリケーションの使用方法

### 基本的な操作

1. **チャット送信**
   - テキスト入力フィールドにメッセージを入力
   - 送信ボタンをクリックまたはEnterキー

2. **設定変更**
   - 設定ボタン（⚙️アイコン）をクリック
   - Server URL、API Key、User IDを変更
   - 「接続テスト」で接続確認
   - 「保存」で設定を適用

3. **3Dキャラクターの行動**
   - キャラクターは自律的に以下の行動をとります：
     - **Wait**: 待機
     - **Walk**: ウェイポイント間を移動
     - **Look**: 周囲を見渡す
     - **Sit**: 座る

### UI構成

```
┌─────────────────────────────────────┐
│  Header                              │
│  [接続状態インジケーター] [設定ボタン] │
├─────────────────────────────────────┤
│  Message Area                       │
│  ┌─────────────────────────────┐   │
│  │ AI: こんにちは！             │   │
│  │    qwen | 履歴参照 14:30    │   │
│  └─────────────────────────────┘   │
│  ┌─────────────────────────────┐   │
│  │           こんにちは！        │   │
│  │                    14:31     │   │
│  └─────────────────────────────┘   │
├─────────────────────────────────────┤
│  Input Area                         │
│  [入力フィールド................] [送信]│
└─────────────────────────────────────┘
```

---

## 🔧 API仕様

### チャットエンドポイント

**リクエスト**
```http
POST /api/chat
Content-Type: application/json
X-API-Key: your-secret-key

{
  "message": "Pythonを教えて",
  "user_id": "player_123",
  "web_search_confirmed": false,
  "web_search_action": null
}
```

**レスポンス**
```json
{
  "response": "Pythonは...",
  "model_used": "qwen",
  "processing_time": 1.45,
  "context_used": true,
  "web_search_used": false,
  "requires_confirmation": false,
  "pending_web_search": "",
  "search_in_progress": false
}
```

### ヘルスチェックエンドポイント

**リクエスト**
```http
GET /api/health
```

**レスポンス**
```json
{
  "status": "healthy",
  "phi3": "ready",
  "qwen": "ready",
  "chromadb": "connected"
}
```

---

## 📁 ディレクトリ構成

```
local-ai-3DchatUI-system/
├── Assets/
│   ├── Scripts/                # C#スクリプト
│   │   ├── API/                # API通信
│   │   ├── Chat/               # チャット機能
│   │   ├── Character/          # キャラクター制御
│   │   ├── UI/                 # UI管理
│   │   ├── Network/            # ネットワーク監視
│   │   ├── Core/               # コア機能
│   │   └── Navigation/         # ナビゲーション
│   ├── Scenes/                # Unityシーン
│   ├── Prefabs/               # プレハブ
│   └── Resources/             # リソース
├── ProjectSettings/           # プロジェクト設定
└── README.md                  # 本ファイル
```

---

## 🧠 キャラクターAIシステム

### 行動状態マシン

キャラクターは以下の状態を自律的に遷移します：

```
Idle (待機)
  ↓
Walk (移動) → Look (見渡し) → Sit (座る)
  ↓           ↓              ↓
  └───────────┴──────────────┘
              ↓
           Idle (待機)
```

### 行動重み付け

各状態からの遷移確率を設定可能：

- **waitDuration**: 0.8〜3.5秒
- **lookDuration**: 1.5〜4.5秒
- **sitDuration**: 2.0〜5.5秒
- **repeatPenalty**: 同じ行動の繰り返しを抑制 (0.85)

### ウェイポイントシステム

- 10個のウェイポイントを自動配置
- キャラクターはウェイポイント間をランダムに移動
- NavMeshを使用した経路探索

---

## 🐛 トラブルシューティング

### Unity側の問題

**Q: キャラクターが表示されない**
- `LocalAI → 3D model → Build` を実行
- Assets/untitled.fbx が存在するか確認

**Q: UIが正しく表示されない**
- `LocalAI → Auto Wire References` を実行
- Canvasの設定を確認

**Q: バックエンドに接続できない**
- 設定画面でServer URLを確認
- NetworkMonitorが接続状態を監視
- バックエンドが起動しているか確認（バックエンドのREADMEを参照）

---

## 📦 依存パッケージ

### Unityパッケージ

- TextMeshPro (UI表示用)
- Unity AI Navigation (キャラクター移動用)
- (標準Unityコンポーネント)

---

## 📝 開発メモ

### Unityエディタ拡張

プロジェクトには以下のエディタ拡張が含まれています：

- **Auto Wire References**: UI参照の自動設定
- **3D model Build**: 3D環境の自動構築

これらはUnityエディタのメニュー `LocalAI` からアクセスできます。

### キャラクターモデルの変更

キャラクターモデルを変更する場合：

1. 新しいFBXモデルをAssetsにインポート
2. `AutonomousPlayBootstrap` の `characterModelPrefab` を設定
3. `LocalAI → 3D model → Build` を実行

---

## 📞 サポート

- **バックエンドシステム**: https://github.com/masu-katsu/local-ai-chatUI-system.git
- **問題報告**: GitHub Issues

---

## 📄 ライセンス

このプロジェクトはオープンソースです。改善提案やバグ報告を随時受け付けています。

---

**最終更新**: 2026年5月30日
