package com.example.ubuhlebethumapp.model

// Matches OfficeApiController.QuoteDto / QuotesApiController.QuoteDto on the backend.
data class QuoteDto(
    val id: String,
    val reference: String,
    val requestDate: String,
    val projectType: String,
    val siteLocation: String,
    val estimatedBudget: Double,
    val status: String,
    val isPaid: Boolean,
    val clientName: String,
    val email: String,
    val phone: String,
    val projectDescription: String,
    val createdBy: String,
    val invoiceId: String?
)