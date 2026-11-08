package com.example.ubuhlebethumapp

import android.annotation.SuppressLint
import android.content.Intent
import android.os.Bundle
import android.view.View
import android.widget.Button
import android.widget.ProgressBar
import android.widget.TextView
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import androidx.biometric.BiometricManager
import androidx.biometric.BiometricPrompt
import androidx.core.content.ContextCompat
import androidx.lifecycle.lifecycleScope
import com.example.ubuhlebethumapp.model.LoginRequest
import com.example.ubuhlebethumapp.model.UserRole
import com.example.ubuhlebethumapp.network.ApiClient
import com.example.ubuhlebethumapp.network.ChangePasswordRequest
import com.example.ubuhlebethumapp.network.TokenStore
import com.google.android.material.dialog.MaterialAlertDialogBuilder
import com.google.android.material.textfield.TextInputEditText
import kotlinx.coroutines.launch
import org.json.JSONObject
import retrofit2.HttpException
import java.io.IOException

class LoginActivity : BaseActivity() {

    @SuppressLint("MissingInflatedId")
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_login)

        ApiClient.init(this)

        // 1. If a valid session with an active mobile role already exists, prompt for Biometrics before opening MainActivity
        val existingRole = TokenStore.getRole(this)
        val existingToken = TokenStore.getToken(this)
        val existingUsername = TokenStore.getUsername(this) ?: ""

        if (!existingRole.isNullOrBlank() && !existingToken.isNullOrBlank()) {
            val isValidRole = try {
                UserRole.valueOf(existingRole)
                true
            } catch (_: Exception) {
                false
            }

            if (isValidRole) {
                val bm = BiometricManager.from(this)
                val auths = BiometricManager.Authenticators.BIOMETRIC_STRONG or BiometricManager.Authenticators.BIOMETRIC_WEAK
                if (bm.canAuthenticate(auths) == BiometricManager.BIOMETRIC_SUCCESS) {
                    // Trigger Biometric Login for existing active session
                    showBiometricPrompt(
                        onSuccess = {
                            navigateToMain(existingRole, existingUsername)
                        },
                        onError = { error ->
                            Toast.makeText(this, error, Toast.LENGTH_SHORT).show()
                        }
                    )
                }
            } else {
                TokenStore.clearTokens(this)
            }
        }

        val btnLogin          = findViewById<Button>(R.id.btnLogin)
        val btnBiometricLogin = findViewById<Button>(R.id.btnBiometricLogin) // Optional Biometric Button in activity_login.xml
        val etEmail           = findViewById<TextInputEditText>(R.id.etEmail)
        val etPassword        = findViewById<TextInputEditText>(R.id.etPassword)
        val progress          = findViewById<ProgressBar>(R.id.progressLogin)
        val tvError           = findViewById<TextView>(R.id.tvLoginError)

        // Handle manual Biometric button click (if present in layout)
        btnBiometricLogin?.setOnClickListener {
            if (!existingRole.isNullOrBlank() && !existingToken.isNullOrBlank()) {
                showBiometricPrompt(
                    onSuccess = { navigateToMain(existingRole, existingUsername) },
                    onError = { error -> Toast.makeText(this, error, Toast.LENGTH_SHORT).show() }
                )
            } else {
                Toast.makeText(this, "Please log in with password once to enable biometrics.", Toast.LENGTH_LONG).show()
            }
        }

        btnLogin.setOnClickListener {
            val username = etEmail.text.toString().trim()
            val password = etPassword.text.toString().trim()
            tvError.visibility = View.GONE

            if (username.isEmpty() || password.isEmpty()) {
                tvError.text = "Please enter your username and password."
                tvError.visibility = View.VISIBLE
                return@setOnClickListener
            }

            btnLogin.isEnabled = false
            progress.visibility = View.VISIBLE

            lifecycleScope.launch {
                try {
                    val response = ApiClient.authService(this@LoginActivity)
                        .login(LoginRequest(username = username, password = password))

                    // Determine role from server response — no spinner fallback
                    val serverRoles = response.roles.ifEmpty { listOfNotNull(response.role) }
                    val userRole = mapServerRoleToUserRole(serverRoles)

                    if (userRole == null) {
                        progress.visibility = View.GONE
                        btnLogin.isEnabled = true
                        tvError.text = "Your account has no mobile-compatible role assigned. Contact your administrator."
                        tvError.visibility = View.VISIBLE
                        return@launch
                    }

                    val needsPasswordReset = response.requiresPasswordReset || password == "TempPassword1!"
                    if (needsPasswordReset) {
                        // Save session temporarily so ApiClient includes the Bearer token for change-password call
                        TokenStore.saveSession(
                            this@LoginActivity,
                            token        = response.token,
                            refreshToken = response.refreshToken ?: "",
                            role         = userRole.name,
                            username     = username
                        )

                        progress.visibility = View.GONE
                        btnLogin.isEnabled = true

                        showChangePasswordDialog(
                            currentPassword = password,
                            roleName        = userRole.name,
                            username        = username
                        )
                        return@launch
                    }

                    // Persist the session (token + role) so the user stays logged in
                    TokenStore.saveSession(
                        this@LoginActivity,
                        token        = response.token,
                        refreshToken = response.refreshToken ?: "",
                        role         = userRole.name,
                        username     = username
                    )

                    navigateToMain(userRole.name, username)

                } catch (e: HttpException) {
                    progress.visibility = View.GONE
                    btnLogin.isEnabled = true
                    tvError.text = when (e.code()) {
                        401  -> "Invalid username or password."
                        403  -> "Account exists but access is denied."
                        else -> "Login failed (${e.code()}). Please try again."
                    }
                    tvError.visibility = View.VISIBLE

                } catch (_: IOException) {
                    progress.visibility = View.GONE
                    btnLogin.isEnabled = true
                    tvError.text = "Cannot reach server. Check your internet connection."
                    tvError.visibility = View.VISIBLE
                }
            }
        }
    }

    /**
     * Triggers the Android Biometric Prompt (Face ID / Fingerprint)
     */
    /**
     * Triggers the Android Biometric Prompt (Face ID / Fingerprint)
     */
    private fun showBiometricPrompt(
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
        val biometricManager = BiometricManager.from(this)
        val authenticators = BiometricManager.Authenticators.BIOMETRIC_STRONG or BiometricManager.Authenticators.BIOMETRIC_WEAK

        when (biometricManager.canAuthenticate(authenticators)) {
            BiometricManager.BIOMETRIC_SUCCESS -> {
                // Hardware is available and biometrics are enrolled
            }
            BiometricManager.BIOMETRIC_ERROR_NO_HARDWARE -> {
                onError("This device does not have biometric hardware.")
                return
            }
            BiometricManager.BIOMETRIC_ERROR_NONE_ENROLLED -> {
                onError("No biometric credentials enrolled. Please set up fingerprint or face unlock in system settings.")
                return
            }
            BiometricManager.BIOMETRIC_ERROR_HW_UNAVAILABLE -> {
                onError("Biometric sensor is currently unavailable. Please try again or use password.")
                return
            }
            else -> {
                onError("Biometric authentication unavailable.")
                return
            }
        }

        val executor = ContextCompat.getMainExecutor(this)

        val biometricPrompt = BiometricPrompt(this, executor,
            object : BiometricPrompt.AuthenticationCallback() {
                override fun onAuthenticationSucceeded(result: BiometricPrompt.AuthenticationResult) {
                    super.onAuthenticationSucceeded(result)
                    onSuccess()
                }

                override fun onAuthenticationError(errorCode: Int, errString: CharSequence) {
                    super.onAuthenticationError(errorCode, errString)
                    // If user tapped "Use Password" or dismissed/cancelled, do not show error toast
                    if (errorCode == BiometricPrompt.ERROR_NEGATIVE_BUTTON ||
                        errorCode == BiometricPrompt.ERROR_USER_CANCELED ||
                        errorCode == BiometricPrompt.ERROR_CANCELED) {
                        return
                    }
                    onError(errString.toString())
                }

                override fun onAuthenticationFailed() {
                    super.onAuthenticationFailed()
                    // The system UI already displays "Not recognized. Try again."
                }
            })

        val promptInfo = BiometricPrompt.PromptInfo.Builder()
            .setTitle(getString(R.string.app_name))
            .setSubtitle("Authenticate using Fingerprint or Face Unlock")
            .setNegativeButtonText("Use Password")
            .setAllowedAuthenticators(authenticators)
            .build()

        biometricPrompt.authenticate(promptInfo)
    }

    private fun showChangePasswordDialog(
        currentPassword: String,
        roleName: String,
        username: String
    ) {
        val dialogView = layoutInflater.inflate(R.layout.dialog_change_password, null)
        val etNewPassword = dialogView.findViewById<TextInputEditText>(R.id.etNewPassword)
        val etConfirmNewPassword = dialogView.findViewById<TextInputEditText>(R.id.etConfirmNewPassword)
        val progressChange = dialogView.findViewById<ProgressBar>(R.id.progressChangePassword)
        val tvChangeError = dialogView.findViewById<TextView>(R.id.tvChangePasswordError)
        val btnSubmit = dialogView.findViewById<Button>(R.id.btnSubmitNewPassword)
        val btnCancel = dialogView.findViewById<Button>(R.id.btnCancelChangePassword)

        val dialog = MaterialAlertDialogBuilder(this)
            .setView(dialogView)
            .setCancelable(false)
            .create()

        btnCancel.setOnClickListener {
            TokenStore.clearTokens(this)
            dialog.dismiss()
            Toast.makeText(this, "Password update is required before accessing your account.", Toast.LENGTH_LONG).show()
        }

        btnSubmit.setOnClickListener {
            val newPassword = etNewPassword.text?.toString()?.trim() ?: ""
            val confirmPassword = etConfirmNewPassword.text?.toString()?.trim() ?: ""
            tvChangeError.visibility = View.GONE

            if (newPassword.isEmpty() || confirmPassword.isEmpty()) {
                tvChangeError.text = "Please enter and confirm your new password."
                tvChangeError.visibility = View.VISIBLE
                return@setOnClickListener
            }

            if (newPassword.length < 6) {
                tvChangeError.text = "New password must be at least 6 characters long."
                tvChangeError.visibility = View.VISIBLE
                return@setOnClickListener
            }

            if (newPassword != confirmPassword) {
                tvChangeError.text = "Passwords do not match."
                tvChangeError.visibility = View.VISIBLE
                return@setOnClickListener
            }

            if (newPassword == currentPassword) {
                tvChangeError.text = "New password cannot be the same as your temporary password."
                tvChangeError.visibility = View.VISIBLE
                return@setOnClickListener
            }

            btnSubmit.isEnabled = false
            btnCancel.isEnabled = false
            progressChange.visibility = View.VISIBLE

            lifecycleScope.launch {
                try {
                    val changeReq = ChangePasswordRequest(
                        currentPassword = currentPassword,
                        newPassword = newPassword
                    )
                    val resp = ApiClient.authService(this@LoginActivity).changePassword(changeReq)

                    dialog.dismiss()
                    Toast.makeText(this@LoginActivity, resp.message ?: "Password changed successfully!", Toast.LENGTH_SHORT).show()
                    navigateToMain(roleName, username)
                } catch (e: HttpException) {
                    btnSubmit.isEnabled = true
                    btnCancel.isEnabled = true
                    progressChange.visibility = View.GONE

                    val errorBody = try {
                        e.response()?.errorBody()?.string()
                    } catch (_: Exception) {
                        null
                    }

                    val errorMessage = if (!errorBody.isNullOrBlank()) {
                        try {
                            val jsonObj = JSONObject(errorBody)
                            when {
                                jsonObj.has("error") -> jsonObj.getString("error")
                                jsonObj.has("message") -> jsonObj.getString("message")
                                else -> errorBody
                            }
                        } catch (_: Exception) {
                            errorBody
                        }
                    } else {
                        "Failed to update password (${e.code()}). Please try again."
                    }

                    tvChangeError.text = errorMessage
                    tvChangeError.visibility = View.VISIBLE
                } catch (e: Exception) {
                    btnSubmit.isEnabled = true
                    btnCancel.isEnabled = true
                    progressChange.visibility = View.GONE
                    tvChangeError.text = "Network error: ${e.message ?: "Please check your connection and try again."}"
                    tvChangeError.visibility = View.VISIBLE
                }
            }
        }

        dialog.show()
    }

    private fun navigateToMain(roleName: String, username: String) {
        startActivity(Intent(this, MainActivity::class.java).apply {
            putExtra("USER_ROLE", roleName)
            putExtra("USER_EMAIL", username)
            flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TASK
        })
        finish()
    }

    /**
     * Maps the server-side role strings to mobile UserRole enum.
     * Only MobileWorkers/Field Workers and Site Managers are allowed on the mobile app.
     */
    private fun mapServerRoleToUserRole(serverRoles: List<String>): UserRole? = when {
        serverRoles.any { it == "SiteManager" || it == "Manager" }        -> UserRole.SITE_MANAGER
        serverRoles.any { it == "FieldWorker" || it == "MobileWorker" }   -> UserRole.FIELD_WORKER
        else                                                               -> null
    }
}