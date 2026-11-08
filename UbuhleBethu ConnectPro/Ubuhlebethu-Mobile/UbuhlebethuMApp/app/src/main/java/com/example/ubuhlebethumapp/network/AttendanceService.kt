package com.example.ubuhlebethumapp.network

import com.example.ubuhlebethumapp.model.AttendanceRecord
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.PUT
import retrofit2.http.Path
import retrofit2.http.Query

/** Not yet implemented on the backend — see the "next backend tasks" list. */
interface AttendanceService {

    @POST("api/v1/attendance/check-in")
    suspend fun checkIn(@Body body: CheckInRequest): Response<AttendanceRecord>

    @POST("api/v1/attendance/check-out")
    suspend fun checkOut(@Body body: CheckOutRequest): Response<AttendanceRecord>

    @GET("api/v1/attendance")
    suspend fun listForProject(
        @Query("projectId") projectId: String,
        @Query("date") date: String
    ): Response<List<AttendanceRecord>>

    @PUT("api/v1/attendance/{id}/approval")
    suspend fun approve(
        @Path("id") id: Int,
        @Body body: ApprovalRequest
    ): Response<Unit>
}

data class CheckInRequest(val projectId: String, val latitude: Double?, val longitude: Double?)
data class CheckOutRequest(val attendanceId: Int)
data class ApprovalRequest(val approved: Boolean)

