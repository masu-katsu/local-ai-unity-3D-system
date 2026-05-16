# ============================================
# FastAPI メインサーバー（司令塔）
# ============================================
# すべてのリクエストはここを通る
# Unity → FastAPI → AI(Phi3/Qwen) → FastAPI → Unity
# ============================================

import os
import time
import logging
import asyncio
from fastapi import FastAPI, HTTPException, Request
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, Field
from typing import Optional

from app.router import AIRouter
from app.history import ConversationHistory
from app.web_search_detector import detect_web_search_request, get_confirmation_message
from app.web_log_manager import WebLogManager
from app.web_search_client import BingSearchClient

# ============================================
# ログ設定
# ============================================
# ログディレクトリを自動作成
os.makedirs("/app/logs/system", exist_ok=True)

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    handlers=[
        logging.StreamHandler(),
        logging.FileHandler("/app/logs/system/fastapi.log", encoding="utf-8"),
    ],
)
logger = logging.getLogger(__name__)

# ============================================
# 環境変数
# ============================================
API_KEY = os.getenv("API_KEY", "your-secret-key-here")
PHI3_URL = os.getenv("PHI3_URL", "http://phi3:8001")
QWEN_URL = os.getenv("QWEN_URL", "http://qwen:8002")
BING_API_KEY = os.getenv("BING_API_KEY", "")  # Bing Search API キー

# ============================================
# FastAPI アプリ初期化
# ============================================
app = FastAPI(
    title="ローカルAI 制御サーバー",
    description="Unity → FastAPI → AI の司令塔",
    version="1.0.0",
)

# CORS設定（Unity・スマホからのアクセスを許可）
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # 開発中は全許可、本番では制限する
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# ルーターと会話履歴の初期化
ai_router = AIRouter(phi3_url=PHI3_URL, qwen_url=QWEN_URL)
conversation_history = ConversationHistory()

# Web 検索関連の初期化
web_search_client = BingSearchClient(api_key=BING_API_KEY) if BING_API_KEY else None
web_log_manager = None  # 後で初期化

# 検索中のタスク管理
web_search_tasks = {}  # {user_id: asyncio.Task}
web_search_results = {}  # {user_id: list}


# ============================================
# リクエスト・レスポンスモデル
# ============================================
class ChatRequest(BaseModel):
    """ユーザーからのチャットリクエスト"""
    message: str = Field(..., description="ユーザーのメッセージ", min_length=1)
    user_id: str = Field(default="default_user", description="ユーザーID")
    force_model: Optional[str] = Field(
        default=None, description="AIを強制指定（phi3 / qwen）"
    )
    web_search_confirmed: bool = Field(
        default=False, description="Web検索許可ボタン押下時 True"
    )
    web_search_action: Optional[str] = Field(
        default=None, description="'stop' で検索停止"
    )


class ChatResponse(BaseModel):
    """AIからのレスポンス"""
    response: str = Field(..., description="AIの応答テキスト")
    model_used: str = Field(..., description="使用したAIモデル名")
    processing_time: float = Field(..., description="処理時間（秒）")
    context_used: bool = Field(..., description="過去の会話を参照したか")
    
    # Web 検索関連
    web_search_used: bool = Field(default=False, description="Web検索を実行したか")
    requires_confirmation: bool = Field(default=False, description="確認ダイアログ表示が必要か")
    pending_web_search: Optional[str] = Field(default=None, description="確認待ちのキーワード")
    search_in_progress: bool = Field(default=False, description="検索中か")


# ============================================
# APIキー認証ミドルウェア
# ============================================
@app.middleware("http")
async def verify_api_key(request: Request, call_next):
    # ヘルスチェックとドキュメントはスキップ
    skip_paths = ["/api/health", "/docs", "/openapi.json", "/redoc"]
    if request.url.path in skip_paths:
        return await call_next(request)

    # APIキーの検証
    api_key = request.headers.get("X-API-Key")
    if api_key != API_KEY:
        logger.warning(f"不正なAPIキー: {api_key} from {request.client.host}")
        raise HTTPException(status_code=401, detail="無効なAPIキーです")

    return await call_next(request)


# ============================================
# エンドポイント
# ============================================
@app.get("/api/health")
async def health_check():
    """ヘルスチェック - 各サービスの状態を確認"""
    phi3_status = await ai_router.check_health("phi3")
    qwen_status = await ai_router.check_health("qwen")

    return {
        "status": "running",
        "services": {
            "fastapi": "ok",
            "phi3": phi3_status,
            "qwen": qwen_status,
        },
    }


