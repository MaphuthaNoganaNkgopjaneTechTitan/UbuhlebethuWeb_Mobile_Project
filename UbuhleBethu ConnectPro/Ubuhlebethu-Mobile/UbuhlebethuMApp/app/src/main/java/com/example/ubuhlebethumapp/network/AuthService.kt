package com.example.ubuhlebethumapp.network

import com.example.ubuhlebethumapp.model.LoginRequest
import com.example.ubuhlebethumapp.model.LoginResponse
import retrofit2.Call
import retrofit2.http.Body
import retrofit2.http.POST

interface AuthService {
    @POST("api/v1/auth/login")
    suspend fun login(@Body request: LoginRequest): LoginResponse

    @POST("api/v1/auth/refresh")
    suspend fun refresh(@Body request: RefreshRequest): RefreshResponse

    @POST("api/v1/auth/refresh")
    fun refreshSync(@Body request: RefreshRequest): Call<RefreshResponse>

    @POST("api/v1/auth/change-password")
    suspend fun changePassword(@Body request: ChangePasswordRequest): ChangePasswordResponse
}

data class RefreshRequest(
    val token: String,
    val refreshToken: String
)

data class RefreshResponse(
    val token: String,
    val refreshToken: String
)

data class ChangePasswordRequest(
    val currentPassword: String,
    val newPassword: String
)

data class ChangePasswordResponse(
    val message: String? = null,
    val error: String? = null
)