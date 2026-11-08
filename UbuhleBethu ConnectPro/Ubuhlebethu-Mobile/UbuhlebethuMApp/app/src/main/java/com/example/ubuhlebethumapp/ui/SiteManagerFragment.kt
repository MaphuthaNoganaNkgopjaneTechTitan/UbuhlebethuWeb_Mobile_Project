package com.example.ubuhlebethumapp.ui

import android.Manifest
import android.annotation.SuppressLint
import android.app.DatePickerDialog
import android.content.Context
import android.content.pm.PackageManager
import android.location.Location
import android.net.Uri
import android.os.Bundle
import android.os.Environment
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.content.Intent
import android.widget.AdapterView
import android.widget.ArrayAdapter
import android.widget.Button
import android.widget.EditText
import android.widget.LinearLayout
import android.widget.Spinner
import android.widget.TextView
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.core.content.ContextCompat
import androidx.core.content.FileProvider
import androidx.fragment.app.Fragment
import androidx.lifecycle.lifecycleScope
import androidx.recyclerview.widget.LinearLayoutManager
import androidx.recyclerview.widget.RecyclerView
import androidx.work.Constraints
import androidx.work.ExistingWorkPolicy
import androidx.work.NetworkType
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.workDataOf
import com.example.ubuhlebethumapp.MainActivity
import com.example.ubuhlebethumapp.R
import com.example.ubuhlebethumapp.db.AppDatabase
import com.example.ubuhlebethumapp.db.AssignedTaskEntity
import com.example.ubuhlebethumapp.db.SiteReportEntity
import com.example.ubuhlebethumapp.db.SyncQueueEntity
import com.example.ubuhlebethumapp.model.AlertItem
import com.example.ubuhlebethumapp.model.AssignedTaskDto
import com.example.ubuhlebethumapp.model.AttendanceRecord
import com.example.ubuhlebethumapp.model.ExpenseItem
import com.example.ubuhlebethumapp.model.ProjectDto
import com.example.ubuhlebethumapp.network.ApiClient
import com.example.ubuhlebethumapp.network.TokenStore
import com.example.ubuhlebethumapp.utils.ImageCompressor
import com.example.ubuhlebethumapp.worker.SyncWorker
import com.example.ubuhlebethumapp.network.UserDto
import com.example.ubuhlebethumapp.network.BudgetSummary
import com.google.android.gms.location.FusedLocationProviderClient
import com.google.android.gms.location.LocationServices
import com.google.android.material.textfield.TextInputEditText
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import retrofit2.HttpException
import java.io.File
import java.io.IOException
import java.text.SimpleDateFormat
import java.util.Calendar
import java.util.Date
import java.util.Locale
import java.util.UUID
import android.widget.ImageButton
import android.view.KeyEvent
import android.view.inputmethod.EditorInfo
import com.example.ubuhlebethumapp.network.ConversationMessage
import com.example.ubuhlebethumapp.network.ConversationRequest

@SuppressLint("SetTextI18n")
class SiteManagerFragment : Fragment(), RefreshableData {

    private var loadedProjects: List<ProjectDto> = emptyList()
    private var selectedProjectId: String? = null
    private var selectedWorkerId: String? = null

    private var currentManagerTasks: List<AssignedTaskEntity> = emptyList()
    private var taskLoadVersion = 0
    private var currentPhotoPath: String? = null
    private var photoUri: Uri? = null
    private var currentLocation: Location? = null
    private lateinit var fusedLocationClient: FusedLocationProviderClient

    private lateinit var rvAlerts: RecyclerView
    private lateinit var rvTrackTasks: RecyclerView
    private lateinit var rvAttendance: RecyclerView
    private lateinit var rvExpenses: RecyclerView
    private lateinit var tvAttendanceSelectedDate: TextView
    private lateinit var tvProjectLoadStatus: TextView

    // Budget card views
    private lateinit var cardBudgetOverview: com.google.android.material.card.MaterialCardView
    private lateinit var tvBudgetProjectTitle: TextView
    private lateinit var tvBudgetTotal: TextView
    private lateinit var tvBudgetSpent: TextView
    private lateinit var tvBudgetRemaining: TextView
    private lateinit var tvBudgetVariance: TextView
    private lateinit var tvBudgetWarning: TextView

    private var selectedAttendanceDate: String =
        SimpleDateFormat("yyyy-MM-dd", Locale.getDefault()).format(Date())
    private var allExpensesList = mutableListOf<ExpenseItem>()

    // AI chat state
    private val aiChatHistory = mutableListOf<ConversationMessage>()
    private lateinit var aiChatAdapter: AiChatAdapter
    private lateinit var rvAiChat: RecyclerView

    private val takePictureLauncher =
        registerForActivityResult(ActivityResultContracts.TakePicture()) { success ->
            if (success) {
                val path = currentPhotoPath
                if (path != null) {
                    val file = File(path)
                    lifecycleScope.launch(Dispatchers.IO) {
                        val result = ImageCompressor.compressImageFile(file)
                        withContext(Dispatchers.Main) {
                            if (isAdded) {
                                val message = if (result.success) {
                                    val sizeText = ImageCompressor.formatFileSize(result.compressedSizeBytes)
                                    "Photo optimized for fast sync ($sizeText)"
                                } else {
                                    "Photo captured successfully"
                                }
                                Toast.makeText(requireContext(), message, Toast.LENGTH_SHORT).show()
                            }
                        }
                    }
                } else {
                    Toast.makeText(requireContext(), "Photo captured successfully", Toast.LENGTH_SHORT).show()
                }
            } else {
                currentPhotoPath = null
                photoUri = null
                Toast.makeText(requireContext(), "Photo capture failed", Toast.LENGTH_SHORT).show()
            }
        }

    private val locationPermissionLauncher =
        registerForActivityResult(ActivityResultContracts.RequestMultiplePermissions()) { permissions ->
            if (permissions[Manifest.permission.ACCESS_FINE_LOCATION] == true || permissions[Manifest.permission.ACCESS_COARSE_LOCATION] == true) {
                getLastLocation()
            } else {
                Toast.makeText(requireContext(), "Location permission denied", Toast.LENGTH_SHORT)
                    .show()
            }
        }

