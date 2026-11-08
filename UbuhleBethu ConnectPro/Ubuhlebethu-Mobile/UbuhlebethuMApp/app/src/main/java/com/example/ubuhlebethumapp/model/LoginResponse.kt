package com.example.ubuhlebethumapp.model

data class LoginResponse(
    val token: String,
    val refreshToken: String? = null,
    val expiresAt: String? = null,
    val role: String? = null,
    val roles: List<String> = emptyList(),
    val displayName: String? = null,
    val userName: String? = null,
    val requiresPasswordReset: Boolean = false
)