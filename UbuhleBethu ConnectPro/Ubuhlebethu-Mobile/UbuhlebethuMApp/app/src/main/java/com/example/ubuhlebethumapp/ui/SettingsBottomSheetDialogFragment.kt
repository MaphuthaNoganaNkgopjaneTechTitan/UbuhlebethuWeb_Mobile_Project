package com.example.ubuhlebethumapp.ui

import android.content.Context
import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.Button
import android.widget.RadioButton
import android.widget.RadioGroup
import android.widget.TextView
import android.widget.Toast
import androidx.appcompat.app.AppCompatDelegate
import com.example.ubuhlebethumapp.R
import com.example.ubuhlebethumapp.db.AppDatabase
import com.example.ubuhlebethumapp.network.TokenStore
import com.google.android.material.bottomsheet.BottomSheetDialogFragment
import com.google.android.material.switchmaterial.SwitchMaterial
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import kotlin.math.abs

class SettingsBottomSheetDialogFragment : BottomSheetDialogFragment() {

    override fun onCreateView(
        inflater: LayoutInflater, container: ViewGroup?,
        savedInstanceState: Bundle?
    ): View? {
        return inflater.inflate(R.layout.bottom_sheet_settings, container, false)
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)

        val prefs = requireContext().getSharedPreferences("settings_prefs", Context.MODE_PRIVATE)

        val tvUsername = view.findViewById<TextView>(R.id.tvSettingsUsername)
        val tvRole = view.findViewById<TextView>(R.id.tvSettingsRole)
        val rgThemeMode = view.findViewById<RadioGroup>(R.id.rgThemeMode)
        val rbLight = view.findViewById<RadioButton>(R.id.rbThemeLight)
        val rbDark = view.findViewById<RadioButton>(R.id.rbThemeDark)
        val rbSystem = view.findViewById<RadioButton>(R.id.rbThemeSystem)

        val rgTextSize = view.findViewById<RadioGroup>(R.id.rgTextSize)
        val rbSmall = view.findViewById<RadioButton>(R.id.rbTextSizeSmall)
        val rbDefault = view.findViewById<RadioButton>(R.id.rbTextSizeDefault)
        val rbLarge = view.findViewById<RadioButton>(R.id.rbTextSizeLarge)
        val rbExtraLarge = view.findViewById<RadioButton>(R.id.rbTextSizeExtraLarge)

        val switchBiometrics = view.findViewById<SwitchMaterial>(R.id.switchBiometrics)
        val switchNotifications = view.findViewById<SwitchMaterial>(R.id.switchNotifications)
        val btnClearCache = view.findViewById<Button>(R.id.btnClearOfflineCache)

        // Load current user profile info from TokenStore
        val username = TokenStore.getUsername(requireContext()) ?: "User"
        val role = TokenStore.getRole(requireContext()) ?: "Role"
        tvUsername.text = username
        tvRole.text = "Role: ${role.replace("_", " ")}"

        // Load saved theme preference
        val savedTheme = prefs.getInt("theme_mode", AppCompatDelegate.MODE_NIGHT_FOLLOW_SYSTEM)
        when (savedTheme) {
            AppCompatDelegate.MODE_NIGHT_NO -> rbLight.isChecked = true
            AppCompatDelegate.MODE_NIGHT_YES -> rbDark.isChecked = true
            else -> rbSystem.isChecked = true
        }

        // Theme Mode Change Listener
        rgThemeMode.setOnCheckedChangeListener { _, checkedId ->
            val mode = when (checkedId) {
                R.id.rbThemeLight -> AppCompatDelegate.MODE_NIGHT_NO
                R.id.rbThemeDark -> AppCompatDelegate.MODE_NIGHT_YES
                else -> AppCompatDelegate.MODE_NIGHT_FOLLOW_SYSTEM
            }
            if (mode != savedTheme) {
                prefs.edit().putInt("theme_mode", mode).apply()
                AppCompatDelegate.setDefaultNightMode(mode)
            }
        }

        // Load saved font scale preference
        val savedScale = prefs.getFloat("font_scale", 1.0f)
        when {
            savedScale <= 0.90f -> rbSmall.isChecked = true
            savedScale >= 1.25f -> rbExtraLarge.isChecked = true
            savedScale >= 1.10f -> rbLarge.isChecked = true
            else -> rbDefault.isChecked = true
        }

        // Font scale change listener
        rgTextSize.setOnCheckedChangeListener { _, checkedId ->
            val newScale = when (checkedId) {
                R.id.rbTextSizeSmall -> 0.85f
                R.id.rbTextSizeLarge -> 1.15f
                R.id.rbTextSizeExtraLarge -> 1.30f
                else -> 1.0f
            }
            if (abs(newScale - savedScale) > 0.05f) {
                prefs.edit().putFloat("font_scale", newScale).apply()
                Toast.makeText(context, "Text scaling adjusted to ${(newScale * 100).toInt()}%", Toast.LENGTH_SHORT).show()
                dismiss()
                activity?.recreate()
            }
        }

        // Load saved biometrics & notifications switches
        switchBiometrics.isChecked = prefs.getBoolean("biometrics_enabled", false)
        switchBiometrics.setOnCheckedChangeListener { _, isChecked ->
            prefs.edit().putBoolean("biometrics_enabled", isChecked).apply()
            val msg = if (isChecked) "Biometric quick login enabled" else "Biometric quick login disabled"
            Toast.makeText(context, msg, Toast.LENGTH_SHORT).show()
        }

        switchNotifications.isChecked = prefs.getBoolean("notifications_enabled", true)
        switchNotifications.setOnCheckedChangeListener { _, isChecked ->
            prefs.edit().putBoolean("notifications_enabled", isChecked).apply()
            val msg = if (isChecked) "Push notifications enabled" else "Push notifications muted"
            Toast.makeText(context, msg, Toast.LENGTH_SHORT).show()
        }

        // Clear Cache Button
        btnClearCache.setOnClickListener {
            CoroutineScope(Dispatchers.IO).launch {
                val db = AppDatabase.getDatabase(requireContext())
                db.clearAllTables()
                withContext(Dispatchers.Main) {
                    Toast.makeText(context, "🗑️ Offline local database cache cleared!", Toast.LENGTH_SHORT).show()
                    dismiss()
                }
            }
        }
    }

    companion object {
        const val TAG = "SettingsBottomSheet"
    }
}
