package com.example.ubuhlebethumapp.model

data class AssignedTaskDto(
    val id: String,
    val projectId: String,
    val taskTitle: String,
    val assignedWorker: String,
    val isCompleted: Boolean = false
)
