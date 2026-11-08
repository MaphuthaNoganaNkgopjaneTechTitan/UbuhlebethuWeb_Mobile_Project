package com.example.ubuhlebethumapp.worker

import android.content.Context
import android.util.Log
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import com.example.ubuhlebethumapp.db.AppDatabase
import com.example.ubuhlebethumapp.model.ExpenseItem
import com.example.ubuhlebethumapp.network.ApiClient
import com.example.ubuhlebethumapp.network.TokenStore
import kotlinx.coroutines.CancellationException
import okhttp3.MediaType.Companion.toMediaTypeOrNull
import okhttp3.MultipartBody
import okhttp3.RequestBody
import okhttp3.RequestBody.Companion.asRequestBody
import okhttp3.RequestBody.Companion.toRequestBody
import retrofit2.HttpException
import com.example.ubuhlebethumapp.utils.ImageCompressor
import java.io.File

class SyncWorker(
    appContext: Context,
    workerParams: WorkerParameters
) : CoroutineWorker(appContext, workerParams) {

    companion object {
        private const val TAG = "SyncWorker"
    }

    private fun isSameAccount(userId: String): Boolean {
        return !isStopped &&
                TokenStore.getUserId(applicationContext) == userId &&
                !TokenStore.getToken(applicationContext).isNullOrBlank()
    }


    override suspend fun doWork(): Result {

        val scheduledUserId =
            inputData.getString("SYNC_USER_ID")

        val activeUserId =
            TokenStore.getUserId(applicationContext)

        if (
            scheduledUserId.isNullOrBlank() ||
            activeUserId.isNullOrBlank() ||
            scheduledUserId != activeUserId ||
            !isSameAccount(activeUserId)
        ) {
            Log.w(
                TAG,
                "Scheduled account does not match active account. Sync skipped."
            )

            return Result.success()
        }

        val userId = scheduledUserId

        val dao = AppDatabase
            .getDatabase(applicationContext)
            .appDao()

        val textPlain = "text/plain".toMediaTypeOrNull()

        var shouldRetry = false

        // ==========================================
        // 1. SYNC SITE REPORTS
        // ==========================================

        val pendingReports =
            dao.getPendingSiteReportsForUser(userId)

        for (report in pendingReports) {

            if (!isSameAccount(userId)) {
                Log.w(TAG, "Account changed. Sync stopped.")
                return Result.success()
            }

            try {
                val optionalFields =
                    HashMap<String, RequestBody>()

                report.latitude?.let {
                    optionalFields["Latitude"] =
                        it.toString().toRequestBody(textPlain)
                }

                report.longitude?.let {
                    optionalFields["Longitude"] =
                        it.toString().toRequestBody(textPlain)
                }

                var photoPart: MultipartBody.Part? = null

                if (!report.photoPath.isNullOrBlank()) {
                    val file = File(report.photoPath)

                    if (file.exists() && file.length() > 0L) {
                        // Ensure photo is auto-compressed before network upload
                        ImageCompressor.compressImageFile(file)

                        val requestFile = file.asRequestBody(
                            "image/jpeg".toMediaTypeOrNull()
                        )

                        photoPart =
                            MultipartBody.Part.createFormData(
                                "Photo",
                                file.name,
                                requestFile
                            )
                    } else {
                        Log.w(
                            TAG,
                            "Photo missing or empty for report ${report.id}, syncing report without photo attachment"
                        )
                    }
                }

                if (!isSameAccount(userId)) {
                    return Result.success()
                }

                ApiClient.siteReportService(applicationContext)
                    .submitSiteReport(
                        projectId =
                            report.projectId.toRequestBody(textPlain),
                        description =
                            report.description.toRequestBody(textPlain),
                        progressPercent =
                            report.progressPercent.toString()
                                .toRequestBody(textPlain),
                        optionalFields = optionalFields,
                        photo = photoPart,
                        clientReportId = report.id.toRequestBody(textPlain)
                    )

                if (!isSameAccount(userId)) {
                    return Result.success()
                }

                dao.insertSiteReport(
                    report.copy(status = "SYNCED")
                )

                dao.deleteSyncQueueItemByEntityId(report.id)

                // Clean up local temporary picture once safely synced to cloud storage
                if (!report.photoPath.isNullOrBlank()) {
                    try {
                        val tempFile = File(report.photoPath)
                        if (tempFile.exists()) tempFile.delete()
                    } catch (e: Exception) {
                        Log.w(TAG, "Could not delete temporary synced photo file", e)
                    }
                }

                Log.i(TAG, "Site report synchronized.")

            } catch (e: CancellationException) {
                throw e
            } catch (e: Exception) {
                Log.e(TAG, "Site report sync failed", e)
                if (isRetryable(e)) shouldRetry = true
            }
        }

        // ==========================================
        // 2. SYNC EXPENSES
        // ==========================================

        val pendingExpenses =
            dao.getPendingExpensesForUser(userId)

        for (expense in pendingExpenses) {

            if (!isSameAccount(userId)) {
                return Result.success()
            }

            try {
                val item = ExpenseItem(
                    managerName = expense.managerName,
                    itemDescription = expense.itemDescription,
                    amount = expense.amount,
                    status = expense.status,
                    id = expense.id,
                    projectId = expense.projectId
                )

                ApiClient.expenseService(applicationContext)
                    .submitExpense(item)

                if (!isSameAccount(userId)) {
                    return Result.success()
                }

                dao.insertExpense(
                    expense.copy(syncStatus = "SYNCED")
                )

                dao.deleteSyncQueueItemByEntityId(expense.id)

                Log.i(TAG, "Expense synchronized.")

            } catch (e: CancellationException) {
                throw e
            } catch (e: Exception) {
                Log.e(TAG, "Expense sync failed", e)
                if (isRetryable(e)) shouldRetry = true
            }
        }

        // ==========================================
        // 3. SYNC MATERIAL UPDATES
        // ==========================================

        val pendingMaterials =
            dao.getPendingMaterialTasksForUser(userId)

        if (pendingMaterials.isNotEmpty() &&
            isSameAccount(userId)
        ) {
            try {
                val payload = pendingMaterials.map {
                    com.example.ubuhlebethumapp.network
                        .MaterialSyncPayload(
                            id = it.id,
                            projectId = it.projectId,
                            materialName = it.materialName,
                            barcode = it.barcode,
                            quantity = it.quantity,
                            isScanned = it.isScanned
                        )
                }

                val response =
                    ApiClient.materialsService(applicationContext)
                        .syncMaterials(payload)

                if (!isSameAccount(userId)) {
                    return Result.success()
                }

                if (response.success) {
                    for (task in pendingMaterials) {

                        dao.insertMaterialTask(
                            task.copy(status = "SYNCED")
                        )

                        dao.deleteSyncQueueItemByEntityId(task.id)
                    }

                    Log.i(TAG, "Materials synchronized.")
                } else {
                    shouldRetry = true
                }

            } catch (e: CancellationException) {
                throw e
            } catch (e: Exception) {
                Log.e(TAG, "Material sync failed", e)
                if (isRetryable(e)) shouldRetry = true
            }
        }

        // ==========================================
        // 4. ASSIGNED TASKS
        // ==========================================

        // Task synchronization is temporarily disabled.
        //
        // Task creation and task completion require different
        // operations and authorization rules.
        //
        // Do not automatically create assignments simply
        // because isCompleted is false.
        //
        // Pending task records remain in Room until
        // operation-aware synchronization is implemented.

        Log.i(
            TAG,
            "Assigned-task offline sync deferred."
        )

        // ==========================================
        // 5. WORKMANAGER RESULT
        // ==========================================

        if (!isSameAccount(userId)) {
            return Result.success()
        }

        return if (shouldRetry) {
            Log.w(TAG, "Temporary sync failures. Retry requested.")
            Result.retry()
        } else {
            Log.i(TAG, "Eligible sync operations processed.")
            Result.success()
        }
    }

    private fun isRetryable(e: Exception): Boolean {
        return when (e) {
            is java.io.IOException -> true

            is HttpException -> {
                e.code() == 408 ||
                        e.code() == 429 ||
                        e.code() >= 500
            }

            else -> false
        }
    }
}
