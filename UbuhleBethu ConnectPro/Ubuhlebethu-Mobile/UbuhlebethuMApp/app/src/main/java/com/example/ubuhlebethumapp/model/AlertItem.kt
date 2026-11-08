package com.example.ubuhlebethumapp.model

data class AlertItem(
    val id: String,
    val title: String,          // e.g., "Task Overdue"
    val description: String,    // e.g., "Site Preparation – Phase 1\nOverdue by 3 days"
    val timestamp: String,      // e.g., "Today, 09:24"
    val priority: String,       // "High" or "Medium"
    val category: String        // "All", "Overdue Tasks", "Risks", "Behind Schedule"
)