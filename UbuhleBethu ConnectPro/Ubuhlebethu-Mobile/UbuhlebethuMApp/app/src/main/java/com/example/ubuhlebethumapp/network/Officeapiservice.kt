package com.example.ubuhlebethumapp.network

import com.example.ubuhlebethumapp.model.QuoteDto
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.Path

interface OfficeApiService {

    @GET("api/office/pending-quotes")
    suspend fun getPendingQuotes(): List<QuoteDto>

    @POST("api/office/generate-draft/{id}")
    suspend fun generateDraftEstimate(@Path("id") id: String): QuoteDto

    @POST("api/office/approve/{id}")
    suspend fun approveQuote(@Path("id") id: String): QuoteDto
}