    private val registerMaterialBarcodeLauncher =
        registerForActivityResult(ActivityResultContracts.StartActivityForResult()) { result ->
            if (result.resultCode == androidx.appcompat.app.AppCompatActivity.RESULT_OK) {
                val barcode = result.data?.getStringExtra("SCAN_RESULT")
                if (!barcode.isNullOrBlank()) {
                    showRegisterMaterialDialog(barcode)
                }
            }
        }

    override fun onCreateView(
        inflater: LayoutInflater, container: ViewGroup?,
        savedInstanceState: Bundle?
    ): View = inflater.inflate(R.layout.fragment_site_manager, container, false)

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        fusedLocationClient = LocationServices.getFusedLocationProviderClient(requireActivity())

        tvProjectLoadStatus = view.findViewById(R.id.tvProjectLoadStatus)
        rvAlerts = view.findViewById(R.id.rvAlerts)
        val etDescription = view.findViewById<TextInputEditText>(R.id.etReportDescription)
        val etProgress = view.findViewById<TextInputEditText>(R.id.etReportProgress)
        val btnSubmit = view.findViewById<Button>(R.id.btnSubmitSiteReport)

        val spWorkerSelect = view.findViewById<Spinner>(R.id.spWorkerSelect)
        val etAssignTaskTitle = view.findViewById<TextInputEditText>(R.id.etAssignTaskTitle)
        val btnAssignTask = view.findViewById<Button>(R.id.btnAssignTask)
        rvTrackTasks = view.findViewById(R.id.rvManagerTrackTasks)

        rvAttendance = view.findViewById(R.id.rvManagerAttendance)
        rvExpenses = view.findViewById(R.id.rvExpenses)
        tvAttendanceSelectedDate = view.findViewById(R.id.tvAttendanceSelectedDate)
        val btnSelectAttendanceDate = view.findViewById<Button>(R.id.btnSelectAttendanceDate)

        rvAlerts.layoutManager = LinearLayoutManager(requireContext())
        rvAlerts.adapter = AlertsAdapter { clickedAlert ->
            Toast.makeText(requireContext(), "Alert: ${clickedAlert.title}", Toast.LENGTH_SHORT)
                .show()
        }

        rvTrackTasks.layoutManager = LinearLayoutManager(requireContext())
        rvTrackTasks.adapter = TasksAdapter(emptyList()) {}

        rvAttendance.layoutManager = LinearLayoutManager(requireContext())
        rvAttendance.adapter = AttendanceAdapter(emptyList())

        rvExpenses.layoutManager = LinearLayoutManager(requireContext())

        tvAttendanceSelectedDate.text = "Selected Date: $selectedAttendanceDate"

        loadActiveProjects(tvProjectLoadStatus)
        loadFieldWorkers(spWorkerSelect)
        loadExpenses()


        // Budget card views
        cardBudgetOverview = view.findViewById(R.id.cardBudgetOverview)
        tvBudgetProjectTitle = view.findViewById(R.id.tvBudgetProjectTitle)
        tvBudgetTotal = view.findViewById(R.id.tvBudgetTotal)
        tvBudgetSpent = view.findViewById(R.id.tvBudgetSpent)
        tvBudgetRemaining = view.findViewById(R.id.tvBudgetRemaining)
        tvBudgetVariance = view.findViewById(R.id.tvBudgetVariance)
        tvBudgetWarning = view.findViewById(R.id.tvBudgetWarning)

        // Initially hide budget card
        cardBudgetOverview.visibility = View.GONE

        btnSelectAttendanceDate.setOnClickListener {
            val calendar = Calendar.getInstance()
                val datePickerDialog = DatePickerDialog(
                    requireContext(),
                    { _, year, month, dayOfMonth ->
                        val cal = Calendar.getInstance()
                        cal.set(year, month, dayOfMonth)
                        selectedAttendanceDate =
                            SimpleDateFormat("yyyy-MM-dd", Locale.getDefault()).format(cal.time)
                        tvAttendanceSelectedDate.text = "Selected Date: $selectedAttendanceDate"
                        loadAttendanceForDate(selectedAttendanceDate)
                    },
                    calendar.get(Calendar.YEAR),
                    calendar.get(Calendar.MONTH),
                    calendar.get(Calendar.DAY_OF_MONTH)
                )
                datePickerDialog.show()
            }


            btnAssignTask.setOnClickListener {
                val workerUsername =
                    spWorkerSelect.selectedItem?.toString()?.trim()
                val taskTitle = etAssignTaskTitle.text.toString().trim()
                val projectId = selectedProjectId

                if (taskTitle.isBlank()) {
                    Toast.makeText(
                        requireContext(),
                        "Please enter a task description",
                        Toast.LENGTH_SHORT
                    ).show()
                    return@setOnClickListener
                }

                if (
                    workerUsername.isNullOrBlank() ||
                    workerUsername == "No registered field workers available" ||
                    workerUsername == "Unable to load field workers"
                ) {
                    Toast.makeText(
                        requireContext(),
                        "Please select a registered field worker",
                        Toast.LENGTH_SHORT
                    ).show()

                    return@setOnClickListener
                }

                if (projectId.isNullOrBlank()) {
                    Toast.makeText(
                        requireContext(),
                        "Please select a project",
                        Toast.LENGTH_SHORT
                    ).show()
                    return@setOnClickListener
                }

                val task = AssignedTaskDto(
                    id = UUID.randomUUID().toString(),
                    projectId = projectId,
                    taskTitle = taskTitle,
                    assignedWorker = workerUsername,
                    isCompleted = false
                )

                btnAssignTask.isEnabled = false
                spWorkerSelect.isEnabled = false

                viewLifecycleOwner.lifecycleScope.launch {
                    val ctx = context ?: return@launch

                    try {
                        val savedTask = withContext(Dispatchers.IO) {
                            ApiClient.taskService(ctx).assignTask(task)
                        }

                        etAssignTaskTitle.setText("")

                        Toast.makeText(
                            ctx,
                            "Task assigned to ${savedTask.assignedWorker}",
                            Toast.LENGTH_SHORT
                        ).show()

                        loadTrackTasks()

                    } catch (e: Exception) {
                        Toast.makeText(
                            ctx,
                            "Task assignment failed. Please try again.",
                            Toast.LENGTH_LONG
                        ).show()

                    } finally {
                        if (isAdded && view != null) {
                            btnAssignTask.isEnabled = true
                            spWorkerSelect.isEnabled = true
                        }
                    }
                }
            }


