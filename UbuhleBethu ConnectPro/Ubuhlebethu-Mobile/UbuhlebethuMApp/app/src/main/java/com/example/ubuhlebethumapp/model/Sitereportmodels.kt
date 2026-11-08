package com.example.ubuhlebethumapp.model

// Matches the anonymous object returned by GET /api/v1/sitereports/mine
data class SiteReportDto(
    val id: String,
    val projectId: String,
    val description: String,
    val progressPercent: Int,
    val constructionStage: String?,
    val status: String,
    val loggedAt: String,
    val hasPhoto: Boolean
)

// Matches the anonymous object returned by POST /api/v1/sitereports
data class SiteReportCreateResponse(
    val id: String,
    val constructionStage: String?,
    val status: String
)
