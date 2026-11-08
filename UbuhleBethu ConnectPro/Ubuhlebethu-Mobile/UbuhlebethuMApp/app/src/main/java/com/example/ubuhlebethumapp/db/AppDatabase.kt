package com.example.ubuhlebethumapp.db

import android.content.Context
import androidx.room.*
import androidx.room.migration.Migration
import androidx.sqlite.db.SupportSQLiteDatabase

@Entity(tableName = "material_task")
data class MaterialTask(
    @PrimaryKey val id: String,
    val projectId: String,
    val materialName: String,
    val barcode: String,
    val quantity: Int,
    val isScanned: Boolean = false,
    val status: String = "PENDING_SYNC", // PENDING_SYNC, SYNCED
    val ownerUserId: String = ""
)

@Entity(tableName = "site_report")
data class SiteReportEntity(
    @PrimaryKey val id: String,
    val projectId: String,
    val description: String,
    val progressPercent: Int,
    val photoPath: String? = null,
    val latitude: Double? = null,
    val longitude: Double? = null,
    val status: String = "PENDING_SYNC", // PENDING_SYNC, SYNCED
    val ownerUserId: String = ""
)

@Entity(tableName = "sync_queue")
data class SyncQueueEntity(
    @PrimaryKey(autoGenerate = true) val id: Int = 0,
    val entityType: String, // "SITE_REPORT", "MATERIAL_TASK", "EXPENSE"
    val entityId: String,
    val operation: String, // "POST", "PUT"
    val ownerUserId: String = ""
)

@Entity(tableName = "expense")
data class ExpenseEntity(
    @PrimaryKey val id: String,
    val managerName: String,
    val itemDescription: String,
    val amount: Double,
    var status: String,
    val projectId: String = "",
    val syncStatus: String = "PENDING_SYNC",
    val ownerUserId: String = ""
)

@Entity(tableName = "assigned_task")
data class AssignedTaskEntity(
    @PrimaryKey val id: String,
    val projectId: String,
    val taskTitle: String,
    val assignedWorker: String,
    var isCompleted: Boolean = false,
    val photoPath: String? = null,
    val latitude: Double? = null,
    val longitude: Double? = null,
    val syncStatus: String = "PENDING_SYNC",
    val ownerUserId: String = ""
)

@Dao
interface AppDao {
    // MaterialTask
    @Query("SELECT * FROM material_task")
    suspend fun getAllMaterialTasks(): List<MaterialTask>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertMaterialTask(task: MaterialTask)
    
    @Query("SELECT * FROM material_task WHERE barcode = :barcode LIMIT 1")
    suspend fun getMaterialTaskByBarcode(barcode: String): MaterialTask?
    
    @Query("SELECT * FROM material_task WHERE status = 'PENDING_SYNC'")
    suspend fun getPendingMaterialTasks(): List<MaterialTask>

    // SiteReport
    @Query("SELECT * FROM site_report")
    suspend fun getAllSiteReports(): List<SiteReportEntity>
    
    @Query("SELECT * FROM site_report WHERE status = 'PENDING_SYNC'")
    suspend fun getPendingSiteReports(): List<SiteReportEntity>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertSiteReport(report: SiteReportEntity)
    
    // Expense
    @Query("SELECT * FROM expense")
    suspend fun getAllExpenses(): List<ExpenseEntity>
    
    @Query("SELECT * FROM expense WHERE syncStatus = 'PENDING_SYNC'")
    suspend fun getPendingExpenses(): List<ExpenseEntity>
    
    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertExpense(expense: ExpenseEntity)
    
    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertExpenses(expenses: List<ExpenseEntity>)

    // AssignedTask
    @Query("SELECT * FROM assigned_task")
    suspend fun getAllAssignedTasks(): List<AssignedTaskEntity>

