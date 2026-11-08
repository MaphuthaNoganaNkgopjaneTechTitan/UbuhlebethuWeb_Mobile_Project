package com.example.ubuhlebethumapp.ui

import android.Manifest
import android.annotation.SuppressLint
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.location.Location
import android.net.Uri
import android.os.Bundle
import android.os.Environment
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.AdapterView
import android.widget.ArrayAdapter
import android.widget.Button
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
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.WorkManager
import com.example.ubuhlebethumapp.MainActivity
import com.example.ubuhlebethumapp.R
import com.example.ubuhlebethumapp.db.AppDatabase
import com.example.ubuhlebethumapp.db.AssignedTaskEntity
import com.example.ubuhlebethumapp.db.MaterialTask
import com.example.ubuhlebethumapp.db.SiteReportEntity
import com.example.ubuhlebethumapp.db.SyncQueueEntity
import com.example.ubuhlebethumapp.model.ProjectDto
import com.example.ubuhlebethumapp.network.ApiClient
import com.example.ubuhlebethumapp.network.CheckInRequest
import com.example.ubuhlebethumapp.network.CheckOutRequest
import com.example.ubuhlebethumapp.utils.ImageCompressor
import com.example.ubuhlebethumapp.worker.SyncWorker
import com.google.android.gms.location.FusedLocationProviderClient
import com.google.android.gms.location.LocationServices
import com.google.android.material.textfield.TextInputEditText
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import okhttp3.MediaType.Companion.toMediaTypeOrNull
import okhttp3.MultipartBody
import okhttp3.RequestBody
import okhttp3.RequestBody.Companion.asRequestBody
import okhttp3.RequestBody.Companion.toRequestBody
import java.io.File
import java.io.IOException
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.UUID
import com.example.ubuhlebethumapp.network.TokenStore
import android.widget.ImageButton
import android.view.KeyEvent
import android.view.inputmethod.EditorInfo
import com.example.ubuhlebethumapp.network.ConversationMessage
import com.example.ubuhlebethumapp.network.ConversationRequest

class FieldWorkerFragment : Fragment(), RefreshableData {

    private lateinit var etName: TextInputEditText
    private lateinit var tvStatus: TextView
    private lateinit var btnStart: Button
    private lateinit var btnFinish: Button
    private lateinit var rvAttendance: RecyclerView
    private lateinit var rvActiveTasks: RecyclerView
    private lateinit var spProjectSelect: Spinner
    private lateinit var tvProjectLoadStatus: TextView
    private lateinit var tvGpsStatus: TextView
    private lateinit var etWorkLogDesc: TextInputEditText

    private var loadedProjects: List<ProjectDto> = emptyList()
    private var activeAttendanceId: Int = -1
    private var selectedProjectId: String? = null
    private var currentPhotoPath: String? = null
    private var photoUri: Uri? = null
    private var currentLocation: Location? = null
    private lateinit var fusedLocationClient: FusedLocationProviderClient

    // AI chat state
    private val aiChatHistory = mutableListOf<ConversationMessage>()
    private lateinit var aiChatAdapterWorker: AiChatAdapter
    private lateinit var rvAiChatWorker: RecyclerView


    private val prefs by lazy {
        val ctx = requireContext()

        val username = TokenStore.getUsername(ctx)
            ?.trim()
            ?.lowercase(java.util.Locale.ROOT)
            ?: ""

        require(username.isNotBlank()) {
            "An authenticated username is required for attendance."
        }

        val accountKey = java.security.MessageDigest
            .getInstance("SHA-256")
            .digest(username.toByteArray(Charsets.UTF_8))
            .joinToString("") { "%02x".format(it) }

        ctx.getSharedPreferences(
            "field_worker_prefs_$accountKey",
            Context.MODE_PRIVATE
        )
    }


