package com.example.ubuhlebethumapp.ui

import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.ImageView
import android.widget.TextView
import androidx.recyclerview.widget.DiffUtil
import androidx.recyclerview.widget.ListAdapter
import androidx.recyclerview.widget.RecyclerView
import com.example.ubuhlebethumapp.R
import com.example.ubuhlebethumapp.model.AlertItem

class AlertsAdapter(
    private val onItemClick: (AlertItem) -> Unit
) : ListAdapter<AlertItem, AlertsAdapter.AlertViewHolder>(DiffCallback()) {

    class AlertViewHolder(itemView: View) : RecyclerView.ViewHolder(itemView) {
        val ivIcon: ImageView = itemView.findViewById(R.id.ivAlertIcon)
        val tvTitle: TextView = itemView.findViewById(R.id.tvAlertTitle)
        val tvDescription: TextView = itemView.findViewById(R.id.tvAlertMessage)
        val tvTimestamp: TextView = itemView.findViewById(R.id.tvAlertTime)
        val tvPriority: TextView = itemView.findViewById(R.id.tvAlertBadge)
    }

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): AlertViewHolder {
        val view = LayoutInflater.from(parent.context)
            .inflate(R.layout.item_alert_card, parent, false)
        return AlertViewHolder(view)
    }

    override fun onBindViewHolder(holder: AlertViewHolder, position: Int) {
        val alert = getItem(position)
        holder.tvTitle.text = alert.title
        holder.tvDescription.text = alert.description
        holder.tvTimestamp.text = alert.timestamp
        holder.tvPriority.text = alert.priority

        // Using ivIcon dynamically based on alert type (resolves unused property warning)
        when {
            alert.title.contains("Overdue", ignoreCase = true) -> {
                holder.ivIcon.setImageResource(R.drawable.ic_warning)
            }
            alert.title.contains("Schedule", ignoreCase = true) -> {
                holder.ivIcon.setImageResource(R.drawable.ic_warning) // or clock icon if you have one
            }
            else -> {
                holder.ivIcon.setImageResource(R.drawable.ic_warning)
            }
        }

        // Priority badge styling
        if (alert.priority.equals("High", ignoreCase = true)) {
            holder.tvPriority.setBackgroundResource(R.drawable.bg_badge_high)
        } else {
            holder.tvPriority.setBackgroundResource(R.drawable.bg_badge_medium)
        }

        holder.itemView.setOnClickListener { onItemClick(alert) }
    }

    class DiffCallback : DiffUtil.ItemCallback<AlertItem>() {
        override fun areItemsTheSame(oldItem: AlertItem, newItem: AlertItem): Boolean {
            return oldItem.id == newItem.id
        }

        override fun areContentsTheSame(oldItem: AlertItem, newItem: AlertItem): Boolean {
            return oldItem == newItem
        }
    }
}