package com.example.ubuhlebethumapp.network

import retrofit2.http.GET

interface UserService {
    @GET("api/v1/users/workers")
    suspend fun getWorkers(): List<UserDto>

    @GET("api/v1/users")
    suspend fun getUsers(): List<UserDto>
}

data class UserDto(
    val id: String = "",
    val userName: String = "",
    val email: String = "",
    val roles: List<String> = emptyList(),
    val isLockedOut: Boolean = false
)