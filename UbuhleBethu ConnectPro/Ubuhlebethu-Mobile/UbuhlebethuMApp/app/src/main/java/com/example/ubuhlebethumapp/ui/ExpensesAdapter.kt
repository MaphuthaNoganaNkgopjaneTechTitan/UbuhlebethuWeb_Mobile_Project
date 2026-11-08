package com.example.ubuhlebethumapp.ui

import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.Button
import android.widget.TextView
import androidx.recyclerview.widget.RecyclerView
import com.example.ubuhlebethumapp.R
import com.example.ubuhlebethumapp.model.ExpenseItem

class ExpensesAdapter(
    private var list: List<ExpenseItem>,
    private val onExpenseUpdate: (ExpenseItem) -> Unit
) : RecyclerView.Adapter<ExpensesAdapter.ViewHolder>() {

    fun updateList(newList: List<ExpenseItem>) {
        list = newList
        notifyDataSetChanged()
    }

    class ViewHolder(view: View) : RecyclerView.ViewHolder(view) {
        val title: TextView = view.findViewById(R.id.tvExpenseTitle)
        val amount: TextView = view.findViewById(R.id.tvExpenseAmount)
        val approveBtn: Button = view.findViewById(R.id.btnApprove)
        val rejectBtn: Button = view.findViewById(R.id.btnReject)
    }

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): ViewHolder {
        val view = LayoutInflater.from(parent.context).inflate(R.layout.item_expense, parent, false)
        return ViewHolder(view)
    }

    override fun onBindViewHolder(holder: ViewHolder, position: Int) {
        if (position !in list.indices) return
        val item = list[position]
        holder.title.text = "${item.managerName ?: "Manager"} — ${item.itemDescription ?: ""}"
        val statusStr = item.status ?: "PENDING"
        val statusMarker = when (statusStr.uppercase()) {
            "APPROVED" -> "[✓ APPROVED]"
            "REJECTED" -> "[✕ REJECTED]"
            else -> "[⏳ PENDING]"
        }
        holder.amount.text = "R ${String.format(java.util.Locale.getDefault(), "%.2f", item.amount)}  $statusMarker"

        when (statusStr.uppercase()) {
            "APPROVED" -> {
                holder.approveBtn.visibility = View.GONE
                holder.rejectBtn.visibility = View.GONE
            }
            "REJECTED" -> {
                holder.approveBtn.visibility = View.GONE
                holder.rejectBtn.visibility = View.VISIBLE
                holder.rejectBtn.text = "✕ REJECTED"
                holder.rejectBtn.isEnabled = false
            }
            else -> {
                holder.approveBtn.visibility = View.VISIBLE
                holder.approveBtn.isEnabled = true
                holder.approveBtn.text = "✓ APPROVE"

                holder.rejectBtn.visibility = View.VISIBLE
                holder.rejectBtn.isEnabled = true
                holder.rejectBtn.text = "✕ REJECT"

                holder.approveBtn.setOnClickListener {
                    val currentPos = holder.adapterPosition
                    if (currentPos in list.indices && currentPos != RecyclerView.NO_POSITION) {
                        val currentItem = list[currentPos]
                        currentItem.status = "APPROVED"
                        notifyItemChanged(currentPos)
                        onExpenseUpdate(currentItem)
                    }
                }

                holder.rejectBtn.setOnClickListener {
                    val currentPos = holder.adapterPosition
                    if (currentPos in list.indices && currentPos != RecyclerView.NO_POSITION) {
                        val currentItem = list[currentPos]
                        currentItem.status = "REJECTED"
                        notifyItemChanged(currentPos)
                        onExpenseUpdate(currentItem)
                    }
                }
            }
        }
    }

    override fun getItemCount(): Int = list.size
}
