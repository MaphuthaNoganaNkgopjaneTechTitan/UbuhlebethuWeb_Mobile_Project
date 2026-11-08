package com.example.ubuhlebethumapp.network

import com.example.ubuhlebethumapp.model.ProjectDto
import com.example.ubuhlebethumapp.model.SiteReportCreateResponse
import com.example.ubuhlebethumapp.model.SiteReportDto
import okhttp3.MultipartBody
import okhttp3.RequestBody
import retrofit2.http.GET
import retrofit2.http.Multipart
import retrofit2.http.POST
import retrofit2.http.Part
import retrofit2.http.PartMap

interface SiteReportService {

    @GET("api/v1/projects")
    suspend fun getActiveProjects(): List<ProjectDto>

    @GET("api/v1/sitereports/mine")
    suspend fun getMyReports(): List<SiteReportDto>

    /**
     * Required fields go in as individual @Part params (they can never be null).
     * Optional fields go into @PartMap so we can omit them from the body entirely
     * when the caller has nothing to send.
     */
    @Multipart
    @POST("api/v1/sitereports")
    suspend fun submitSiteReport(
        @Part("ProjectId") projectId: RequestBody,
        @Part("Description") description: RequestBody,
        @Part("ProgressPercent") progressPercent: RequestBody,
        @PartMap optionalFields: HashMap<String, RequestBody>,
        @Part photo: MultipartBody.Part?,
        @Part("ClientReportId") clientReportId: RequestBody? = null
    ): SiteReportCreateResponse
}