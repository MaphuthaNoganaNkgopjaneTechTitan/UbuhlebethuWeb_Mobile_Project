package com.example.ubuhlebethumapp.network

import retrofit2.http.GET
import retrofit2.http.Path

interface BudgetService {
    @GET("api/v1/projects/{projectId}/budget")
    suspend fun getProjectBudget(@Path("projectId") projectId: String): BudgetSummary
}

data class BudgetSummary(
    val projectId: String,
    val projectTitle: String,
    val estimatedBudget: Double,
    val totalApprovedExpenses: Double,
    val budgetRemaining: Double,
    val variancePercent: Double,
    val isOverBudget: Boolean
)
