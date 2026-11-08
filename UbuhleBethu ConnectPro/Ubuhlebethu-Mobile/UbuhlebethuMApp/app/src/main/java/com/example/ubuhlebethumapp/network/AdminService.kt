package com.example.ubuhlebethumapp.network

import retrofit2.http.Body
import retrofit2.http.POST

interface AdminService {
    @POST("api/v1/admin/assign-user")
    suspend fun assignUser(@Body request: AssignUserRequest): AssignUserResponse
}

data class AssignUserRequest(val role: String, val email: String)
data class AssignUserResponse(val message: String)
