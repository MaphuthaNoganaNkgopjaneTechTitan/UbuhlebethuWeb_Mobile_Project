package com.example.ubuhlebethumapp.ui

import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.TextView
import androidx.recyclerview.widget.RecyclerView
import com.example.ubuhlebethumapp.R
import com.example.ubuhlebethumapp.model.AttendanceRecord

class AttendanceAdapter(private var list: List<AttendanceRecord>) :
    RecyclerView.Adapter<AttendanceAdapter.ViewHolder>() {

    fun updateList(newList: List<AttendanceRecord>) {
        list = newList
        notifyDataSetChanged()
    }

    class ViewHolder(view: View) : RecyclerView.ViewHolder(view) {
        val name: TextView = view.findViewById(R.id.tvWorkerName)
        val date: TextView = view.findViewById(R.id.tvAttendanceDate)
        val statusBadge: TextView = view.findViewById(R.id.tvStatusBadge)
    }

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): ViewHolder {
        val view = LayoutInflater.from(parent.context).inflate(R.layout.item_attendance, parent, false)
        return ViewHolder(view)
    }

    override fun onBindViewHolder(holder: ViewHolder, position: Int) {
        if (position !in list.indices) return
        val item = list[position]
        holder.name.text = item.workerName ?: "Worker"
        holder.date.text = item.date ?: ""

        if (item.isOffSite) {
            val dist = item.distanceFromSiteMeters?.toInt()?.let { " (${it}m)" } ?: ""
            holder.statusBadge.text = "⚠️ Off-Site$dist"
            holder.statusBadge.setBackgroundResource(R.drawable.bg_badge_high)
        } else if (item.isPresent) {
            holder.statusBadge.text = "✓ Present"
            holder.statusBadge.setBackgroundResource(R.drawable.bg_badge_low)
        } else {
            holder.statusBadge.text = "✕ Absent"
            holder.statusBadge.setBackgroundResource(R.drawable.bg_badge_high)
        }
    }

    override fun getItemCount(): Int = list.size
}
