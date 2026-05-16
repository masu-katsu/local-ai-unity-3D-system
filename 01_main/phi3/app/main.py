# ============================================
# Phi3 会話AI サーバー（CPU動作）
# ============================================
# 雑談・簡単な質問応答を担当
# llama-cpp-python で GGUF モデルを CPU 上で実行
# ============================================

import os
import re
import logging
from fastapi import FastAPI
from pydantic import BaseModel, Field
from typing import Optional
from llama_cpp import Llama


def clean_response(text: str) -> str:
    """
    Aggressively remove all instruction patterns and meta text
    """
    # First pass: remove entire lines with instruction keywords
    lines = text.split('\n')
    cleaned_lines = []
    for line in lines:
        # Skip lines containing instruction/step keywords
        if any(kw in line for kw in ['指示', 'instruction', 'step', 'ステップ', 'Instruction', 'Step', '発語詰、']):
            continue
        # Skip lines with only brackets/metadata
        if line.strip() and all(c in '[]{}()\[\]{}()\n' for c in line.strip()):
            continue
        cleaned_lines.append(line)
    
    text = '\n'.join(cleaned_lines)
    
    # Second pass: regex patterns for remaining cleanup
    meta_patterns = [
        r"\n\n+(?:この|その|上記の|以下の|以上の|下記の)[^\n]*$",
        r"\n\n+(?:Answer|Response|Note|注[:：]).*$",
        r"\n\n+-{3,}.*$",
        r"^\s*\[.*?\]\s*$",
        r"^\s*\{.*?\}\s*$",
        r"^\s*\(.*?\)\s*$",
    ]
    for pattern in meta_patterns:
        text = re.sub(pattern, "", text, flags=re.MULTILINE | re.DOTALL)
    
    # Remove excessive newlines
    text = re.sub(r'\n{3,}', '\n\n', text)
    
    return text.strip()

# ============================================
# ログ設定
# ============================================
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [Phi3] %(message)s",
)
logger = logging.getLogger(__name__)

# ============================================
# 環境変数・設定
# ============================================
MODEL_PATH = os.getenv("MODEL_PATH", "/models/phi3-mini-4k-instruct.Q4_K_M.gguf")
MAX_TOKENS = int(os.getenv("PHI3_MAX_TOKENS", "512"))
N_CTX = 4096          # コンテキストウィンドウサイズ
N_THREADS = 4         # CPUスレッド数

# ============================================
# FastAPI 初期化
# ============================================
app = FastAPI(title="Phi3 会話AI", version="1.0.0")

# ============================================
# モデル読み込み（起動時に1回だけ）
# ============================================
llm: Optional[Llama] = None


@app.on_event("startup")
async def load_model():
    global llm
    logger.info("=" * 40)
    logger.info("Phi3 モデル読み込み開始...")
    logger.info(f"  モデルパス: {MODEL_PATH}")
    logger.info(f"  最大トークン: {MAX_TOKENS}")
    logger.info(f"  CPUスレッド数: {N_THREADS}")

    try:
        llm = Llama(
            model_path=MODEL_PATH,
            n_ctx=N_CTX,
            n_threads=N_THREADS,
            verbose=False,
        )
        logger.info("Phi3 モデル読み込み完了 ✓")
    except Exception as e:
        logger.error(f"モデル読み込み失敗: {e}")
        logger.error("モデルファイルが存在するか確認してください")
        llm = None


# ============================================
# リクエスト・レスポンスモデル
# ============================================
class GenerateRequest(BaseModel):
    message: str = Field(..., description="ユーザーのメッセージ")
    context: list[dict] = Field(default=[], description="過去の関連会話")


class GenerateResponse(BaseModel):
    response: str = Field(..., description="AIの応答")
    tokens_used: int = Field(default=0, description="使用トークン数")


# ============================================
# プロンプト構築
# ============================================
def build_prompt(message: str, context: list[dict]) -> str:
    """
    Phi3 用のプロンプトを構築する

    過去の関連会話を「会話ターン」として埋め込む
    → モデルが自然に文脈を理解し、そのまま出力しなくなる
    """
    system_prompt = (
        "あなたはフレンドリーなAIアシスタントです。\n"
        "ルール:\n"
        "- 日本語で返答すること\n"
        "- 敬語は「です・ます」調を使う。過度に丁寧な表現は禁止\n"
        "- 「お客様」「申し上げます」「おかれましては」などの堅い表現は使わない\n"
        "- 友達に話すような自然な日本語で答える\n"
        "- 回答は簡潔に、要点だけ伝える\n"
    )

    # Phi3 のチャットテンプレート（system）
    prompt = f"<|system|>\n{system_prompt}<|end|>\n"

    # 過去の関連会話を「会話ターン」として追加
    # → systemプロンプトではなく会話履歴として渡すことで
    #   モデルが自然に文脈を理解する
    if context:
        for conv in context:
            user_msg = conv.get("user_message", "")
            ai_resp = conv.get("ai_response", "")[:150]
            prompt += f"<|user|>\n{user_msg}<|end|>\n"
            prompt += f"<|assistant|>\n{ai_resp}<|end|>\n"

    # 現在のユーザーメッセージ
    prompt += f"<|user|>\n{message}<|end|>\n"
    prompt += "<|assistant|>\n"

    return prompt


# ============================================
# エンドポイント
# ============================================
@app.get("/health")
async def health():
    """ヘルスチェック"""
    return {
        "status": "ok" if llm is not None else "model_not_loaded",
        "model": "phi3-mini",
        "device": "cpu",
    }


@app.post("/generate", response_model=GenerateResponse)
async def generate(request: GenerateRequest):
    """テキスト生成"""
    if llm is None:
        return GenerateResponse(
            response="モデルがまだ読み込まれていません。しばらくお待ちください。",
            tokens_used=0,
        )

    logger.info(f"生成リクエスト: {request.message[:50]}...")

    # プロンプト構築
    prompt = build_prompt(request.message, request.context)

    # 生成実行
    try:
        output = llm(
            prompt,
            max_tokens=MAX_TOKENS,
            temperature=0.7,
            top_p=0.9,
            stop=["<|end|>", "<|user|>"],
            echo=False,
        )

        raw_text = output["choices"][0]["text"].strip()
        response_text = clean_response(raw_text)
        tokens_used = output.get("usage", {}).get("total_tokens", 0)

        if raw_text != response_text:
            logger.info(f"メタ解説を除去: {len(raw_text)} → {len(response_text)}文字")
        logger.info(f"生成完了: {tokens_used}トークン使用")

        return GenerateResponse(
            response=response_text,
            tokens_used=tokens_used,
        )

    except Exception as e:
        logger.error(f"生成エラー: {e}")
        return GenerateResponse(
            response="申し訳ありません、応答の生成中にエラーが発生しました。",
            tokens_used=0,
        )
