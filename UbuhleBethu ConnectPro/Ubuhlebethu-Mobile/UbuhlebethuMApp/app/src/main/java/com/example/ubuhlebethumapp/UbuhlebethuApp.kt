package com.example.ubuhlebethumapp

import android.app.Application
import android.content.Context
import androidx.appcompat.app.AppCompatDelegate

class UbuhlebethuApp : Application() {
    override fun onCreate() {
        super.onCreate()
        val prefs = getSharedPreferences("settings_prefs", Context.MODE_PRIVATE)
        val savedTheme = prefs.getInt("theme_mode", AppCompatDelegate.MODE_NIGHT_FOLLOW_SYSTEM)
        AppCompatDelegate.setDefaultNightMode(savedTheme)
    }
}
