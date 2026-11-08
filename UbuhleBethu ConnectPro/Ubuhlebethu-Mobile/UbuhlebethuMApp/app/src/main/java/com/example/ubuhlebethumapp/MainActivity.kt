package com.example.ubuhlebethumapp

import android.content.Intent
import android.os.Bundle
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import androidx.fragment.app.Fragment
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.lifecycleScope
import androidx.lifecycle.repeatOnLifecycle
import com.example.ubuhlebethumapp.model.UserRole
import com.example.ubuhlebethumapp.network.ApiClient
import com.example.ubuhlebethumapp.network.NetworkMonitor
import com.example.ubuhlebethumapp.network.TokenStore
import com.example.ubuhlebethumapp.ui.FieldWorkerFragment
import com.example.ubuhlebethumapp.ui.RefreshableData
import com.example.ubuhlebethumapp.ui.SiteManagerFragment
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlin.time.Duration.Companion.seconds

class MainActivity : BaseActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)
        ApiClient.init(this)

        NetworkMonitor.startMonitoring(this) {
            runOnUiThread {
                Toast.makeText(this, "🌐 Internet / API connection restored — Auto-refreshing...", Toast.LENGTH_SHORT).show()
                val currentFragment = supportFragmentManager.findFragmentById(R.id.fragmentContainer)
                if (currentFragment is RefreshableData) {
                    currentFragment.refreshData()
                }
            }
        }

        // Resolve the role: prefer the intent extra (fresh login), then fall back to persisted session.
        val roleName = intent.getStringExtra("USER_ROLE")
            ?: TokenStore.getRole(this)

        // If there's no role at all (cleared session), send back to log in.
        if (roleName == null) {
            TokenStore.clearTokens(this)
            startActivity(Intent(this, LoginActivity::class.java).apply {
                flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TASK
            })
            finish()
            return
        }

        val userRole = try {
            UserRole.valueOf(roleName)
        } catch (_: IllegalArgumentException) {
            // Unrecognised role name stored — log out for safety
            TokenStore.clearTokens(this)
            startActivity(Intent(this, LoginActivity::class.java).apply {
                flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TASK
            })
            finish()
            return
        }

        // Load the initial fragment for this role.
        when (userRole) {
            UserRole.FIELD_WORKER  -> loadFragment(FieldWorkerFragment())
            UserRole.SITE_MANAGER  -> loadFragment(SiteManagerFragment())
        }

        // Periodic 30-second auto-refresh loop when online and app is active
        lifecycleScope.launch {
            repeatOnLifecycle(Lifecycle.State.RESUMED) {
                while (isActive) {
                    delay(30.seconds)
                    if (NetworkMonitor.isConnected.value) {
                        val currentFragment = supportFragmentManager.findFragmentById(R.id.fragmentContainer)
                        if (currentFragment is RefreshableData) {
                            currentFragment.refreshData()
                        }
                    }
                }
            }
        }
    }

    private fun loadFragment(fragment: Fragment): Boolean {
        supportFragmentManager.beginTransaction()
            .replace(R.id.fragmentContainer, fragment)
            .commit()
        return true
    }

    /** Clears the JWT + persisted role and returns to log in. Call from any fragment's logout button. */

    fun performLogout() {
        NetworkMonitor.stopMonitoring()

        // Identify the account before clearing its session.
        val userId = TokenStore.getUserId(this)

        // Clear credentials so they cannot be reused.
        TokenStore.clearTokens(this)
        ApiClient.reset()

        // Cancel pending synchronization for this account.
        if (!userId.isNullOrBlank()) {
            androidx.work.WorkManager.getInstance(this)
                .cancelUniqueWork("sync_$userId")
        }

        // Return to the login screen.
        startActivity(
            Intent(this, LoginActivity::class.java).apply {
                flags = Intent.FLAG_ACTIVITY_NEW_TASK or
                        Intent.FLAG_ACTIVITY_CLEAR_TASK
            }
        )

        finish()
    }


    override fun onDestroy() {
        super.onDestroy()
        NetworkMonitor.stopMonitoring()
    }
}
