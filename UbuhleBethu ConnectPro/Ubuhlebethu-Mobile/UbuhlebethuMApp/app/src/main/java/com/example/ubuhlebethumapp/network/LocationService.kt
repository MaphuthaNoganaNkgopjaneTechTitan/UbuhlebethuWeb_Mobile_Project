package com.example.ubuhlebethumapp.network

import retrofit2.http.GET
import retrofit2.http.Query

interface LocationService {
    @GET("api/v1/location/fallback")
    suspend fun getFallbackLocation(@Query("city") city: String? = null): LocationResponseDto
}

data class LocationResponseDto(
    val latitude: Double,
    val longitude: Double,
    val locationName: String,
    val country: String,
    val source: String
)
