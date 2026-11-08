package com.example.ubuhlebethumapp.network

import com.example.ubuhlebethumapp.model.ExpenseItem
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.Query

interface ExpenseService {
    @GET("api/v1/expenses")
    suspend fun getExpenses(@Query("projectId") projectId: String? = null): List<ExpenseItem>

    @POST("api/v1/expenses")
    suspend fun submitExpense(@Body expense: ExpenseItem): ExpenseItem
}