    private val takePictureLauncher = registerForActivityResult(ActivityResultContracts.TakePicture()) { success ->
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

    private val scanBarcodeLauncher = registerForActivityResult(ActivityResultContracts.StartActivityForResult()) { result ->
        if (result.resultCode == androidx.appcompat.app.AppCompatActivity.RESULT_OK) {
            val barcode = result.data?.getStringExtra("SCAN_RESULT")
            barcode?.let { scannedBarcode ->
                lifecycleScope.launch(Dispatchers.IO) {
                    val ctx = context ?: return@launch
                    val userId = TokenStore.getUserId(ctx)

                    if (userId.isNullOrBlank()) {
                        android.util.Log.w("OFFLINE_SYNC", "Material not saved: no authenticated account")
                        return@launch
                    }

                    val db = AppDatabase.getDatabase(ctx)

                    var registeredName: String? = null
                    var unitCost: Double = 0.0

                    try {
                        val lookup = ApiClient.materialsService(ctx).lookupMaterial(scannedBarcode)
                        registeredName = lookup.name
                        unitCost = lookup.unitCost
                    } catch (e: Exception) {
                        val local = db.appDao().getMaterialTaskByBarcode(scannedBarcode)
                        if (local != null && local.materialName != "Scanned Material") {
                            registeredName = local.materialName
                        }
                    }

                    if (registeredName.isNullOrBlank()) {
                        withContext(Dispatchers.Main) {
                            if (!isAdded) return@withContext
                            com.google.android.material.dialog.MaterialAlertDialogBuilder(ctx)
                                .setTitle("Item Not Registered")
                                .setMessage("Barcode: $scannedBarcode\n\nItem not found / not registered. Please ask your Site Manager to register this barcode.")
                                .setIcon(android.R.drawable.ic_dialog_alert)
                                .setPositiveButton("OK", null)
                                .show()
                        }
                        return@launch
                    }

                    val task = MaterialTask(
                        id = UUID.randomUUID().toString(),
                        projectId = selectedProjectId ?: "unknown",
                        materialName = registeredName,
                        barcode = scannedBarcode,
                        quantity = 1,
                        isScanned = true,
                        ownerUserId = userId
                    )

                    db.appDao().insertMaterialTask(task)
                    withContext(Dispatchers.Main) {
                        if (!isAdded) return@withContext
                        val costMsg = if (unitCost > 0) " (R${String.format(java.util.Locale.US, "%.2f", unitCost)})" else ""
                        Toast.makeText(ctx, "✅ $registeredName$costMsg verified & recorded", Toast.LENGTH_LONG).show()
                        enqueueSync()
                    }
                }
            }
        }
    }

    private val locationPermissionLauncher = registerForActivityResult(ActivityResultContracts.RequestMultiplePermissions()) { permissions ->
        if (permissions[Manifest.permission.ACCESS_FINE_LOCATION] == true || permissions[Manifest.permission.ACCESS_COARSE_LOCATION] == true) {
            getLastLocation()
        } else {
            Toast.makeText(requireContext(), "Location permission denied", Toast.LENGTH_SHORT).show()
        }
    }

    override fun onCreateView(
        inflater: LayoutInflater, container: ViewGroup?,
        savedInstanceState: Bundle?
    ): View = inflater.inflate(R.layout.fragment_field_worker, container, false)

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        fusedLocationClient = LocationServices.getFusedLocationProviderClient(requireActivity())

        etName       = view.findViewById(R.id.etWorkerNameInput)
        tvStatus     = view.findViewById(R.id.tvShiftStatus)
        btnStart     = view.findViewById(R.id.btnStartShift)
        btnFinish    = view.findViewById(R.id.btnFinishShift)
        rvAttendance = view.findViewById(R.id.rvAttendance)
        rvActiveTasks = view.findViewById(R.id.rvActiveTasks)
        spProjectSelect = view.findViewById(R.id.spFieldProjectSelect)
        tvProjectLoadStatus = view.findViewById(R.id.tvProjectLoadStatus)
        tvGpsStatus = view.findViewById(R.id.tvGpsStatus)
        etWorkLogDesc = view.findViewById(R.id.etWorkLogDesc)

        rvAttendance.layoutManager = LinearLayoutManager(requireContext())
        rvAttendance.adapter = AttendanceAdapter(emptyList())

        rvActiveTasks.layoutManager = LinearLayoutManager(requireContext())
        rvActiveTasks.adapter = TasksAdapter(emptyList()) {}

        val storedUsername = TokenStore.getUsername(requireContext()).orEmpty()

        val savedName = prefs.getString(KEY_NAME, null)
            ?: storedUsername

        etName.setText(savedName)

        activeAttendanceId = prefs.getInt(KEY_ATTENDANCE_ID, -1)
        setShiftState(onShift = prefs.getBoolean(KEY_ON_SHIFT, false))

        loadActiveProjects()
        loadAssignedTasks()

        view.findViewById<Button>(R.id.btnFieldScanMaterials).setOnClickListener {
            val intent = Intent(requireContext(), ScannerActivity::class.java)
            scanBarcodeLauncher.launch(intent)
        }

        view.findViewById<Button>(R.id.btnUploadPhoto).setOnClickListener {
            launchCamera()
        }

        view.findViewById<Button>(R.id.btnTagGPS).setOnClickListener {
            checkAndGetLocation()
        }

        view.findViewById<Button>(R.id.btnSubmitWorkLog).setOnClickListener {
            submitWorkLog()
        }

        spProjectSelect.onItemSelectedListener = object : AdapterView.OnItemSelectedListener {
            override fun onItemSelected(parent: AdapterView<*>?, view: View?, position: Int, id: Long) {
                if (loadedProjects.isNotEmpty() && position in loadedProjects.indices) {
                    selectedProjectId = loadedProjects[position].id
                    refreshList()
                    loadAssignedTasks()
                }
            }
            override fun onNothingSelected(parent: AdapterView<*>?) {}
        }

        btnStart.setOnClickListener {
            val name = etName.text.toString().trim()
            if (name.isEmpty()) {
                Toast.makeText(requireContext(), "Please enter your name", Toast.LENGTH_SHORT).show()
                return@setOnClickListener
            }
            if (selectedProjectId == null) {
                Toast.makeText(requireContext(), "Please select a project", Toast.LENGTH_SHORT).show()
                return@setOnClickListener
            }

            // Anti-Cheating & GPS Spoofing Verification
            if (currentLocation != null) {
                val isMock = if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.S) {
                    currentLocation!!.isMock
                } else {
                    @Suppress("DEPRECATION")
                    currentLocation!!.isFromMockProvider
                }
                if (isMock) {
                    Toast.makeText(requireContext(), "❌ Verification Blocked: Mock GPS / Location Spoofing detected. Please turn off fake GPS apps.", Toast.LENGTH_LONG).show()
                    return@setOnClickListener
                }
            } else {
                checkAndGetLocation()
                Toast.makeText(requireContext(), "Acquiring live GPS location... Please tap START JOB again once GPS is acquired.", Toast.LENGTH_SHORT).show()
                return@setOnClickListener
            }

            prefs.edit().putString(KEY_NAME, name).apply()

            val selectedProject = loadedProjects.firstOrNull { it.id == selectedProjectId }
            val projLat = selectedProject?.latitude
            val projLon = selectedProject?.longitude
            val projRadius = selectedProject?.geofenceRadiusMeters ?: 300.0

            if (currentLocation != null && projLat != null && projLon != null) {
                val results = FloatArray(1)
                Location.distanceBetween(currentLocation!!.latitude, currentLocation!!.longitude, projLat, projLon, results)
                val dist = results[0]
                if (dist > (projRadius ?: 300.0)) {
                    Toast.makeText(requireContext(), "⚠️ Geofence Warning: You are ${dist.toInt()}m from the site (${(projRadius ?: 300.0).toInt()}m allowed). Submission flagged as Off-Site.", Toast.LENGTH_LONG).show()
                }
            }

            lifecycleScope.launch {
                val ctx = context ?: return@launch
                try {
                    val req = CheckInRequest(selectedProjectId!!, currentLocation?.latitude, currentLocation?.longitude)
                    val res = ApiClient.attendanceService(ctx).checkIn(req)
                    if (res.isSuccessful && res.body() != null) {
                        val attendance = res.body()!!
                        activeAttendanceId = attendance.id
                        val checkIn = attendance.checkInTime
                        prefs.edit()
                            .putBoolean(KEY_ON_SHIFT, true)
                            .putString(KEY_CHECK_IN, checkIn)
                            .putInt(KEY_ATTENDANCE_ID, activeAttendanceId)
                            .apply()

                        tvStatus.text = "Checked in at $checkIn"
                        setShiftState(onShift = true)
                        refreshList()
                        Toast.makeText(ctx, "Shift started", Toast.LENGTH_SHORT).show()
                    } else {
                        Toast.makeText(ctx, "Check-in failed: ${res.code()}", Toast.LENGTH_SHORT).show()
                    }
                } catch (e: Exception) {
                    Toast.makeText(ctx, "Check-in error: ${e.message}", Toast.LENGTH_SHORT).show()
                }
            }
        }

