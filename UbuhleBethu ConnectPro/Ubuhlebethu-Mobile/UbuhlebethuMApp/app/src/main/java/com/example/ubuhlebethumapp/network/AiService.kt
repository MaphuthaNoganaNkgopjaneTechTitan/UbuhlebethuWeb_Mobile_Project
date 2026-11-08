package com.example.ubuhlebethumapp.network

import retrofit2.http.Body
import retrofit2.http.POST

interface AiService {
    @POST("api/v1/chat/ask")
    suspend fun askChatbot(@Body request: ChatAskRequest): ChatAskResponse

    /** Multi-turn conversation endpoint with tool/function calling support. */
    @POST("api/v1/chat/conversation")
    suspend fun conversation(@Body request: ConversationRequest): ConversationResponse

    @POST("api/v1/ai/estimate-materials")
    suspend fun estimateMaterials(@Body request: MaterialEstimateRequest): AiResponse

    @POST("api/v1/ai/suggest-quote")
    suspend fun suggestQuote(@Body request: QuoteSuggestionRequest): AiResponse
}

// ── Simple ask (backward-compatible) ─────────────────────────────────────────
data class ChatAskRequest(val Question: String)
data class ChatAskResponse(val answer: String?, val suggestion: String? = null)

// ── Multi-turn conversation ───────────────────────────────────────────────────
data class ConversationMessage(
    val role: String,       // "user" | "assistant" | "system"
    val content: String
)

data class ConversationRequest(
    val messages: List<ConversationMessage>,
    val projectId: String? = null,
    val userRole: String? = null,
    val provider: String? = null
)

data class ConversationResponse(
    val answer: String,
    val usedFallback: Boolean = false,
    val source: String = "",
    val toolsUsed: List<String> = emptyList()
)

// ── Estimation endpoints ──────────────────────────────────────────────────────
data class MaterialEstimateRequest(val materials: String)
data class QuoteSuggestionRequest(val context: String)
data class AiResponse(val suggestion: String)
