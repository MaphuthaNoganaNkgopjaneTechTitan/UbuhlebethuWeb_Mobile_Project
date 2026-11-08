package com.example.ubuhlebethumapp.network

import android.content.Context
import android.net.ConnectivityManager
import android.net.Network
import android.net.NetworkCapabilities
import android.net.NetworkRequest
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow

object NetworkMonitor {

    private val _isConnected = MutableStateFlow(false)
    val isConnected: StateFlow<Boolean> = _isConnected

    private var connectivityManager: ConnectivityManager? = null
    private var networkCallback: ConnectivityManager.NetworkCallback? = null

    fun startMonitoring(context: Context, onNetworkRestored: () -> Unit) {
        val cm = context.applicationContext.getSystemService(Context.CONNECTIVITY_SERVICE) as? ConnectivityManager
            ?: return
        connectivityManager = cm

        val initialConnected = isCurrentlyConnected(cm)
        _isConnected.value = initialConnected

        val builder = NetworkRequest.Builder()
            .addCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET)

        val callback = object : ConnectivityManager.NetworkCallback() {
            override fun onAvailable(network: Network) {
                val wasConnected = _isConnected.value
                _isConnected.value = true
                if (!wasConnected) {
                    onNetworkRestored()
                }
            }

            override fun onLost(network: Network) {
                _isConnected.value = isCurrentlyConnected(cm)
            }
        }

        networkCallback = callback
        try {
            cm.registerNetworkCallback(builder.build(), callback)
        } catch (e: Exception) {
            // Ignore
        }
    }

    fun stopMonitoring() {
        try {
            networkCallback?.let { connectivityManager?.unregisterNetworkCallback(it) }
        } catch (e: Exception) {
            // Ignore
        }
        networkCallback = null
    }

    private fun isCurrentlyConnected(cm: ConnectivityManager): Boolean {
        val activeNetwork = cm.activeNetwork ?: return false
        val capabilities = cm.getNetworkCapabilities(activeNetwork) ?: return false
        return capabilities.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET)
    }
}