        btnFinish.setOnClickListener {
            if (activeAttendanceId == -1) {
                Toast.makeText(requireContext(), "No active shift to finish", Toast.LENGTH_SHORT).show()
                return@setOnClickListener
            }

            lifecycleScope.launch {
                val ctx = context ?: return@launch
                try {
                    val req = CheckOutRequest(activeAttendanceId)
                    val res = ApiClient.attendanceService(ctx).checkOut(req)
                    if (res.isSuccessful && res.body() != null) {
                        val attendance = res.body()!!
                        val checkOut = attendance.checkOutTime

                        prefs.edit()
                            .putBoolean(KEY_ON_SHIFT, false)
                            .remove(KEY_CHECK_IN)
                            .remove(KEY_ATTENDANCE_ID)
                            .apply()

                        activeAttendanceId = -1
                        tvStatus.text = "Shift completed at $checkOut"
                        setShiftState(onShift = false)
                        refreshList()
                        Toast.makeText(ctx, "Job finished. Shift closed.", Toast.LENGTH_SHORT).show()
                    } else {
                        Toast.makeText(ctx, "Check-out failed: ${res.code()}", Toast.LENGTH_SHORT).show()
                    }
                } catch (e: Exception) {
                    Toast.makeText(ctx, "Check-out error: ${e.message}", Toast.LENGTH_SHORT).show()
                }
            }
        }

