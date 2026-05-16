# ============================================
# AI ルーター（Intent分類 + 処理分岐）
# ============================================
# 1) Phi3 で intent を分類
# 2) TaskRouter ロジックで処理方針を決定
# 3) Qwen を実行AIとして利用
# ============================================

import logging
import re
from typing import Optional

import httpx

logger = logging.getLogger(__name__)

INTENTS = {"chat", "code", "fix", "idea"}


class AIRouter:
    def __init__(self, phi3_url: str, qwen_url: str):
        self.phi3_url = phi3_url
        self.qwen_url = qwen_url
        self.client = httpx.AsyncClient(timeout=120.0)
        logger.info("AIRouter 初期化完了（Intent分類: Phi3 / 実行: Qwen）")

    def _fallback_intent(self, message: str) -> str:
        msg = message.lower()
        if any(k in msg for k in ["バグ", "修正", "直して", "エラー", "fix", "bug"]):
            return "fix"
        if any(k in msg for k in ["コード", "実装", "関数", "class", "code", "implement"]):
            return "code"
        if any(k in msg for k in ["案", "アイデア", "企画", "brainstorm", "idea"]):
            return "idea"
        return "chat"

    def _extract_intent(self, raw: str) -> Optional[str]:
        if not raw:
            return None
        m = re.search(r"(chat|code|fix|idea)", raw.lower())
        if not m:
            return None
        intent = m.group(1)
        return intent if intent in INTENTS else None

    async def classify_intent(self, message: str) -> str:
        """
        Phi3 で intent を分類する。
        返却は chat/code/fix/idea のいずれか。
        """
        classify_prompt = (
            "あなたは意図分類器です。以下のユーザー入力を、"
            "chat / code / fix / idea のどれか1語だけで返してください。\n"
            "説明文は不要です。\n"
            f"入力: {message}"
        )
        try:
            raw = await self.send_to_ai("phi3", classify_prompt, context=[])
            intent = self._extract_intent(raw)
            if intent:
                logger.info(f"  IntentClassifier(Phi3): {intent}")
                return intent
            fallback = self._fallback_intent(message)
            logger.warning(f"  Intent解析失敗（raw='{raw[:40]}...'）→ fallback: {fallback}")
            return fallback
        except Exception as e:
            fallback = self._fallback_intent(message)
            logger.warning(f"  Intent分類失敗: {e} → fallback: {fallback}")
            return fallback

    def build_task_prompt(self, intent: str, message: str) -> str:
        """
        TaskRouter: intent から Qwen 向け実行指示を組み立てる。
        """
        intent = intent if intent in INTENTS else "chat"
        if intent == "code":
            task_header = (
                "[MODE: CODE]\n"
                "あなたは実装アシスタントです。"
                "動くコード例・手順・注意点を優先して回答してください。"
            )
        elif intent == "fix":
            task_header = (
                "[MODE: FIX]\n"
                "あなたはデバッグ支援アシスタントです。"
                "原因候補、再現手順、修正案、確認方法を順に回答してください。"
            )
        elif intent == "idea":
            task_header = (
                "[MODE: IDEA]\n"
                "あなたはアイデア発想アシスタントです。"
                "複数案と各案のメリット・デメリットを簡潔に示してください。"
            )
        else:
            task_header = (
                "[MODE: CHAT]\n"
                "あなたは自然な会話アシスタントです。"
                "日本語で分かりやすく回答してください。"
            )
        return f"{task_header}\n\nユーザー入力:\n{message}"

    async def send_to_ai(self, model: str, message: str, context: list[dict]) -> str:
        """指定モデルにリクエスト送信する共通メソッド。"""
        url = self._get_url(model)
        payload = {
            "message": message,
            "context": context,
        }
        try:
            response = await self.client.post(
                f"{url}/generate",
                json=payload,
            )
            response.raise_for_status()
            data = response.json()
            return data.get("response", "応答を取得できませんでした")
        except httpx.ConnectError:
            logger.error(f"{model} に接続できません: {url}")
            raise ConnectionError(f"{model} サービスに接続できません")
        except httpx.TimeoutException:
            logger.error(f"{model} がタイムアウトしました")
            raise TimeoutError(f"{model} の応答がタイムアウトしました")
        except Exception as e:
            logger.error(f"{model} 通信エラー: {e}")
            raise

    async def check_health(self, model: str) -> str:
        """AIサービスのヘルスチェック"""
        url = self._get_url(model)
        try:
            response = await self.client.get(f"{url}/health", timeout=5.0)
            if response.status_code == 200:
                return "ok"
            return f"error (status: {response.status_code})"
        except Exception:
            return "offline"

    def _get_url(self, model: str) -> str:
        if model == "phi3":
            return self.phi3_url
        if model == "qwen":
            return self.qwen_url
        raise ValueError(f"不明なモデル: {model}")
