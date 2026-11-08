package com.example.ubuhlebethumapp.network

import android.content.Context
import android.content.SharedPreferences
import android.os.Build
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKey

/**
 * Encrypted token storage. Uses EncryptedSharedPreferences with fallback protection
 * so Keystore decryption exceptions never crash the application on startup.
 */
object TokenStore {
    private const val PREFS_NAME = "ubuhlebethu_secure_prefs"
    private const val KEY_TOKEN = "jwt_token"
    private const val KEY_REFRESH = "refresh_token"
    private const val KEY_ROLE = "user_role"
    private const val KEY_USERNAME = "user_name"

    @Volatile
    private var cachedPrefs: SharedPreferences? = null

    private fun prefs(context: Context): SharedPreferences {
        cachedPrefs?.let { return it }
        return synchronized(this) {
            cachedPrefs?.let { return it }
            val appContext = context.applicationContext
            val sp = try {
                EncryptedSharedPreferences.create(
                    appContext,
                    PREFS_NAME,
                    MasterKey.Builder(appContext)
                        .setKeyScheme(MasterKey.KeyScheme.AES256_GCM)
                        .build(),
                    EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
                    EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM
                )
            } catch (e: Exception) {
                android.util.Log.w("TokenStore", "EncryptedSharedPreferences error: ${e.message}. Recreating fresh prefs.")
                try {
                    if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.N) {
                        appContext.deleteSharedPreferences(PREFS_NAME)
                    } else {
                        appContext.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE).edit().clear().commit()
                    }
                    EncryptedSharedPreferences.create(
                        appContext,
                        PREFS_NAME,
                        MasterKey.Builder(appContext)
                            .setKeyScheme(MasterKey.KeyScheme.AES256_GCM)
                            .build(),
                        EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
                        EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM
                    )
                } catch (_: Exception) {
                    appContext.getSharedPreferences("ubuhlebethu_unencrypted_prefs", Context.MODE_PRIVATE)
                }
            }
            cachedPrefs = sp
            sp
        }
    }

    fun saveTokens(context: Context, token: String, refreshToken: String) {
        try {
            prefs(context).edit()
                .putString(KEY_TOKEN, token)
                .putString(KEY_REFRESH, refreshToken)
                .commit()
        } catch (e: Exception) {
            android.util.Log.e("TokenStore", "Failed to save tokens", e)
        }
    }

    fun saveSession(context: Context, token: String, refreshToken: String, role: String, username: String) {
        try {
            prefs(context).edit()
                .putString(KEY_TOKEN, token)
                .putString(KEY_REFRESH, refreshToken)
                .putString(KEY_ROLE, role)
                .putString(KEY_USERNAME, username)
                .commit()
        } catch (e: Exception) {
            android.util.Log.e("TokenStore", "Failed to save session", e)
        }
    }

    fun getToken(context: Context): String? {
        val raw = try { prefs(context).getString(KEY_TOKEN, null) } catch (e: Exception) { null }
        if (raw.isNullOrBlank()) return null
        var cleaned = raw.trim()
        if (cleaned.startsWith("Bearer ", ignoreCase = true)) {
            cleaned = cleaned.substring(7).trim()
        }
        cleaned = cleaned.removeSurrounding("\"")
        return if (cleaned.isNotBlank()) cleaned else null
    }

    fun getRefreshToken(context: Context): String? {
        val raw = try { prefs(context).getString(KEY_REFRESH, null) } catch (e: Exception) { null }
        if (raw.isNullOrBlank()) return null
        var cleaned = raw.trim()
        cleaned = cleaned.removeSurrounding("\"")
        return if (cleaned.isNotBlank()) cleaned else null
    }

    /** Returns the persisted UserRole name (e.g. "FIELD_WORKER"), or null if not set. */
    fun getRole(context: Context): String? =
        try { prefs(context).getString(KEY_ROLE, null) } catch (e: Exception) { null }

    fun getUsername(context: Context): String? =
        try { prefs(context).getString(KEY_USERNAME, null) } catch (e: Exception) { null }

    fun getUserId(context: Context): String? {
        val token = getToken(context) ?: return null
        return getUserIdFromToken(token)
    }

    fun getUserIdFromToken(token: String): String? {
        return try {
            val payload = token.split(".").getOrNull(1)
                ?: return null

            val decoded = android.util.Base64.decode(
                payload,
                android.util.Base64.URL_SAFE or
                        android.util.Base64.NO_WRAP or
                        android.util.Base64.NO_PADDING
            )

            val json = org.json.JSONObject(
                String(decoded, Charsets.UTF_8)
            )

            json.optString("sub").takeIf { it.isNotBlank() }
        } catch (e: Exception) {
            null
        }
    }

    fun clearTokens(context: Context) {
        try {
            prefs(context).edit()
                .remove(KEY_TOKEN)
                .remove(KEY_REFRESH)
                .remove(KEY_ROLE)
                .remove(KEY_USERNAME)
                .commit()
        } catch (e: Exception) {
            android.util.Log.e("TokenStore", "Failed to clear tokens", e)
        }
    }
}