        view.findViewById<android.widget.ImageButton>(R.id.btnSettingsFieldWorker)?.setOnClickListener {
            val settingsSheet = SettingsBottomSheetDialogFragment()
            settingsSheet.show(childFragmentManager, SettingsBottomSheetDialogFragment.TAG)
        }

        view.findViewById<Button>(R.id.btnLogoutFieldWorker).setOnClickListener {
            (requireActivity() as MainActivity).performLogout()
        }

        // ── AI Chat UI Setup (Field Worker) ────────────────────────────────────────
        rvAiChatWorker = view.findViewById(R.id.rvAiChatWorker)
        aiChatAdapterWorker = AiChatAdapter(aiChatHistory)
        rvAiChatWorker.layoutManager = LinearLayoutManager(requireContext()).apply { stackFromEnd = true }
        rvAiChatWorker.adapter = aiChatAdapterWorker

        val layoutAiBodyWorker = view.findViewById<View>(R.id.layoutAiChatBodyWorker)
        val btnAiToggleWorker = view.findViewById<android.widget.ImageView>(R.id.btnAiToggleWorker)
        val headerAiChatWorker = view.findViewById<View>(R.id.headerAiChatWorker)
        val tvAiHeaderSubtitleWorker = view.findViewById<TextView>(R.id.tvAiHeaderSubtitleWorker)

        var isAiExpandedWorker = false
        fun toggleAiChatWorker(forceExpand: Boolean = false) {
            if (forceExpand && isAiExpandedWorker) return
            isAiExpandedWorker = if (forceExpand) true else !isAiExpandedWorker
            if (isAiExpandedWorker) {
                layoutAiBodyWorker.visibility = View.VISIBLE
                btnAiToggleWorker.setImageResource(R.drawable.ic_chevron_up)
                tvAiHeaderSubtitleWorker.text = "Tap to shrink assistant"
                rvAiChatWorker.scrollToPosition(aiChatAdapterWorker.itemCount - 1)
            } else {
                layoutAiBodyWorker.visibility = View.GONE
                btnAiToggleWorker.setImageResource(R.drawable.ic_chevron_down)
                tvAiHeaderSubtitleWorker.text = "Tap to expand / open assistant"
            }
        }

        headerAiChatWorker.setOnClickListener { toggleAiChatWorker() }
        btnAiToggleWorker.setOnClickListener { toggleAiChatWorker() }

        val tvTypingWorker = view.findViewById<TextView>(R.id.tvAiTypingWorker)
        val etAiInputWorker = view.findViewById<com.google.android.material.textfield.TextInputEditText>(R.id.etAiInputWorker)
        val btnAiSendWorker = view.findViewById<ImageButton>(R.id.btnAiSendWorker)

