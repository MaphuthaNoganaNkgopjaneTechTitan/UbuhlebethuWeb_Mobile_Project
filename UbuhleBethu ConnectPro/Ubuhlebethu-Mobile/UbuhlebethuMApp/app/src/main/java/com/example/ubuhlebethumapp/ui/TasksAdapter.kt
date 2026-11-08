package com.example.ubuhlebethumapp.ui

import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.CheckBox
import android.widget.TextView
import androidx.recyclerview.widget.RecyclerView
import com.example.ubuhlebethumapp.R
import com.example.ubuhlebethumapp.db.AssignedTaskEntity

class TasksAdapter(
    private var list: List<AssignedTaskEntity>,
    private val onTaskCompletedToggle: (AssignedTaskEntity) -> Unit
) : RecyclerView.Adapter<TasksAdapter.ViewHolder>() {

    fun updateList(newList: List<AssignedTaskEntity>) {
        list = newList
        notifyDataSetChanged()
    }

    class ViewHolder(view: View) : RecyclerView.ViewHolder(view) {
        val title: TextView = view.findViewById(R.id.tvTaskTitle)
        val worker: TextView = view.findViewById(R.id.tvTaskWorker)
        val cbComplete: CheckBox = view.findViewById(R.id.cbTaskComplete)
    }

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): ViewHolder {
        val view = LayoutInflater.from(parent.context).inflate(R.layout.item_assigned_task, parent, false)
        return ViewHolder(view)
    }

    override fun onBindViewHolder(holder: ViewHolder, position: Int) {
        if (position !in list.indices) return
        val item = list[position]
        holder.title.text = item.taskTitle
        holder.worker.text = "Assigned to: ${item.assignedWorker}"
        
        holder.cbComplete.setOnCheckedChangeListener(null)
        holder.cbComplete.isChecked = item.isCompleted

        holder.cbComplete.setOnCheckedChangeListener { _, isChecked ->
            val currentPos = holder.adapterPosition
            if (currentPos in list.indices && currentPos != RecyclerView.NO_POSITION) {
                val currentItem = list[currentPos]
                currentItem.isCompleted = isChecked
                onTaskCompletedToggle(currentItem)
            }
        }
    }

    override fun getItemCount(): Int = list.size
}
