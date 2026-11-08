package com.example.ubuhlebethumapp.model

data class AttendanceRecord(
    val workerName: String? = "Worker",
    val date: String? = "",
    var isPresent: Boolean = false,
    val dailyRate: Double = 500.0,
    var checkInTime: String? = "Not Started",
    var checkOutTime: String? = "Not Finished",
    val id: Int = 0,
    val isOffSite: Boolean = false,
    val distanceFromSiteMeters: Double? = null
)
