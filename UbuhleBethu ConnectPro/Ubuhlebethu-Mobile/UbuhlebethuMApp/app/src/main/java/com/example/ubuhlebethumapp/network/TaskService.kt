package com.example.ubuhlebethumapp.network

import com.example.ubuhlebethumapp.model.AssignedTaskDto
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.PUT
import retrofit2.http.Path
import retrofit2.http.Query

interface TaskService {
    @GET("api/v1/tasks")
    suspend fun getTasks(@Query("projectId") projectId: String? = null): List<AssignedTaskDto>

    @POST("api/v1/tasks")
    suspend fun assignTask(@Body task: AssignedTaskDto): AssignedTaskDto

    @PUT("api/v1/tasks/{id}/complete")
    suspend fun completeTask(@Path("id") id: String): AssignedTaskDto
}
