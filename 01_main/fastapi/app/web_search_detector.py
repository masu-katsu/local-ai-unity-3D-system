# ============================================
# WEB検索キーワード抽出モジュール
# ============================================
# ユーザーメッセージから「検索して」という指示を検出し、
# キーワードを抽出する

import re
import logging
from typing import Optional

logger = logging.getLogger(__name__)

WEB_SEARCH_TRIGGERS = [
    "検索して",
    "調べて",
    "探して",
    "について情報",
    "について知りたい",
    "最新情報",
    "news",
    "search for",
    "find out about",
]


def detect_web_search_request(message: str) -> Optional[str]:
    """
    メッセージから WEB 検索リクエストを検出
    
    例：
      "Webで Python 3.13 について検索して"
      → "Python 3.13"
      
      "通常の回答をしてください"
      → None
    
    Args:
        message: ユーザーメッセージ
    
    Returns:
        検索キーワード or None
    """
    msg_lower = message.lower()
    
    # トリガーワードの検出
    for trigger in WEB_SEARCH_TRIGGERS:
        if trigger in msg_lower:
            # パターン1: トリガーワードの直後の部分
            pattern = re.escape(trigger) + r'\s*「?(.+?)」?[\s]*$'
            match = re.search(pattern, msg_lower)
            if match:
                keyword = match.group(1).strip()
                if keyword:
                    logger.info(f"WEB検索キーワード検出（パターン1）: {keyword}")
                    return keyword
            
            # パターン2: 「」で囲まれた部分
            pattern = r'「(.+?)」'
            match = re.search(pattern, msg_lower)
            if match:
                keyword = match.group(1).strip()
                if keyword:
                    logger.info(f"WEB検索キーワード検出（パターン2）: {keyword}")
                    return keyword
            
            # パターン3: トリガーワードの前にあるキーワード
            parts = msg_lower.split(trigger)
            if len(parts) > 1 and parts[0].strip():
                # 最後の単語/フレーズを抽出
                before_trigger = parts[0].strip()
                words = before_trigger.split()
                if words:
                    keyword = words[-1]
                    logger.info(f"WEB検索キーワード検出（パターン3）: {keyword}")
                    return keyword
    
    return None


def get_confirmation_message(keyword: str) -> str:
    """確認ダイアログのメッセージ作成"""
    return f"""🔍 Web 検索確認

以下の内容で Web 検索を実行しますか？

📌 キーワード: "{keyword}"

⚠️ インターネットに接続します。
   （個人データは送信されません）

【許可】 or 【キャンセル】"""