        // Add welcome message if empty
        if (aiChatHistory.isEmpty()) {
            aiChatAdapterWorker.addMessage(ConversationMessage("assistant",
                "Hello! I'm your AI site assistant. Ask me about your assigned tasks, site status, or how to submit logs."))
            rvAiChatWorker.scrollToPosition(aiChatAdapterWorker.itemCount - 1)
        }

        fun sendAiMessageWorker(userText: String) {
            if (userText.isBlank()) return
            toggleAiChatWorker(forceExpand = true)
            val ctx = context ?: return
            val userMsg = ConversationMessage("user", userText)
            aiChatAdapterWorker.addMessage(userMsg)
            rvAiChatWorker.scrollToPosition(aiChatAdapterWorker.itemCount - 1)
            etAiInputWorker.setText("")
            tvTypingWorker.visibility = android.view.View.VISIBLE
            btnAiSendWorker.isEnabled = false

            lifecycleScope.launch {
                try {
                    val outgoing = aiChatHistory.dropWhile { it.role != "user" }.takeLast(16)
                    val request = ConversationRequest(
                        messages = outgoing,
                        projectId = selectedProjectId,
                        userRole = "FieldWorker"
                    )
                    val response = ApiClient.aiService(ctx).conversation(request)
                    withContext(Dispatchers.Main) {
                        tvTypingWorker.visibility = android.view.View.GONE
                        aiChatAdapterWorker.addMessage(ConversationMessage("assistant", response.answer))
                        rvAiChatWorker.scrollToPosition(aiChatAdapterWorker.itemCount - 1)
                        btnAiSendWorker.isEnabled = true
                    }
                } catch (e: Exception) {
                    withContext(Dispatchers.Main) {
                        tvTypingWorker.visibility = android.view.View.GONE
                        aiChatAdapterWorker.addMessage(ConversationMessage("assistant",
                            "⚠️ AI temporarily unavailable. Please check your connection and try again."))
                        rvAiChatWorker.scrollToPosition(aiChatAdapterWorker.itemCount - 1)
                        btnAiSendWorker.isEnabled = true
                    }
                }
            }
        }

        btnAiSendWorker.setOnClickListener {
            sendAiMessageWorker(etAiInputWorker.text.toString().trim())
        }

        etAiInputWorker.setOnEditorActionListener { _, actionId, event ->
            if (actionId == EditorInfo.IME_ACTION_SEND ||
                (event?.keyCode == KeyEvent.KEYCODE_ENTER && event.action == KeyEvent.ACTION_DOWN)) {
                sendAiMessageWorker(etAiInputWorker.text.toString().trim())
                true
            } else false
        }