            btnSubmit.setOnClickListener {
                submitSiteReport(selectedProjectId, etDescription, etProgress, btnSubmit)
            }

            view.findViewById<Button>(R.id.btnManagerUploadPhoto).setOnClickListener {
                if (ContextCompat.checkSelfPermission(
                        requireContext(),
                        Manifest.permission.ACCESS_FINE_LOCATION
                    ) == PackageManager.PERMISSION_GRANTED
                ) {
                    getLastLocation()
                } else {
                    locationPermissionLauncher.launch(
                        arrayOf(
                            Manifest.permission.ACCESS_FINE_LOCATION,
                            Manifest.permission.ACCESS_COARSE_LOCATION
                        )
                    )
                }
                launchCamera()
            }

            view.findViewById<Button>(R.id.btnRegisterMaterialBarcode)?.setOnClickListener {
                val intent = Intent(requireContext(), ScannerActivity::class.java)
                registerMaterialBarcodeLauncher.launch(intent)
            }

            // ── AI Chat UI Setup (Site Manager) ────────────────────────────────────────
            rvAiChat = view.findViewById(R.id.rvAiChatManager)
            aiChatAdapter = AiChatAdapter(aiChatHistory)
            rvAiChat.layoutManager = LinearLayoutManager(requireContext()).apply { stackFromEnd = true }
            rvAiChat.adapter = aiChatAdapter

            val layoutAiBodyManager = view.findViewById<View>(R.id.layoutAiChatBodyManager)
            val btnAiToggleManager = view.findViewById<android.widget.ImageView>(R.id.btnAiToggleManager)
            val headerAiChatManager = view.findViewById<View>(R.id.headerAiChatManager)
            val tvAiHeaderSubtitleManager = view.findViewById<TextView>(R.id.tvAiHeaderSubtitleManager)

            var isAiExpandedManager = false
            fun toggleAiChatManager(forceExpand: Boolean = false) {
                if (forceExpand && isAiExpandedManager) return
                isAiExpandedManager = if (forceExpand) true else !isAiExpandedManager
                if (isAiExpandedManager) {
                    layoutAiBodyManager.visibility = View.VISIBLE
                    btnAiToggleManager.setImageResource(R.drawable.ic_chevron_up)
                    tvAiHeaderSubtitleManager.text = "Tap to shrink assistant"
                    rvAiChat.scrollToPosition(aiChatAdapter.itemCount - 1)
                } else {
                    layoutAiBodyManager.visibility = View.GONE
                    btnAiToggleManager.setImageResource(R.drawable.ic_chevron_down)
                    tvAiHeaderSubtitleManager.text = "Tap to expand / open assistant"
                }
            }

            headerAiChatManager.setOnClickListener { toggleAiChatManager() }
            btnAiToggleManager.setOnClickListener { toggleAiChatManager() }

            val tvTypingManager = view.findViewById<TextView>(R.id.tvAiTypingManager)
            val etAiInputManager = view.findViewById<TextInputEditText>(R.id.etAiInputManager)
            val btnAiSendManager = view.findViewById<ImageButton>(R.id.btnAiSendManager)

            if (aiChatHistory.isEmpty()) {
                aiChatAdapter.addMessage(ConversationMessage("assistant", "Hello! I'm your AI site assistant. Ask me about budgets, expenses, site reports, or project status."))
                rvAiChat.scrollToPosition(aiChatAdapter.itemCount - 1)
            }

            fun sendAiMessageManager(userText: String) {
                if (userText.isBlank()) return
                toggleAiChatManager(forceExpand = true)
                val ctx = context ?: return
                val userMsg = ConversationMessage("user", userText)
                aiChatAdapter.addMessage(userMsg)
                rvAiChat.scrollToPosition(aiChatAdapter.itemCount - 1)
                etAiInputManager.setText("")
                tvTypingManager.visibility = View.VISIBLE
                btnAiSendManager.isEnabled = false

                lifecycleScope.launch {
                    try {
                        val outgoing = aiChatHistory.dropWhile { it.role != "user" }.takeLast(16)
                        val request = ConversationRequest(
                            messages = outgoing,
                            projectId = selectedProjectId,
                            userRole = "SiteManager"
                        )
                        val response = ApiClient.aiService(ctx).conversation(request)
                        withContext(Dispatchers.Main) {
                            tvTypingManager.visibility = View.GONE
                            aiChatAdapter.addMessage(ConversationMessage("assistant", response.answer))
                            rvAiChat.scrollToPosition(aiChatAdapter.itemCount - 1)
                            btnAiSendManager.isEnabled = true
                        }
                    } catch (_: Exception) {
                        withContext(Dispatchers.Main) {
                            tvTypingManager.visibility = View.GONE
                            aiChatAdapter.addMessage(ConversationMessage("assistant", "⚠️ AI temporarily unavailable. Please check your connection and try again."))
                            rvAiChat.scrollToPosition(aiChatAdapter.itemCount - 1)
                            btnAiSendManager.isEnabled = true
                        }
                    }
                }
            }

            btnAiSendManager.setOnClickListener { sendAiMessageManager(etAiInputManager.text.toString().trim()) }
            etAiInputManager.setOnEditorActionListener { _, actionId, event ->
                if (actionId == EditorInfo.IME_ACTION_SEND || (event?.keyCode == KeyEvent.KEYCODE_ENTER && event.action == KeyEvent.ACTION_DOWN)) {
                    sendAiMessageManager(etAiInputManager.text.toString().trim())
                    true
                } else false
            }

            view.findViewById<Button>(R.id.btnAiQuickBudget).setOnClickListener {
                sendAiMessageManager("What is the budget status for the current project?")
            }
            view.findViewById<Button>(R.id.btnAiQuickExpenses).setOnClickListener {
                sendAiMessageManager("Show me the recent expenses for this project.")
            }
            view.findViewById<Button>(R.id.btnAiQuickStatus).setOnClickListener {
                sendAiMessageManager("What is the current progress and status of this project?")
            }

            val etExpenseTitle = view.findViewById<TextInputEditText>(R.id.etExpenseTitle)
            val etExpenseAmount = view.findViewById<TextInputEditText>(R.id.etExpenseAmount)
            val btnCaptureExpense = view.findViewById<Button>(R.id.btnCaptureExpense)

