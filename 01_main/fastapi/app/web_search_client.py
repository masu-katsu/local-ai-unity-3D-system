# ============================================
# Bing Search API クライアント
# ============================================
# Web 検索をユーザーの停止指示に応じて処理

import httpx
import logging
from typing import Callable, Optional
import asyncio

logger = logging.getLogger(__name__)


class BingSearchClient:
    """Bing Search API クライアント"""
    
    def __init__(self, api_key: str):
        """
        Args:
            api_key: Bing Search API キー
        """
        self.api_key = api_key
        self.base_url = "https://api.bing.microsoft.com/v7.0/search"
    
    async def search_incremental(
        self,
        query: str,
        on_result: Optional[Callable] = None,
        top_k: int = 5,
    ) -> list:
        """
        インクリメンタル検索
        結果を得るたびに on_result() コールバックを呼ぶ
        （UI 側でリアルタイム表示可能）
        
        Args:
            query: 検索クエリ
            on_result: 結果取得時のコールバック
            top_k: 取得する結果数
        
        Returns:
            検索結果リスト
        """
        headers = {
            "Ocp-Apim-Subscription-Key": self.api_key,
            "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"
        }
        
        params = {
            "q": query,
            "count": top_k,
            "mkt": "ja-JP",
        }
        
        results = []
        
        try:
            logger.info(f"Web検索開始: '{query}'")
            async with httpx.AsyncClient(timeout=10.0) as client:
                response = await client.get(
                    self.base_url,
                    headers=headers,
                    params=params,
                )
                response.raise_for_status()
                
                data = response.json()
                logger.info(f"Bing API レスポンス取得")
                
                # 検索結果をインクリメンタルに処理
                web_pages = data.get("webPages", {}).get("value", [])
                logger.info(f"検索結果: {len(web_pages)}件")
                
                for idx, item in enumerate(web_pages[:top_k]):
                    result = {
                        "title": item.get("name", ""),
                        "url": item.get("url", ""),
                        "snippet": item.get("snippet", ""),
                    }
                    results.append(result)
                    
                    # コールバック（UI リアルタイム表示）
                    if on_result:
                        try:
                            on_result(result)
                        except Exception as e:
                            logger.error(f"コールバックエラー: {e}")
                    
                    # UI の「停止」指令を確認する間隔
                    await asyncio.sleep(0.1)
                
                logger.info(f"Web検索完了: {len(results)}件")
                return results
                
        except asyncio.CancelledError:
            logger.info(f"Web検索キャンセル（{len(results)}件取得済み）")
            return results
        except httpx.HTTPStatusError as e:
            if e.response.status_code == 401:
                logger.error("Bing API キーが無効です")
            elif e.response.status_code == 403:
                logger.error("Bing API 呼び出し制限に達しました")
            else:
                logger.error(f"Bing API エラー: {e.response.status_code}")
            return results
        except Exception as e:
            logger.error(f"Web検索エラー: {e}")
            return results
    
    async def search(self, query: str, top_k: int = 5) -> list:
        """シンプル検索"""
        return await self.search_incremental(query, on_result=None, top_k=top_k)