        view.findViewById<Button>(R.id.btnAiQuickTasksWorker).setOnClickListener {
            sendAiMessageWorker("What are my current assigned tasks for this site?")
        }
        view.findViewById<Button>(R.id.btnAiQuickProgressWorker).setOnClickListener {
            sendAiMessageWorker("What is the current progress and status of this site?")
        }
    }

    private fun loadAssignedTasks() {
        viewLifecycleOwner.lifecycleScope.launch {
            val ctx = context ?: return@launch
            val projectId = selectedProjectId

            // Clear tasks from any previous project or user.
            rvActiveTasks.adapter = TasksAdapter(emptyList()) {}

            try {
                // The API determines which tasks the
                // authenticated Field Worker can access.
                val apiTasks = withContext(Dispatchers.IO) {
                    ApiClient.taskService(ctx)
                        .getTasks(projectId)
                }

                val tasks = apiTasks.map {
                    AssignedTaskEntity(
                        id = it.id,
                        projectId = it.projectId,
                        taskTitle = it.taskTitle,
                        assignedWorker = it.assignedWorker,
                        isCompleted = it.isCompleted
                    )
                }

                // Ignore responses for a previously selected project.
                if (!isAdded || selectedProjectId != projectId) {
                    return@launch
                }

                rvActiveTasks.adapter = TasksAdapter(tasks) { updatedTask ->
                    viewLifecycleOwner.lifecycleScope.launch {
                        try {
                            if (updatedTask.isCompleted) {
                                withContext(Dispatchers.IO) {
                                    ApiClient.taskService(ctx)
                                        .completeTask(updatedTask.id)
                                }

                                Toast.makeText(
                                    ctx,
                                    "Task completed successfully",
                                    Toast.LENGTH_SHORT
                                ).show()
                            }

                            loadAssignedTasks()

                        } catch (e: Exception) {
                            Toast.makeText(
                                ctx,
                                "Unable to update task",
                                Toast.LENGTH_SHORT
                            ).show()

                            // Reload the authorized server state.
                            loadAssignedTasks()
                        }
                    }
                }

            } catch (e: Exception) {
                if (!isAdded) return@launch

                // Never display unrestricted cached tasks.
                rvActiveTasks.adapter = TasksAdapter(emptyList()) {}

                Toast.makeText(
                    ctx,
                    "Unable to load assigned tasks",
                    Toast.LENGTH_SHORT
                ).show()
            }
        }
    }


    override fun refreshData() {
        if (!isAdded || view == null || !::spProjectSelect.isInitialized || !::rvAttendance.isInitialized || !::rvActiveTasks.isInitialized) return
        lifecycleScope.launch {
            loadActiveProjects()
            loadAssignedTasks()
            refreshList()
        }
    }

    private fun checkAndGetLocation() {
        if (ContextCompat.checkSelfPermission(requireContext(), Manifest.permission.ACCESS_FINE_LOCATION) == PackageManager.PERMISSION_GRANTED) {
            getLastLocation()
        } else {
            locationPermissionLauncher.launch(arrayOf(Manifest.permission.ACCESS_FINE_LOCATION, Manifest.permission.ACCESS_COARSE_LOCATION))
        }
    }

    @SuppressLint("MissingPermission")
    private fun getLastLocation() {
        val ctx = context ?: return
        fusedLocationClient.lastLocation.addOnSuccessListener { location: Location? ->
            if (location != null) {
                currentLocation = location
                tvGpsStatus.text = "GPS Tagged: ${String.format(Locale.getDefault(), "%.4f", location.latitude)}, ${String.format(Locale.getDefault(), "%.4f", location.longitude)}"
                Toast.makeText(ctx, "GPS Tagged successfully!", Toast.LENGTH_SHORT).show()
            } else {
                fetchFallbackLocationFromApi()
            }
        }.addOnFailureListener {
            fetchFallbackLocationFromApi()
        }
    }

    private fun fetchFallbackLocationFromApi() {
        val ctx = context ?: return
        tvGpsStatus.text = "GPS: Tagging location..."
        viewLifecycleOwner.lifecycleScope.launch {
            try {
                val response = ApiClient.locationService(ctx).getFallbackLocation()
                val loc = Location("API").apply {
                    latitude = response.latitude
                    longitude = response.longitude
                }
                currentLocation = loc
                tvGpsStatus.text = "GPS Tagged: ${String.format(Locale.getDefault(), "%.4f", response.latitude)}, ${String.format(Locale.getDefault(), "%.4f", response.longitude)}"
                Toast.makeText(ctx, "GPS Tagged successfully!", Toast.LENGTH_SHORT).show()
            } catch (e: Exception) {
                tvGpsStatus.text = "GPS: Location detection failed"
            }
        }
    }

    private fun launchCamera() {
        try {
            val photoFile: File = createImageFile()
            val photoURI: Uri = FileProvider.getUriForFile(
                requireContext(),
                "${requireContext().packageName}.fileprovider",
                photoFile
            )
            photoUri = photoURI
            takePictureLauncher.launch(photoURI)
        } catch (_: Exception) {
            currentPhotoPath = null
            photoUri = null
            Toast.makeText(requireContext(), "Camera unavailable on this device/emulator", Toast.LENGTH_SHORT).show()
        }
    }

    @Throws(IOException::class)
    private fun createImageFile(): File {
        val timeStamp: String = SimpleDateFormat("yyyyMMdd_HHmmss", Locale.getDefault()).format(Date())
        val storageDir: File = requireContext().getExternalFilesDir(Environment.DIRECTORY_PICTURES)!!
        return File.createTempFile(
            "JPEG_${timeStamp}_",
            ".jpg",
            storageDir
        ).apply {
            currentPhotoPath = absolutePath
        }
    }


    private fun submitWorkLog() {
        val ctx = requireContext().applicationContext
        val userId = TokenStore.getUserId(ctx)

        if (userId.isNullOrBlank()) {
            Toast.makeText(
                ctx,
                "Please sign in again before saving a work log",
                Toast.LENGTH_SHORT
            ).show()
            return
        }

        val desc = etWorkLogDesc.text.toString().trim()
        if (desc.isBlank()) {
            Toast.makeText(
                ctx,
                "Please enter a work log description",
                Toast.LENGTH_SHORT
            ).show()
            return
        }

        val projectId = selectedProjectId
        if (projectId.isNullOrBlank()) {
            Toast.makeText(
                ctx,
                "Please select a project",
                Toast.LENGTH_SHORT
            ).show()
            return
        }

        val btnSubmit =
            view?.findViewById<Button>(R.id.btnSubmitWorkLog)

        btnSubmit?.isEnabled = false

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
            description = desc,
            progressPercent = 100,
            photoPath = currentPhotoPath,
            latitude = currentLocation?.latitude,
            longitude = currentLocation?.longitude,
            status = "PENDING_SYNC",
            ownerUserId = userId
        )

        viewLifecycleOwner.lifecycleScope.launch {
            try {
                withContext(Dispatchers.IO) {
                    val dao = AppDatabase.getDatabase(ctx).appDao()

                    dao.insertSiteReport(reportEntity)

                    dao.insertSyncQueueItem(
                        SyncQueueEntity(
                            entityType = "SITE_REPORT",
                            entityId = reportEntity.id,
                            operation = "POST",
                            ownerUserId = userId
                        )
                    )
                }

                enqueueSync(ctx)

                var directUploadSuccess = false
                withContext(Dispatchers.IO) {
                    try {
                        val textPlain = "text/plain".toMediaTypeOrNull()
                        val optionalFields = HashMap<String, RequestBody>()
                        reportEntity.latitude?.let { optionalFields["Latitude"] = it.toString().toRequestBody(textPlain) }
                        reportEntity.longitude?.let { optionalFields["Longitude"] = it.toString().toRequestBody(textPlain) }

                        var photoPart: MultipartBody.Part? = null
                        if (!reportEntity.photoPath.isNullOrBlank()) {
                            val file = File(reportEntity.photoPath)
                            if (file.exists()) {
                                ImageCompressor.compressImageFile(file)
                                val requestFile = file.asRequestBody("image/jpeg".toMediaTypeOrNull())
                                photoPart = MultipartBody.Part.createFormData("Photo", file.name, requestFile)
                            }
                        }

                        ApiClient.siteReportService(ctx).submitSiteReport(
                            projectId = reportEntity.projectId.toRequestBody(textPlain),
                            description = reportEntity.description.toRequestBody(textPlain),
                            progressPercent = reportEntity.progressPercent.toString().toRequestBody(textPlain),
                            optionalFields = optionalFields,
                            photo = photoPart,
                            clientReportId = reportEntity.id.toRequestBody(textPlain)
                        )

                        val dao = AppDatabase.getDatabase(ctx).appDao()
                        dao.insertSiteReport(reportEntity.copy(status = "SYNCED"))
                        dao.deleteSyncQueueItemByEntityId(reportEntity.id)
                        if (!reportEntity.photoPath.isNullOrBlank()) {
                            try { File(reportEntity.photoPath).delete() } catch (_: Exception) {}
                        }
                        directUploadSuccess = true
                    } catch (e: Exception) {
                        android.util.Log.w("SYNC_DIRECT", "Direct upload failed, background SyncWorker will retry: ${e.message}")
                    }
                }

                if (!isAdded) return@launch

                if (directUploadSuccess) {
                    Toast.makeText(
                        ctx,
                        "✅ Work log submitted and synced to office!",
                        Toast.LENGTH_SHORT
                    ).show()
                } else {
                    Toast.makeText(
                        ctx,
                        "Work log saved locally. Syncing in background...",
                        Toast.LENGTH_LONG
                    ).show()
                }

                etWorkLogDesc.setText("")
                currentPhotoPath = null
                photoUri = null
                currentLocation = null
                tvGpsStatus.text = "GPS: Not Tagged"

            } catch (e: Exception) {
                android.util.Log.e(
                    "OFFLINE_SYNC",
                    "Failed to save work log locally",
                    e
                )

                if (isAdded) {
                    Toast.makeText(
                        ctx,
                        "Unable to save work log. Please try again.",
                        Toast.LENGTH_LONG
                    ).show()
                }
            } finally {
                btnSubmit?.isEnabled = true
            }
        }
    }


    private fun loadActiveProjects() {
        lifecycleScope.launch {
            val ctx = context ?: return@launch
            try {
                val previousSelectedId = selectedProjectId
                val previousSelectionIndex = if (::spProjectSelect.isInitialized) spProjectSelect.selectedItemPosition else 0
                val projects = ApiClient.siteReportService(ctx).getActiveProjects()
                loadedProjects = projects

                if (!isAdded || view == null || !::spProjectSelect.isInitialized || !::tvProjectLoadStatus.isInitialized) return@launch
                if (projects.isEmpty()) {
                    tvProjectLoadStatus.text = "No active projects."
                    val emptyAdapter = ArrayAdapter(
                        ctx,
                        R.layout.item_spinner,
                        listOf("No projects available")
                    )
                    emptyAdapter.setDropDownViewResource(R.layout.item_spinner_dropdown)
                    spProjectSelect.adapter = emptyAdapter
                } else {
                    tvProjectLoadStatus.text = "Select a project"
                    val titles = projects.map { it.title }
                    val projectAdapter = ArrayAdapter(
                        ctx,
                        R.layout.item_spinner,
                        titles
                    )
                    projectAdapter.setDropDownViewResource(R.layout.item_spinner_dropdown)
                    spProjectSelect.adapter = projectAdapter

                    val restoredIndex = projects.indexOfFirst { it.id == previousSelectedId }
                    if (restoredIndex != -1) {
                        spProjectSelect.setSelection(restoredIndex, false)
                    } else if (previousSelectionIndex in projects.indices) {
                        spProjectSelect.setSelection(previousSelectionIndex, false)
                    }
                }
            } catch (e: Exception) {
                if (isAdded && ::tvProjectLoadStatus.isInitialized) {
                    tvProjectLoadStatus.text = "Couldn't load projects"
                }
            }
        }
    }

    private fun refreshList() {
        val projId = selectedProjectId ?: return
        val today = SimpleDateFormat("yyyy-MM-dd", Locale.getDefault()).format(Date())
        viewLifecycleOwner.lifecycleScope.launch(Dispatchers.IO) {
            val ctx = context ?: return@launch
            try {
                val res = ApiClient.attendanceService(ctx).listForProject(projId, today)
                if (res.isSuccessful) {
                    val list = res.body() ?: emptyList()
                    withContext(Dispatchers.Main) {
                        if (!isAdded || view == null || !::rvAttendance.isInitialized) return@withContext
                        val existing = rvAttendance.adapter as? AttendanceAdapter
                        if (existing != null) {
                            existing.updateList(list)
                        } else {
                            rvAttendance.adapter = AttendanceAdapter(list)
                        }
                    }
                }
            } catch (e: Exception) {
                // Ignore silent errors on refresh
            }
        }
    }

    private fun setShiftState(onShift: Boolean) {
        btnStart.isEnabled  = !onShift
        btnFinish.isEnabled = onShift
        etName.isEnabled    = !onShift
        if (!onShift) {
            tvStatus.text = "Not currently on shift"
        } else {
            val checkIn = prefs.getString(KEY_CHECK_IN, "Unknown")
            tvStatus.text = "Checked in at $checkIn"
        }
    }


    private fun enqueueSync(
        ctx: Context = requireContext().applicationContext
    ) {

        val userId = TokenStore.getUserId(ctx)

        if (userId.isNullOrBlank()) {
            android.util.Log.w(
                "SYNC_SCHEDULER",
                "No authenticated user. Sync not scheduled."
            )
            return
        }

        val constraints = androidx.work.Constraints.Builder()
            .setRequiredNetworkType(
                androidx.work.NetworkType.CONNECTED
            )
            .build()

        val workRequest = OneTimeWorkRequestBuilder<SyncWorker>()
            .setConstraints(constraints)
            .setInputData(
                androidx.work.workDataOf("SYNC_USER_ID" to userId)
            )
            .build()

        WorkManager.getInstance(ctx)
            .enqueueUniqueWork(
                "sync_$userId",
                androidx.work.ExistingWorkPolicy.REPLACE,
                workRequest
            )
    }


    companion object {
        private const val KEY_NAME     = "worker_name"
        private const val KEY_ON_SHIFT = "on_shift"
        private const val KEY_CHECK_IN = "check_in_time"
        private const val KEY_ATTENDANCE_ID = "attendance_id"
    }
}
