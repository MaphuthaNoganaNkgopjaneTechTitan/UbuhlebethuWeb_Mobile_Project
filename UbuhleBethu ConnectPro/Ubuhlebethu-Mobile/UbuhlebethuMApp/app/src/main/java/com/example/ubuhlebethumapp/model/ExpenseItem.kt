package com.example.ubuhlebethumapp.model

import java.util.UUID

data class ExpenseItem(
    val managerName: String? = "Site Manager",
    val itemDescription: String? = "",
    val amount: Double = 0.0,
    var status: String? = "PENDING",
    var id: String? = UUID.randomUUID().toString(),
    val projectId: String? = ""
) {
    fun getProjectIdAsGuid(): String = projectId ?: ""
}
