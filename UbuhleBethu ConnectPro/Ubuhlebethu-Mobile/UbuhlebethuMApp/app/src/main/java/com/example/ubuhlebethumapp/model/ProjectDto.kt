package com.example.ubuhlebethumapp.model

// Matches the anonymous object returned by GET /api/v1/projects
data class ProjectDto(
    val id: String,
    val title: String,
    val location: String?,
    val progressPercent: Int,
    val status: String,
    val latitude: Double? = null,
    val longitude: Double? = null,
    val geofenceRadiusMeters: Double? = 300.0
)
