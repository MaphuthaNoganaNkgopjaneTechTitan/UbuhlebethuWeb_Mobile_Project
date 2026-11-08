package com.example.ubuhlebethumapp.ui

import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.Button
import android.widget.TextView
import android.widget.Toast
import androidx.recyclerview.widget.RecyclerView
import com.example.ubuhlebethumapp.R
import com.example.ubuhlebethumapp.model.QuoteDto
import java.util.Locale

/**
 * Displays live pending quotes from GET /api/office/pending-quotes. Button clicks call back
 * into the Fragment (which owns the coroutine scope and the Retrofit service) rather than
 * making network calls here directly — keeps the adapter simple and testable.
 */
class QuoteAdapter(
    private val quotes: List<QuoteDto>,
    private val onGenerateDraft: (QuoteDto) -> Unit,
    private val onApprove: (QuoteDto) -> Unit
) : RecyclerView.Adapter<QuoteAdapter.ViewHolder>() {

    class ViewHolder(view: View) : RecyclerView.ViewHolder(view) {
        val reference: TextView = view.findViewById(R.id.tvQuoteReference)
        val client: TextView = view.findViewById(R.id.tvQuoteClient)
        val budget: TextView = view.findViewById(R.id.tvQuoteBudget)
        val status: TextView = view.findViewById(R.id.tvQuoteStatus)
        val btnDraft: Button = view.findViewById(R.id.btnGenerateDraft)
        val btnApprove: Button = view.findViewById(R.id.btnApproveQuoteItem)
        val btnReject: Button = view.findViewById(R.id.btnRejectQuoteItem)
    }

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): ViewHolder {
        val view = LayoutInflater.from(parent.context).inflate(R.layout.item_quote, parent, false)
        return ViewHolder(view)
    }

    override fun onBindViewHolder(holder: ViewHolder, position: Int) {
        val quote = quotes[position]

        holder.reference.text = "Quotation: ${quote.reference}"
        holder.client.text = "Client: ${quote.clientName} (${quote.projectType})"
        holder.budget.text = "Estimated: R ${String.format(Locale.getDefault(), "%.2f", quote.estimatedBudget)}"
        holder.status.text = "Status: ${quote.status}"

        holder.btnDraft.setOnClickListener { onGenerateDraft(quote) }
        holder.btnApprove.setOnClickListener { onApprove(quote) }

        // No backend endpoint for reject yet (see OfficeApiService.kt note) — honest
        // placeholder rather than pretending this does something real.
        holder.btnReject.setOnClickListener {
            Toast.makeText(
                holder.itemView.context,
                "Reject isn't wired to the backend yet — ask the web team for a reject endpoint",
                Toast.LENGTH_LONG
            ).show()
        }
    }

    override fun getItemCount(): Int = quotes.size
}