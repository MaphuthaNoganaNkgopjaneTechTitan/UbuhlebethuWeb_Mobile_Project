package com.example.ubuhlebethumapp.ui

import android.text.method.LinkMovementMethod
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.TextView
import androidx.core.text.HtmlCompat
import androidx.recyclerview.widget.RecyclerView
import com.example.ubuhlebethumapp.R
import com.example.ubuhlebethumapp.network.ConversationMessage

/**
 * RecyclerView adapter that renders conversational AI chat bubbles like an LLM.
 * User messages appear right-aligned (primary theme bubble).
 * Assistant messages appear left-aligned (clean light bubble) with rich formatted markdown.
 */
class AiChatAdapter(
    private val messages: MutableList<ConversationMessage> = mutableListOf()
) : RecyclerView.Adapter<AiChatAdapter.ChatViewHolder>() {

    inner class ChatViewHolder(itemView: View) : RecyclerView.ViewHolder(itemView) {
        val tvUser: TextView = itemView.findViewById(R.id.tvUserMessage)
        val tvAssistant: TextView = itemView.findViewById(R.id.tvAssistantMessage)

        init {
            tvAssistant.movementMethod = LinkMovementMethod.getInstance()
        }
    }

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): ChatViewHolder {
        val view = LayoutInflater.from(parent.context)
            .inflate(R.layout.item_ai_chat_message, parent, false)
        return ChatViewHolder(view)
    }

    override fun onBindViewHolder(holder: ChatViewHolder, position: Int) {
        val msg = messages[position]
        when (msg.role) {
            "user" -> {
                holder.tvUser.visibility = View.VISIBLE
                holder.tvAssistant.visibility = View.GONE
                holder.tvUser.text = msg.content
            }
            else -> {
                holder.tvAssistant.visibility = View.VISIBLE
                holder.tvUser.visibility = View.GONE
                holder.tvAssistant.text = formatMarkdownToSpanned(msg.content)
            }
        }
    }

    override fun getItemCount(): Int = messages.size

    /** Append a new message and trigger scroll notification. */
    fun addMessage(msg: ConversationMessage) {
        messages.add(msg)
        notifyItemInserted(messages.size - 1)
    }

    /** Clear conversation history. */
    fun clearMessages() {
        val size = messages.size
        messages.clear()
        notifyItemRangeRemoved(0, size)
    }

    companion object {
        /**
         * Converts simple Markdown formatting (bold, italic, bullet points, headers, backticks)
         * to styled Spanned text for display in Android TextViews.
         */
        fun formatMarkdownToSpanned(markdown: String): CharSequence {
            if (markdown.isBlank()) return ""

            var html = android.text.TextUtils.htmlEncode(markdown)

            // Convert headers: ### Header, ## Header, # Header
            html = html.replace(Regex("(?m)^\\s*#{1,6}\\s+(.*)$"), "<b><big>$1</big></b><br/>")

            // Convert bold: **text**
            html = html.replace(Regex("\\*\\*([^*]+)\\*\\*"), "<b>$1</b>")

            // Convert italic: *text* (avoiding bullet items)
            html = html.replace(Regex("(?<=[\\s(])\\*([^*\\s][^*]*)\\*(?=[\\s).,!?:;]|$)"), "<i>$1</i>")

            // Convert bullet points: - item, * item, • item
            html = html.replace(Regex("(?m)^\\s*[-*•]\\s+(.*)$"), "&bull; $1<br/>")

            // Convert numbered lists: 1. item
            html = html.replace(Regex("(?m)^\\s*(\\d+)[.)]\\s+(.*)$"), "<b>$1.</b> $2<br/>")

            // Convert inline code: `code`
            html = html.replace(Regex("`([^`]+)`"), "<tt><font color='#b91c1c'>$1</font></tt>")

            // Convert newlines to HTML break tags
            html = html.replace("\n", "<br/>")

            return HtmlCompat.fromHtml(html, HtmlCompat.FROM_HTML_MODE_COMPACT)
        }
    }
}