    @Query("SELECT * FROM assigned_task WHERE syncStatus = 'PENDING_SYNC'")
    suspend fun getPendingAssignedTasks(): List<AssignedTaskEntity>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertAssignedTask(task: AssignedTaskEntity)

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertAssignedTasks(tasks: List<AssignedTaskEntity>)


    // ==========================================
    // ACCOUNT-SPECIFIC OFFLINE SYNC QUERIES
    // ==========================================

    // Site reports belonging to the logged-in user
    @Query("""
        SELECT * FROM site_report
        WHERE status = 'PENDING_SYNC'
        AND ownerUserId = :userId
    """)
    suspend fun getPendingSiteReportsForUser(
        userId: String
    ): List<SiteReportEntity>

    // Material updates belonging to the logged-in user
    @Query("""
        SELECT * FROM material_task
        WHERE status = 'PENDING_SYNC'
        AND ownerUserId = :userId
    """)
    suspend fun getPendingMaterialTasksForUser(
        userId: String
    ): List<MaterialTask>

    // Expenses belonging to the logged-in user
    @Query("""
        SELECT * FROM expense
        WHERE syncStatus = 'PENDING_SYNC'
        AND ownerUserId = :userId
    """)
    suspend fun getPendingExpensesForUser(
        userId: String
    ): List<ExpenseEntity>

    // Assigned-task operations belonging to the logged-in user
    @Query("""
        SELECT * FROM assigned_task
        WHERE syncStatus = 'PENDING_SYNC'
        AND ownerUserId = :userId
    """)
    suspend fun getPendingAssignedTasksForUser(
        userId: String
    ): List<AssignedTaskEntity>

    // Sync queue operations belonging to the logged-in user
    @Query("""
        SELECT * FROM sync_queue
        WHERE ownerUserId = :userId
    """)
    suspend fun getSyncQueueItemsForUser(
        userId: String
    ): List<SyncQueueEntity>


    // SyncQueue
    @Query("SELECT * FROM sync_queue")
    suspend fun getAllSyncQueueItems(): List<SyncQueueEntity>

    @Insert
    suspend fun insertSyncQueueItem(item: SyncQueueEntity)

    @Delete
    suspend fun deleteSyncQueueItem(item: SyncQueueEntity)
    
    @Query("DELETE FROM sync_queue WHERE entityId = :id")
    suspend fun deleteSyncQueueItemByEntityId(id: String)
}

val MIGRATION_6_7 = object : Migration(6, 7) {
    override fun migrate(db: SupportSQLiteDatabase) {
        db.execSQL(
            "ALTER TABLE material_task ADD COLUMN ownerUserId TEXT NOT NULL DEFAULT ''"
        )
        db.execSQL(
            "ALTER TABLE site_report ADD COLUMN ownerUserId TEXT NOT NULL DEFAULT ''"
        )
        db.execSQL(
            "ALTER TABLE sync_queue ADD COLUMN ownerUserId TEXT NOT NULL DEFAULT ''"
        )
        db.execSQL(
            "ALTER TABLE expense ADD COLUMN ownerUserId TEXT NOT NULL DEFAULT ''"
        )
        db.execSQL(
            "ALTER TABLE assigned_task ADD COLUMN ownerUserId TEXT NOT NULL DEFAULT ''"
        )
    }
}
@Database(entities = [MaterialTask::class, SiteReportEntity::class, SyncQueueEntity::class, ExpenseEntity::class, AssignedTaskEntity::class], version = 7, exportSchema = false)
abstract class AppDatabase : RoomDatabase() {
    abstract fun appDao(): AppDao

    companion object {
        @Volatile
        private var INSTANCE: AppDatabase? = null

        fun getDatabase(context: Context): AppDatabase {
            return INSTANCE ?: synchronized(this) {
                val instance = Room.databaseBuilder(
                    context.applicationContext,
                    AppDatabase::class.java,
                    "app_database"
                ).addMigrations(MIGRATION_6_7)
                 .build()
                INSTANCE = instance
                instance
            }
        }
    }
}