@app.post("/api/chat", response_model=ChatResponse)
async def chat(request: ChatRequest):
    """
    メインのチャットエンドポイント
    1. Web検索リクエストの検出
    2. Web検索の実行（許可時）
    3. 過去の会話と WEB ログを検索
    4. AIを振り分け
    5. 応答を生成
    6. 会話を保存
    """
    start_time = time.time()
    user_id = request.user_id
    
    logger.info(f"[{user_id}] リクエスト | web_action: {request.web_search_action}")
    
    # =========================================
    # Step 0: 停止指令の処理
    # =========================================
    if request.web_search_action == "stop":
        logger.info(f"[{user_id}] WEB検索停止指令を受け取った")
        
        # 検索タスクをキャンセル
        if user_id in web_search_tasks:
            task = web_search_tasks[user_id]
            task.cancel()
            del web_search_tasks[user_id]
        
        # ここまでの結果を取得
        partial_results = web_search_results.get(user_id, [])
        logger.info(f"[{user_id}] 途中結果: {len(partial_results)}件")
        
        # 以下、通常フロー続行（途中結果を使用）
        request.web_search_confirmed = False
        web_search_keyword = None
        web_results = partial_results
    else:
        web_search_keyword = None
        web_results = None
    
    # =========================================
    # Step 1: WEB検索リクエストの検出
    # =========================================
    if web_search_keyword is None:
        web_search_keyword = detect_web_search_request(request.message)
    
    if web_search_keyword:
        logger.info(f"[{user_id}] WEB検索リクエスト検出: {web_search_keyword}")
        
        # まだ確認されていない → ダイアログを返す
        if not request.web_search_confirmed:
            logger.info(f"  → 確認待ち")
            return ChatResponse(
                response=get_confirmation_message(web_search_keyword),
                model_used="system",
                processing_time=0,
                context_used=False,
                web_search_used=False,
                requires_confirmation=True,
                pending_web_search=web_search_keyword,
                search_in_progress=False,
            )
        
        # ユーザーが [許可] → 検索実行（既にキャンセルされていないなら）
        if web_results is None:
            logger.info(f"[{user_id}] → WEB検索開始")
            try:
                if web_search_client is None:
                    logger.warning("Bing API キーが未設定です")
                    web_results = []
                else:
                    # 非同期検索タスク（停止可能）
                    async def search_with_cancel():
                        results = []
                        try:
                            async def on_result_callback(result):
                                results.append(result)
                            
                            results = await web_search_client.search_incremental(
                                web_search_keyword,
                                on_result=on_result_callback
                            )
                        except asyncio.CancelledError:
                            logger.info(f"[{user_id}] 検索がキャンセルされた")
                            return results
                        return results
                    
                    # タスク作成＆実行
                    task = asyncio.create_task(search_with_cancel())
                    web_search_tasks[user_id] = task
                    web_search_results[user_id] = []
                    
                    # タスク完了を待つ（または時間切れまで）
                    try:
                        web_results = await asyncio.wait_for(task, timeout=25.0)
                    except asyncio.TimeoutError:
                        logger.warning(f"[{user_id}] WEB検索タイムアウト（15秒）")
                        task.cancel()
                        web_results = web_search_results.get(user_id, [])
                    finally:
                        if user_id in web_search_tasks:
                            del web_search_tasks[user_id]
                    
                    logger.info(f"[{user_id}] WEB検索完了: {len(web_results)}件")
                
                # Qwen で要約
                if web_results and web_log_manager:
                    try:
                        web_results_text = "\n".join([
                            f"タイトル: {r.get('title', '')}\nスニペット: {r.get('snippet', '')}\nURL: {r.get('url', '')}"
                            for r in web_results
                        ])
                        
                        qwen_summary = await ai_router.send_to_ai(
                            model="qwen",
                            message=f"以下の Web 検索結果を日本語で要約して:\n{web_results_text}",
                            context=[]
                        )
                        
                        # WEB ログ保存
                        web_log_manager.save_web_search(
                            user_id=user_id,
                            keyword=web_search_keyword,
                            web_results=web_results,
                            qwen_summary=qwen_summary,
                        )
                        logger.info(f"[{user_id}] WEB ログ保存完了")
                    except Exception as e:
                        logger.error(f"[{user_id}] Qwen 要約エラー: {e}")
                
            except Exception as e:
                logger.error(f"[{user_id}] WEB検索エラー: {e}")
                web_results = []
    
    # =========================================
    # Step 2: 過去の会話を検索（常時）
    # =========================================
    related_context = conversation_history.search_related(
        user_id=user_id,
        query=request.message,
        top_k=3,
    )
    
    # =========================================
    # Step 3: 過去の WEB ログを検索（常時）
    # =========================================
    related_web_logs = []
    if web_log_manager:
        related_web_logs = web_log_manager.search_web_logs(
            user_id=user_id,
            query=request.message,
            top_k=2,
        )
        logger.info(f"[{user_id}] WEB ログ検索: {len(related_web_logs)}件")
    
    # =========================================
    # Step 4: 統合コンテキスト構築
    # =========================================
    combined_context = [
        *related_context,        # 過去の会話
        *(web_results or []),    # 今回の WEB検索結果（あれば）
        *related_web_logs,       # 過去の WEB ログ
    ]
    
    # =========================================
    # Step 5: Intent分類（Phi3）→ 処理分岐（TaskRouter）
    # =========================================
    if request.force_model:
        # 互換性のため force_model は残す（通常運用は intent -> qwen 固定）
        selected_model = request.force_model
        intent = "chat"
        routed_message = request.message
        logger.info(f"[{user_id}]   → モデル強制指定: {selected_model}")
    else:
        intent = await ai_router.classify_intent(request.message)
        selected_model = "qwen"
        routed_message = ai_router.build_task_prompt(intent, request.message)
        logger.info(f"[{user_id}]   → intent: {intent} / executor: {selected_model}")
    
    # =========================================
    # Step 6: 実行AIにリクエスト送信（通常はQwen）
    # =========================================
    try:
        ai_response = await ai_router.send_to_ai(
            model=selected_model,
            message=routed_message,
            context=combined_context,
        )
    except ConnectionError as e:
        logger.error(f"[{user_id}]   → AI接続エラー: {e}")
        raise HTTPException(
            status_code=503,
            detail=f"AI ({selected_model}) に接続できません。モデルがまだ起動中の可能性があります。"
        )
    except TimeoutError as e:
        logger.error(f"[{user_id}]   → AIタイムアウト: {e}")
        raise HTTPException(
            status_code=504,
            detail=f"AI ({selected_model}) の応答がタイムアウトしました。メッセージが長すぎる可能性があります。"
        )
    except Exception as e:
        logger.error(f"[{user_id}]   → AI通信エラー: {e}")
        raise HTTPException(status_code=503, detail=f"AI ({selected_model}) でエラーが発生しました: {str(e)}")
    
    # =========================================
    # Step 7: 会話を保存
    # =========================================
    conversation_history.save(
        user_id=user_id,
        user_message=request.message,
        ai_response=ai_response,
        model_used=selected_model,
    )
    
    processing_time = round(time.time() - start_time, 3)
    logger.info(f"[{user_id}]   → 応答完了 ({processing_time}秒, {selected_model}, intent={intent})")
    
    return ChatResponse(
        response=ai_response,
        model_used=f"{selected_model}:{intent}",
        processing_time=processing_time,
        context_used=len(related_context) > 0 or len(related_web_logs) > 0,
        web_search_used=web_results is not None and len(web_results) > 0,
        requires_confirmation=False,
        search_in_progress=False,
    )


