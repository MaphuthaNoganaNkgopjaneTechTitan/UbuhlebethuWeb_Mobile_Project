package com.example.ubuhlebethumapp.network

import com.example.ubuhlebethumapp.db.MaterialTask
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.Query

interface MaterialsService {

    @GET("api/v1/materials")
    suspend fun getMaterials(@Query("projectId") projectId: String? = null): List<MaterialLookupResponse>

    @GET("api/v1/materials/lookup")
    suspend fun lookupMaterial(@Query("barcode") barcode: String): MaterialLookupResponse

    @POST("api/v1/materials/register")
    suspend fun registerMaterial(@Body request: RegisterMaterialRequest): MaterialLookupResponse

    @POST("api/v1/materials/sync")
    suspend fun syncMaterials(@Body items: List<MaterialSyncPayload>): MaterialSyncResponse
}

data class MaterialLookupResponse(
    val id: String = "",
    val barcode: String = "",
    val name: String = "",
    val unitCost: Double = 0.0,
    val unit: String = "Units",
    val projectId: String? = null,
    val registeredBy: String? = null
)

data class RegisterMaterialRequest(
    val barcode: String,
    val name: String,
    val unitCost: Double,
    val unit: String = "Units",
    val projectId: String? = null
)

data class MaterialSyncPayload(
    val id: String,
    val projectId: String,
    val materialName: String,
    val barcode: String,
    val quantity: Int,
    val isScanned: Boolean
)

data class MaterialSyncResponse(
    val success: Boolean,
    val count: Int,
    val message: String? = null
)

fun MaterialTask.toSyncPayload(): MaterialSyncPayload =
    MaterialSyncPayload(
        id = this.id,
        projectId = this.projectId,
        materialName = this.materialName,
        barcode = this.barcode,
        quantity = this.quantity,
        isScanned = this.isScanned
    )
