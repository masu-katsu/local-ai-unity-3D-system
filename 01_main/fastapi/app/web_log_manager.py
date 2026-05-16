# ============================================
# WEB ログ管理モジュール
# ============================================
# Web 検索結果を ChromaDB に保存し、
# 過去の検索結果から関連情報を検索

import os
import json
import logging
from datetime import datetime
from typing import Optional
import chromadb

logger = logging.getLogger(__name__)


class WebLogManager:
    def __init__(self, chroma_client):
        """ChromaDB に web_logs コレクションを作成"""
        try:
            self.collection = chroma_client.get_or_create_collection(
                name="web_logs",
                metadata={"description": "Web検索結果ログ"}
            )
            count = self.collection.count()
            logger.info(f"WebLogManager 初期化完了 - 既存ログ数: {count}")
        except Exception as e:
            logger.error(f"WebLogManager 初期化失敗: {e}")
            self.collection = None
    
    def save_web_search(
        self,
        user_id: str,
        keyword: str,
        web_results: list,
        qwen_summary: str,
    ) -> None:
        """
        Web 検索結果をログに保存
        
        Args:
            user_id: ユーザーID
            keyword: 検索キーワード
            web_results: Web API から取得した結果リスト
            qwen_summary: Qwen による要約
        """
        if self.collection is None:
            return
        
        timestamp = datetime.now().isoformat()
        doc_id = f"{user_id}_web_{int(datetime.now().timestamp() * 1000)}"
        
        try:
            # ChromaDB に保存（検索対象：キーワード + 要約）
            search_document = f"キーワード: {keyword}\n要約: {qwen_summary}"
            
            self.collection.add(
                documents=[search_document],
                metadatas=[{
                    "user_id": user_id,
                    "keyword": keyword,
                    "web_results": json.dumps(web_results, ensure_ascii=False),
                    "qwen_summary": qwen_summary,
                    "timestamp": timestamp,
                    "source": "web_search",
                    "result_count": len(web_results),
                }],
                ids=[doc_id],
            )
            logger.info(f"WEB ログ保存完了: {doc_id} ({len(web_results)}件)")
            
            # ファイルにもバックアップ保存
            self._save_to_file(user_id, keyword, web_results, qwen_summary, timestamp)
            
        except Exception as e:
            logger.error(f"WEB ログ保存失敗: {e}")
    
    def search_web_logs(
        self,
        user_id: str,
        query: str,
        top_k: int = 2,
        max_distance: float = 1.5,
    ) -> list:
        """
        WEB ログから関連情報を検索
        """
        if self.collection is None or self.collection.count() == 0:
            return []
        
        try:
            results = self.collection.query(
                query_texts=[query],
                n_results=min(top_k, self.collection.count()),
                where={"user_id": user_id},
                include=["metadatas", "distances"],
            )
            
            web_logs = []
            if results and results["metadatas"]:
                distances = results.get("distances", [[]])[0]
                for i, metadata in enumerate(results["metadatas"][0]):
                    dist = distances[i] if i < len(distances) else 999
                    if dist <= max_distance:
                        web_logs.append({
                            "keyword": metadata.get("keyword", ""),
                            "summary": metadata.get("qwen_summary", ""),
                            "timestamp": metadata.get("timestamp", ""),
                        })
                        logger.info(f"  関連WEBログ [{i+1}] 距離={dist:.3f}: {metadata.get('keyword', '')}")
                    else:
                        logger.info(f"  除外（距離超過）[{i+1}] 距離={dist:.3f}")
            
            logger.info(f"WEB ログ検索: {len(web_logs)}件採用")
            return web_logs
            
        except Exception as e:
            logger.error(f"WEB ログ検索失敗: {e}")
            return []
    
    def get_recent_web_logs(self, user_id: str, limit: int = 10) -> list:
        """直近の WEB ログを取得"""
        if self.collection is None or self.collection.count() == 0:
            return []
        
        try:
            results = self.collection.get(
                where={"user_id": user_id},
                limit=limit,
            )
            
            logs = []
            if results and results["metadatas"]:
                for metadata in results["metadatas"]:
                    logs.append({
                        "keyword": metadata.get("keyword", ""),
                        "summary": metadata.get("qwen_summary", ""),
                        "timestamp": metadata.get("timestamp", ""),
                    })
                
                # タイムスタンプでソート（新しい順）
                logs.sort(key=lambda x: x["timestamp"], reverse=True)
            
            return logs
            
        except Exception as e:
            logger.error(f"WEB ログ取得失敗: {e}")
            return []
    
    def _save_to_file(
        self,
        user_id: str,
        keyword: str,
        web_results: list,
        qwen_summary: str,
        timestamp: str,
    ) -> None:
        """WEB ログをファイルに保存（バックアップ）"""
        log_dir = "/logs/web_search"
        os.makedirs(log_dir, exist_ok=True)
        
        date_str = datetime.now().strftime("%Y-%m-%d")
        filepath = os.path.join(log_dir, f"web_logs_{user_id}_{date_str}.jsonl")
        
        entry = {
            "timestamp": timestamp,
            "keyword": keyword,
            "web_results": web_results,
            "qwen_summary": qwen_summary,
        }
        
        try:
            with open(filepath, "a", encoding="utf-8") as f:
                f.write(json.dumps(entry, ensure_ascii=False) + "\n")
        except Exception as e:
            logger.error(f"ファイル保存失敗: {e}")
