package com.example.ubuhlebethumapp

import android.content.Context
import androidx.appcompat.app.AppCompatActivity

open class BaseActivity : AppCompatActivity() {
    override fun attachBaseContext(newBase: Context) {
        val prefs = newBase.getSharedPreferences("settings_prefs", Context.MODE_PRIVATE)
        val fontScale = prefs.getFloat("font_scale", 1.0f)
        val overrideConfig = newBase.resources.configuration.apply {
            this.fontScale = fontScale
        }
        val context = newBase.createConfigurationContext(overrideConfig)
        super.attachBaseContext(context)
    }
}