@app.get("/api/history")
async def get_history(user_id: str = "default_user", limit: int = 20):
    """会話履歴を取得"""
    history = conversation_history.get_recent(user_id=user_id, limit=limit)
    return {"user_id": user_id, "conversations": history, "count": len(history)}


@app.delete("/api/history")
async def clear_history():
    """会話履歴をリセット（ChromaDBのデータを全削除）"""
    try:
        if conversation_history.collection is not None:
            # コレクションを削除して再作成
            conversation_history.client.delete_collection("conversations")
            conversation_history.collection = conversation_history.client.get_or_create_collection(
                name="conversations",
                metadata={"description": "会話履歴のベクトルストア"},
            )
            logger.info("会話履歴をリセットしました")
            return {"status": "ok", "message": "会話履歴をリセットしました"}
        else:
            return {"status": "error", "message": "ChromaDB未接続"}
    except Exception as e:
        logger.error(f"履歴リセット失敗: {e}")
        raise HTTPException(status_code=500, detail=f"リセット失敗: {str(e)}")


# ============================================
# 起動時の初期化
# ============================================
@app.on_event("startup")
async def startup_event():
    global web_log_manager
    
    logger.info("=" * 50)
    logger.info("ローカルAI 制御サーバー起動")
    logger.info(f"  Phi3 URL: {PHI3_URL}")
    logger.info(f"  Qwen URL: {QWEN_URL}")
    
    # Web Log Manager の初期化
    if conversation_history.collection is not None:
        web_log_manager = WebLogManager(conversation_history.client)
        logger.info(f"  Web Log Manager: 初期化完了")
    else:
        logger.warning("  Web Log Manager: ChromaDB 未接続")
    
    # Bing API キー確認
    if web_search_client:
        logger.info(f"  Bing Search API: 設定済み")
    else:
        logger.warning(f"  Bing Search API: キー未設定（Web検索機能は無効）")
    
    logger.info("=" * 50)