            btnCaptureExpense.setOnClickListener {
                val title = etExpenseTitle.text.toString().trim()
                val amountStr = etExpenseAmount.text.toString().trim()
                val amount = amountStr.toDoubleOrNull()

                if (title.isEmpty()) {
                    Toast.makeText(
                        requireContext(),
                        "Please enter a cost description",
                        Toast.LENGTH_SHORT
                    ).show()
                    return@setOnClickListener
                }
                if (amount == null || amount <= 0) {
                    Toast.makeText(
                        requireContext(),
                        "Please enter a valid cost amount",
                        Toast.LENGTH_SHORT
                    ).show()
                    return@setOnClickListener
                }

                val userId = TokenStore.getUserId(requireContext())

                if (userId.isNullOrBlank()) {
                    Toast.makeText(
                        requireContext(),
                        "Please sign in again before saving an expense",
                        Toast.LENGTH_SHORT
                    ).show()
                    return@setOnClickListener
                }

                val currentProjId = selectedProjectId ?: "proj1"

                val newExpense = ExpenseItem(
                    managerName = "Site Manager",
                    itemDescription = title,
                    amount = amount,
                    status = "APPROVED",
                    id = UUID.randomUUID().toString(),
                    projectId = currentProjId
                )

                lifecycleScope.launch(Dispatchers.IO) {
                    val ctx = context ?: return@launch
                    val database = AppDatabase.getDatabase(ctx)
                    val entity = com.example.ubuhlebethumapp.db.ExpenseEntity(
                        newExpense.id ?: UUID.randomUUID().toString(),
                        newExpense.managerName ?: "Site Manager",
                        newExpense.itemDescription ?: "",
                        newExpense.amount,
                        newExpense.status ?: "APPROVED",
                        newExpense.projectId ?: "",
                        ownerUserId = userId
                    )
                    database.appDao().insertExpense(entity)

                    enqueueSync(ctx.applicationContext)

                    withContext(Dispatchers.Main) {
                        if (!isAdded) return@withContext
                        etExpenseTitle.setText("")
                        etExpenseAmount.setText("")
                        Toast.makeText(
                            ctx,
                            "Site cost saved locally. Sync scheduled.",
                            Toast.LENGTH_SHORT
                        ).show()

                        allExpensesList.add(0, newExpense)
                        filterExpensesBySelectedProject()
                    }
                }
            }

            view.findViewById<android.widget.ImageButton>(R.id.btnSettingsSiteManager)
                ?.setOnClickListener {
                    val settingsSheet = SettingsBottomSheetDialogFragment()
                    settingsSheet.show(childFragmentManager, SettingsBottomSheetDialogFragment.TAG)
                }

            view.findViewById<Button>(R.id.btnLogoutSiteManager).setOnClickListener {
                (requireActivity() as MainActivity).performLogout()
            }
        }


        private fun checkAndDisplayDynamicRisks() {
            val pendingTasks = currentManagerTasks.filter {
                !it.isCompleted
            }

            val alertItems = pendingTasks.map { task ->
                AlertItem(
                    id = task.id,
                    title = "Task Pending",
                    description = "${task.taskTitle}\nAssigned to: ${task.assignedWorker}",
                    timestamp = "Action Required",
                    priority = "High",
                    category = "Pending Tasks"
                )
            }

            if (!isAdded || !::rvAlerts.isInitialized) return

            val adapter = rvAlerts.adapter as? AlertsAdapter ?: run {
                val newAdapter = AlertsAdapter { clickedAlert ->
                    Toast.makeText(
                        requireContext(),
                        "Alert: ${clickedAlert.title}",
                        Toast.LENGTH_SHORT
                    ).show()
                }
                rvAlerts.adapter = newAdapter
                newAdapter
            }

            adapter.submitList(alertItems)

            val targetVisibility = if (alertItems.isNotEmpty()) View.VISIBLE else View.GONE
            if (rvAlerts.visibility != targetVisibility) {
                rvAlerts.visibility = targetVisibility
            }
        }


        private fun loadExpenses() {
            lifecycleScope.launch(Dispatchers.IO) {
                val ctx = context ?: return@launch
                try {
                    val db = AppDatabase.getDatabase(ctx)
                    val apiExpenses = try {
                        ApiClient.expenseService(ctx).getExpenses()
                    } catch (_: Exception) {
                        emptyList()
                    }
                    val localExpenses = db.appDao().getAllExpenses()

                    val combined = if (apiExpenses.isNotEmpty()) {
                        apiExpenses
                    } else {
                        localExpenses.map {
                            ExpenseItem(
                                it.managerName,
                                it.itemDescription,
                                it.amount,
                                it.status,
                                it.id,
                                it.projectId
                            )
                        }
                    }

                    withContext(Dispatchers.Main) {
                        if (!isAdded) return@withContext
                        allExpensesList = combined.toMutableList()
                        filterExpensesBySelectedProject()
                    }
                } catch (_: Exception) {
                }
            }
        }

        private fun filterExpensesBySelectedProject() {
            if (!isAdded || view == null || !::rvExpenses.isInitialized) return

            val filtered = if (selectedProjectId != null) {
                allExpensesList.filter {
                    it.projectId == selectedProjectId || it.projectId.isNullOrEmpty()
                }
            } else {
                allExpensesList
            }

            val existingAdapter = rvExpenses.adapter as? ExpensesAdapter
            if (existingAdapter != null) {
                existingAdapter.updateList(filtered)
            } else {
                rvExpenses.adapter = ExpensesAdapter(filtered) { item ->
                    lifecycleScope.launch(Dispatchers.IO) {
                        val ctx = context ?: return@launch
                        val database = AppDatabase.getDatabase(ctx)
                        val entity = com.example.ubuhlebethumapp.db.ExpenseEntity(
                            item.id ?: UUID.randomUUID().toString(),
                            item.managerName ?: "Site Manager",
                            item.itemDescription ?: "",
                            item.amount,
                            item.status ?: "PENDING",
                            item.projectId ?: ""
                        )
                        database.appDao().insertExpense(entity)
                    }
                }
            }

            // Refresh budget when expenses change
            selectedProjectId?.let { loadProjectBudget(it) }
        }

        private fun loadAttendanceForDate(dateString: String) {
            lifecycleScope.launch(Dispatchers.IO) {
                val ctx = context ?: return@launch
                try {
                    val projId = selectedProjectId ?: ""
                    val attendanceRes =
                        ApiClient.attendanceService(ctx).listForProject(projId, dateString)
                    val serverRecords = if (attendanceRes.isSuccessful) attendanceRes.body()
                        ?: emptyList() else emptyList()

                    val fieldWorkers = try {
                        ApiClient.userService(ctx).getWorkers()
                    } catch (_: Exception) {
                        emptyList()
                    }

                    val finalAttendanceList = mutableListOf<AttendanceRecord>()

                    if (fieldWorkers.isNotEmpty()) {
                        for (worker in fieldWorkers) {
                            val record = serverRecords.find {
                                val wName = it.workerName ?: ""
                                val uName = worker.userName
                                val email = worker.email
                                (wName.isNotBlank() && uName.isNotBlank() && wName.equals(
                                    uName,
                                    ignoreCase = true
                                )) ||
                                        (wName.isNotBlank() && email.isNotBlank() && wName.equals(
                                            email,
                                            ignoreCase = true
                                        ))
                            }
                            if (record != null) {
                                finalAttendanceList.add(record)
                            } else {
                                finalAttendanceList.add(
                                    AttendanceRecord(
                                        workerName = if (worker.userName.isBlank()) worker.email else worker.userName,
                                        date = dateString,
                                        isPresent = false
                                    )
                                )
                            }
                        }
                    } else {
                        finalAttendanceList.addAll(serverRecords)
                    }

                    withContext(Dispatchers.Main) {
                        if (!isAdded || view == null || !::rvAttendance.isInitialized) return@withContext
                        val existing = rvAttendance.adapter as? AttendanceAdapter
                        if (existing != null) {
                            existing.updateList(finalAttendanceList)
                        } else {
                            rvAttendance.adapter = AttendanceAdapter(finalAttendanceList)
                        }
                    }
                } catch (_: Exception) {
                    withContext(Dispatchers.Main) {
                        if (isAdded && ::rvAttendance.isInitialized) {
                            (rvAttendance.adapter as? AttendanceAdapter)?.updateList(emptyList())
                                ?: run { rvAttendance.adapter = AttendanceAdapter(emptyList()) }
                        }
                    }
                }
            }
        }

        override fun refreshData() {
            if (!isAdded || view == null || !::rvExpenses.isInitialized || !::rvTrackTasks.isInitialized || !::rvAttendance.isInitialized || !::rvAlerts.isInitialized) return
            lifecycleScope.launch {
                val spWorkerSelect = view?.findViewById<Spinner>(R.id.spWorkerSelect)

                loadActiveProjects(tvProjectLoadStatus)
                if (spWorkerSelect != null) {
                    loadFieldWorkers(spWorkerSelect)
                }
                // loadActiveProjects refreshes tasks after resolving the current project.
                loadExpenses()
                loadAttendanceForDate(selectedAttendanceDate)
            }
        }


        private fun loadFieldWorkers(spinner: Spinner) {
            viewLifecycleOwner.lifecycleScope.launch {
                val ctx = context ?: return@launch
                // Capture selection before rebuilding the spinner adapter.
                val previousWorker = spinner.selectedItem?.toString()?.trim()

                try {
                    val users = withContext(Dispatchers.IO) {
                        // Only the server's authorized Field Worker directory is trusted.
                        ApiClient.userService(ctx).getWorkers()
                            .filter { user ->
                                (user.roles.isEmpty() || user.roles.any { role ->
                                    role.contains("Worker", ignoreCase = true) ||
                                            role.contains("Field", ignoreCase = true)
                                }) && !user.isLockedOut
                            }
                            .distinctBy { user ->
                                user.userName.ifBlank { user.email }
                                    .trim().lowercase(Locale.ROOT)
                            }
                            .sortedBy { user ->
                                user.userName.ifBlank { user.email }
                                    .lowercase(Locale.ROOT)
                            }
                    }

                    if (!isAdded || view == null) return@launch

                    val userNames = users.map {
                        it.userName.ifBlank { it.email }.trim()
                    }.filter { it.isNotBlank() }

                    val displayList = userNames.ifEmpty {
                        listOf("No registered field workers available")
                    }

                    val adapter = ArrayAdapter(ctx, R.layout.item_spinner, displayList)
                    adapter.setDropDownViewResource(R.layout.item_spinner_dropdown)
                    spinner.adapter = adapter

                    spinner.onItemSelectedListener = object : AdapterView.OnItemSelectedListener {
                        override fun onItemSelected(
                            parent: AdapterView<*>?, view: View?, position: Int, id: Long
                        ) {
                            selectedWorkerId = userNames.getOrNull(position)
                        }

                        override fun onNothingSelected(parent: AdapterView<*>?) {
                            selectedWorkerId = null
                        }
                    }

                    val selectionIndex = userNames.indexOfFirst {
                        it.equals(previousWorker, ignoreCase = true)
                    }.takeIf { it >= 0 } ?: 0

                    selectedWorkerId = userNames.getOrNull(selectionIndex)
                    if (userNames.isNotEmpty()) spinner.setSelection(selectionIndex)
                } catch (e: Exception) {
                    android.util.Log.e("WORKER_API", "Failed to load registered workers", e)
                    if (!isAdded || view == null) return@launch

                    selectedWorkerId = null
                    val adapter = ArrayAdapter(
                        ctx, R.layout.item_spinner, listOf("Unable to load field workers")
                    )
                    adapter.setDropDownViewResource(R.layout.item_spinner_dropdown)
                    spinner.adapter = adapter
                    Toast.makeText(
                        ctx, "Unable to retrieve registered field workers", Toast.LENGTH_LONG
                    ).show()
                }
            }
        }


        private fun loadTrackTasks() {
            val projectId = selectedProjectId
            val requestVersion = ++taskLoadVersion

            if (!isAdded || !::rvTrackTasks.isInitialized) return

            if (projectId.isNullOrBlank()) {
                currentManagerTasks = emptyList()
                rvTrackTasks.adapter = TasksAdapter(emptyList()) {}
                checkAndDisplayDynamicRisks()
                return
            }

            viewLifecycleOwner.lifecycleScope.launch {
                val ctx = context ?: return@launch

                try {
                    val serverTasks = withContext(Dispatchers.IO) {
                        ApiClient.taskService(ctx).getTasks(projectId)
                    }

                    if (!isAdded ||
                        requestVersion != taskLoadVersion ||
                        selectedProjectId != projectId
                    ) {
                        return@launch
                    }

                    val tasks = serverTasks.map {
                        AssignedTaskEntity(
                            id = it.id,
                            projectId = it.projectId,
                            taskTitle = it.taskTitle,
                            assignedWorker = it.assignedWorker,
                            isCompleted = it.isCompleted
                        )
                    }

                    val tasksChanged = currentManagerTasks != tasks
                    currentManagerTasks = tasks

                    if (tasksChanged || rvTrackTasks.adapter == null) {
                        rvTrackTasks.adapter = TasksAdapter(tasks) { updatedTask ->

                            viewLifecycleOwner.lifecycleScope.launch {
                                val currentCtx = context ?: return@launch

                                try {
                                    if (updatedTask.isCompleted) {
                                        withContext(Dispatchers.IO) {
                                            ApiClient.taskService(currentCtx)
                                                .completeTask(updatedTask.id)
                                        }

                                        Toast.makeText(
                                            currentCtx,
                                            "Task marked as completed",
                                            Toast.LENGTH_SHORT
                                        ).show()
                                    }
                                } catch (e: Exception) {
                                    Toast.makeText(
                                        currentCtx,
                                        "Could not update task",
                                        Toast.LENGTH_SHORT
                                    ).show()
                                } finally {
                                    if (isAdded) {
                                        loadTrackTasks()
                                    }
                                }
                            }
                        }
                    }

                    checkAndDisplayDynamicRisks()

                } catch (e: Exception) {
                    if (!isAdded ||
                        requestVersion != taskLoadVersion ||
                        selectedProjectId != projectId
                    ) {
                        return@launch
                    }

                    android.util.Log.e(
                        "MANAGER_TASKS",
                        "Failed to load project tasks",
                        e
                    )
                }
            }
        }


        private fun launchCamera() {
            try {
                val ctx = context ?: return
                val photoFile = createImageFile()
                val photoURI: Uri = FileProvider.getUriForFile(
                    ctx,
                    "${ctx.packageName}.fileprovider",
                    photoFile
                )
                photoUri = photoURI
                takePictureLauncher.launch(photoURI)
            } catch (_: Exception) {
                currentPhotoPath = null
                photoUri = null
                Toast.makeText(context, "Camera unavailable on this device", Toast.LENGTH_SHORT)
                    .show()
            }
        }

        @Throws(IOException::class)
        private fun createImageFile(): File {
            val timeStamp: String =
                SimpleDateFormat("yyyyMMdd_HHmmss", Locale.getDefault()).format(Date())
            val storageDir: File =
                requireContext().getExternalFilesDir(Environment.DIRECTORY_PICTURES)!!
            return File.createTempFile(
                "JPEG_${timeStamp}_",
                ".jpg",
                storageDir
            ).apply {
                currentPhotoPath = absolutePath
            }
        }

        @SuppressLint("MissingPermission")
        private fun getLastLocation() {
            val ctx = context ?: return
            fusedLocationClient.lastLocation.addOnSuccessListener { location: Location? ->
                if (location != null) {
                    currentLocation = location
                    Toast.makeText(ctx, "GPS Acquired", Toast.LENGTH_SHORT).show()
                } else {
                    fetchFallbackLocationFromApi()
                }
            }.addOnFailureListener {
                fetchFallbackLocationFromApi()
            }
        }

        private fun fetchFallbackLocationFromApi() {
            viewLifecycleOwner.lifecycleScope.launch {
                val ctx = context ?: return@launch
                try {
                    val response = ApiClient.locationService(ctx).getFallbackLocation()
                    currentLocation = Location("API").apply {
                        latitude = response.latitude
                        longitude = response.longitude
                    }
                    Toast.makeText(ctx, "GPS Acquired via API", Toast.LENGTH_SHORT).show()
                } catch (_: Exception) {
                    Toast.makeText(ctx, "Location acquisition failed", Toast.LENGTH_SHORT).show()
                }
            }
        }

        private fun loadActiveProjects(statusLabel: TextView) {
            viewLifecycleOwner.lifecycleScope.launch {
                val ctx = context ?: return@launch
                try {
                    val projects = withContext(Dispatchers.IO) {
                        ApiClient.siteReportService(ctx).getActiveProjects()
                    }
                    if (!isAdded || view == null) return@launch
                    loadedProjects = projects
                    val projectSpinner = view?.findViewById<Spinner>(R.id.spProjectSelect)

                    if (projects.isEmpty()) {
                        selectedProjectId = null
                        statusLabel.text = "No active projects found on the server."
                        projectSpinner?.let { spinner ->
                            val adapter = ArrayAdapter(
                                ctx, R.layout.item_spinner, listOf("No active projects")
                            )
                            adapter.setDropDownViewResource(R.layout.item_spinner_dropdown)
                            spinner.adapter = adapter
                        }
                        filterExpensesBySelectedProject()
                        loadTrackTasks() // Clears the previously selected project's list.
                        hideBudgetCard()
                        return@launch
                    }

                    statusLabel.text = "${projects.size} active project(s) loaded"
                    val previousIndex = projects.indexOfFirst { it.id == selectedProjectId }
                    val selectedIndex = if (previousIndex >= 0) previousIndex else 0
                    val resolvedProjectId = projects[selectedIndex].id

                    if (projectSpinner != null) {
                        val adapter = ArrayAdapter(
                            ctx, R.layout.item_spinner, projects.map { it.title }
                        )
                        adapter.setDropDownViewResource(R.layout.item_spinner_dropdown)
                        projectSpinner.onItemSelectedListener = null
                        projectSpinner.adapter = adapter
                        projectSpinner.setSelection(selectedIndex, false)

                        projectSpinner.onItemSelectedListener =
                            object : AdapterView.OnItemSelectedListener {
                                override fun onItemSelected(
                                    parent: AdapterView<*>?, view: View?, position: Int, id: Long
                                ) {
                                    if (position !in projects.indices) return
                                    val newProjectId = projects[position].id
                                    if (selectedProjectId != newProjectId) {
                                        selectedProjectId = newProjectId
                                        filterExpensesBySelectedProject()
                                        loadAttendanceForDate(selectedAttendanceDate)
                                        loadTrackTasks()
                                        loadProjectBudget(newProjectId)
                                    }
                                }

                                override fun onNothingSelected(parent: AdapterView<*>?) {}
                            }
                    }

                    selectedProjectId = resolvedProjectId
                    filterExpensesBySelectedProject()
                    loadAttendanceForDate(selectedAttendanceDate)
                    loadTrackTasks() // Refresh also when the selected project stays unchanged.
                    loadProjectBudget(resolvedProjectId)
                } catch (e: HttpException) {
                    if (isAdded) statusLabel.text = "Couldn't load projects (${e.code()})"
                } catch (e: IOException) {
                    if (isAdded) statusLabel.text = "Server unreachable"
                } catch (e: Exception) {
                    android.util.Log.e("MANAGER_PROJECTS", "Failed to load projects", e)
                    if (isAdded) statusLabel.text = "Couldn't load active projects"
                }
            }
        }


        private fun submitSiteReport(
            projectId: String?,
            etDescription: TextInputEditText,
            etProgress: TextInputEditText,
            btnSubmit: Button
        ) {
            val ctx = requireContext().applicationContext

            if (projectId.isNullOrBlank()) {
                Toast.makeText(
                    ctx,
                    "Please select an active project first",
                    Toast.LENGTH_SHORT
                ).show()
                return
            }

            val description = etDescription.text.toString().trim()
            val progress = etProgress.text.toString().trim().toIntOrNull()

            if (description.isBlank()) {
                Toast.makeText(ctx, "Enter a description", Toast.LENGTH_SHORT).show()
                return
            }

            if (progress == null || progress !in 0..100) {
                Toast.makeText(
                    ctx,
                    "Progress must be between 0 and 100",
                    Toast.LENGTH_SHORT
                ).show()
                return
            }

            val userId = TokenStore.getUserId(ctx)

            if (userId.isNullOrBlank()) {
                Toast.makeText(
                    ctx,
                    "Please sign in again before saving a report",
                    Toast.LENGTH_SHORT
                ).show()
                return
            }

            val selectedProject = loadedProjects.firstOrNull { it.id == projectId }
            val projLat = selectedProject?.latitude
            val projLon = selectedProject?.longitude
            val projRadius = selectedProject?.geofenceRadiusMeters ?: 300.0

            if (currentLocation != null && projLat != null && projLon != null) {
                val results = FloatArray(1)
                Location.distanceBetween(currentLocation!!.latitude, currentLocation!!.longitude, projLat, projLon, results)
                val dist = results[0]
                if (dist > (projRadius ?: 300.0)) {
                    Toast.makeText(ctx, "⚠️ Geofence Warning: You are ${dist.toInt()}m from the site (${(projRadius ?: 300.0).toInt()}m allowed). Submission flagged as Off-Site.", Toast.LENGTH_LONG).show()
                }
            }

            val reportEntity = SiteReportEntity(
                id = UUID.randomUUID().toString(),
                projectId = projectId,
                description = description,
                progressPercent = progress,
                photoPath = currentPhotoPath,
                latitude = currentLocation?.latitude,
                longitude = currentLocation?.longitude,
                status = "PENDING_SYNC",
                ownerUserId = userId
            )

            btnSubmit.isEnabled = false

            viewLifecycleOwner.lifecycleScope.launch {
                try {
                    withContext(Dispatchers.IO) {
                        val db = AppDatabase.getDatabase(ctx)

                        // Save locally before attempting any upload.
                        db.appDao().insertSiteReport(reportEntity)

                        db.appDao().insertSyncQueueItem(
                            SyncQueueEntity(
                                entityType = "SITE_REPORT",
                                entityId = reportEntity.id,
                                operation = "POST",
                                ownerUserId = userId
                            )
                        )
                    }

                    // Only schedule while the original account
                    // is still the active account.
                    if (TokenStore.getUserId(ctx) == userId) {
                        enqueueSync(ctx)
                    } else {
                        android.util.Log.w(
                            "OFFLINE_SYNC",
                            "Account changed; report retained locally"
                        )
                    }

                    if (!isAdded) return@launch

                    Toast.makeText(
                        ctx,
                        "Site report saved locally. Sync scheduled when eligible.",
                        Toast.LENGTH_LONG
                    ).show()

                    etDescription.setText("")
                    etProgress.setText("")
                    currentPhotoPath = null
                    photoUri = null
                    currentLocation = null

                } catch (e: Exception) {
                    if (e is kotlinx.coroutines.CancellationException) {
                        throw e
                    }

                    android.util.Log.e(
                        "OFFLINE_SYNC",
                        "Failed to save Site Manager report",
                        e
                    )

                    if (isAdded) {
                        Toast.makeText(
                            ctx,
                            "Could not save the report. Please try again.",
                            Toast.LENGTH_LONG
                        ).show()
                    }
                } finally {
                    btnSubmit.isEnabled = true
                }
            }
        }


        private fun enqueueSync(
            ctx: Context = requireContext().applicationContext
        ) {
            val userId = TokenStore.getUserId(ctx)

            if (userId.isNullOrBlank()) {
                android.util.Log.w(
                    "SYNC_SCHEDULER",
                    "No authenticated Site Manager. Sync not scheduled."
                )
                return
            }

            val constraints = Constraints.Builder()
                .setRequiredNetworkType(NetworkType.CONNECTED)
                .build()

            val workRequest = OneTimeWorkRequestBuilder<SyncWorker>()
                .setConstraints(constraints)
                .setInputData(
                    workDataOf("SYNC_USER_ID" to userId)
                )
                .build()

            WorkManager.getInstance(ctx)
                .enqueueUniqueWork(
                    "sync_$userId",
                    ExistingWorkPolicy.REPLACE,
                    workRequest
                )
        }

        /**
         * Loads the project budget from the API and updates the UI.
         */
        private fun loadProjectBudget(projectId: String) {
            viewLifecycleOwner.lifecycleScope.launch(Dispatchers.IO) {
                val ctx = context ?: return@launch
                try {
                    val response = ApiClient.budgetService(ctx).getProjectBudget(projectId)
                    withContext(Dispatchers.Main) {
                        if (!isAdded) return@withContext
                        displayBudget(response)
                    }
                } catch (e: retrofit2.HttpException) {
                    withContext(Dispatchers.Main) {
                        if (isAdded) {
                            hideBudgetCard()
                            if (e.code() == 404) {
                                // Project might not have budget set up yet
                                android.util.Log.d(
                                    "BUDGET",
                                    "No budget found for project: $projectId"
                                )
                            }
                        }
                    }
                } catch (e: Exception) {
                    withContext(Dispatchers.Main) {
                        if (isAdded) {
                            hideBudgetCard()
                        }
                    }
                    android.util.Log.e("BUDGET", "Failed to load budget for project: $projectId", e)
                }
            }
        }

        /**
         * Displays the budget data in the budget card.
         */
        private fun displayBudget(budget: BudgetSummary) {
            if (!isAdded || view == null) return

            cardBudgetOverview.visibility = View.VISIBLE
            tvBudgetProjectTitle.text = budget.projectTitle
            tvBudgetTotal.text =
                String.format(Locale.getDefault(), "R %.2f", budget.estimatedBudget)
            tvBudgetSpent.text =
                String.format(Locale.getDefault(), "R %.2f", budget.totalApprovedExpenses)
            tvBudgetRemaining.text =
                String.format(Locale.getDefault(), "R %.2f", budget.budgetRemaining)
            tvBudgetVariance.text =
                String.format(Locale.getDefault(), "Variance: %.2f%%", budget.variancePercent)

            // Show warning if over budget
            if (budget.isOverBudget) {
                tvBudgetWarning.text = "⚠️ OVER BUDGET!"
                tvBudgetWarning.visibility = View.VISIBLE
                tvBudgetRemaining.setTextColor(
                    ContextCompat.getColor(
                        requireContext(),
                        R.color.colorError
                    )
                )
            } else if (budget.variancePercent >= 80) {
                tvBudgetWarning.text = "⚠️ Approaching budget limit"
                tvBudgetWarning.visibility = View.VISIBLE
                tvBudgetRemaining.setTextColor(
                    ContextCompat.getColor(
                        requireContext(),
                        R.color.colorWarning
                    )
                )
            } else {
                tvBudgetWarning.visibility = View.GONE
                tvBudgetRemaining.setTextColor(
                    ContextCompat.getColor(
                        requireContext(),
                        R.color.colorSuccess
                    )
                )
            }
        }

        /**
         * Hides the budget card.
         */
        private fun hideBudgetCard() {
            if (!isAdded || view == null) return
            cardBudgetOverview.visibility = View.GONE
        }

        /**
         * Shows dialog to register material details for a scanned barcode.
         */
        private fun showRegisterMaterialDialog(barcode: String) {
            val ctx = context ?: return
            val layout = LinearLayout(ctx).apply {
                orientation = LinearLayout.VERTICAL
                setPadding(48, 24, 48, 16)
            }

            val tvBarcode = TextView(ctx).apply {
                text = "Scanned Barcode: $barcode"
                textSize = 13f
                setTypeface(null, android.graphics.Typeface.BOLD)
                setTextColor(ContextCompat.getColor(ctx, R.color.colorTextPrimary))
                setPadding(0, 0, 0, 16)
            }
            layout.addView(tvBarcode)

            val etName = EditText(ctx).apply {
                hint = "Material Name (e.g. AfriSam 50kg Cement)"
                textSize = 14f
                setPadding(0, 16, 0, 24)
            }
            layout.addView(etName)

            val etCost = EditText(ctx).apply {
                hint = "Unit Cost (e.g. 115.50)"
                inputType = android.text.InputType.TYPE_CLASS_NUMBER or android.text.InputType.TYPE_NUMBER_FLAG_DECIMAL
                textSize = 14f
                setPadding(0, 16, 0, 24)
            }
            layout.addView(etCost)

            val etUnit = EditText(ctx).apply {
                hint = "Unit of Measure (e.g. Bag, Meter, Units)"
                setText("Units")
                textSize = 14f
                setPadding(0, 16, 0, 16)
            }
            layout.addView(etUnit)

            com.google.android.material.dialog.MaterialAlertDialogBuilder(ctx)
                .setTitle("Register Material Barcode")
                .setView(layout)
                .setPositiveButton("Register") { _, _ ->
                    val name = etName.text.toString().trim()
                    val costStr = etCost.text.toString().trim()
                    val unit = etUnit.text.toString().trim().ifBlank { "Units" }

                    if (name.isBlank()) {
                        Toast.makeText(ctx, "Material name is required", Toast.LENGTH_SHORT).show()
                        return@setPositiveButton
                    }

                    val cost = costStr.toDoubleOrNull() ?: 0.0

                    lifecycleScope.launch(Dispatchers.IO) {
                        try {
                            val req = com.example.ubuhlebethumapp.network.RegisterMaterialRequest(
                                barcode = barcode,
                                name = name,
                                unitCost = cost,
                                unit = unit,
                                projectId = selectedProjectId
                            )
                            ApiClient.materialsService(ctx).registerMaterial(req)

                            val userId = TokenStore.getUserId(ctx) ?: "sitemanager"
                            val task = com.example.ubuhlebethumapp.db.MaterialTask(
                                id = UUID.randomUUID().toString(),
                                projectId = selectedProjectId ?: "unknown",
                                materialName = name,
                                barcode = barcode,
                                quantity = 1,
                                isScanned = true,
                                ownerUserId = userId
                            )
                            AppDatabase.getDatabase(ctx).appDao().insertMaterialTask(task)

                            withContext(Dispatchers.Main) {
                                if (!isAdded) return@withContext
                                Toast.makeText(ctx, "✅ Registered: $name (R${String.format(Locale.US, "%.2f", cost)})", Toast.LENGTH_LONG).show()
                            }
                        } catch (e: Exception) {
                            withContext(Dispatchers.Main) {
                                if (!isAdded) return@withContext
                                Toast.makeText(ctx, "Registration failed: ${e.message}", Toast.LENGTH_SHORT).show()
                            }
                        }
                    }
                }
                .setNegativeButton("Cancel", null)
                .show()
        }
    }


