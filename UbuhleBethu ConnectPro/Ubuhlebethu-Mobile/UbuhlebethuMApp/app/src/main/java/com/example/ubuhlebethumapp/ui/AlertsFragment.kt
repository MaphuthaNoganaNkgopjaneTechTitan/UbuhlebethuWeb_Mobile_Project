package com.example.ubuhlebethumapp.ui

import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.Toast
import androidx.fragment.app.Fragment
import androidx.recyclerview.widget.LinearLayoutManager
import androidx.recyclerview.widget.RecyclerView
import com.google.android.material.tabs.TabLayout
import com.example.ubuhlebethumapp.R
import com.example.ubuhlebethumapp.model.AlertItem

class AlertsFragment : Fragment() {

    private lateinit var recyclerView: RecyclerView
    private lateinit var tabLayout: TabLayout
    private lateinit var alertsAdapter: AlertsAdapter
    private lateinit var allAlerts: List<AlertItem>

    override fun onCreateView(
        inflater: LayoutInflater, container: ViewGroup?,
        savedInstanceState: Bundle?
    ): View? {
        val view = inflater.inflate(R.layout.fragment_alerts, container, false)

        recyclerView = view.findViewById(R.id.rvAlerts)
        tabLayout = view.findViewById(R.id.tabLayoutAlerts)

        recyclerView.layoutManager = LinearLayoutManager(context)

        // Option A: Actively using clickedAlert to display item details and fix the unused parameter warning
        alertsAdapter = AlertsAdapter { clickedAlert ->
            Toast.makeText(
                requireContext(),
                "Selected Alert: ${clickedAlert.title}\n${clickedAlert.priority} Priority",
                Toast.LENGTH_SHORT
            ).show()
        }
        recyclerView.adapter = alertsAdapter

        // Sample automated alerts matching your UI dashboard mockup
        allAlerts = listOf(
            AlertItem("1", "Task Overdue", "Site Preparation – Phase 1\nOverdue by 3 days", "Today, 09:24", "High", "Overdue Tasks"),
            AlertItem("2", "Progress Behind Schedule", "Block B – Construction\nCurrent progress: 48% (planned: 65%)", "Yesterday, 16:40", "Medium", "Behind Schedule"),
            AlertItem("3", "Risk Detected", "Material Delivery\nExpected delay: 5 days", "Apr 12, 2025", "High", "Risks")
        )

        // Submit the initial list to ListAdapter
        alertsAdapter.submitList(allAlerts)

        // Set up the filter tabs (spelled as two words to resolve verb lint warning)
        if (tabLayout.tabCount == 0) {
            tabLayout.addTab(tabLayout.newTab().setText("All"))
            tabLayout.addTab(tabLayout.newTab().setText("Overdue Tasks"))
            tabLayout.addTab(tabLayout.newTab().setText("Risks"))
            tabLayout.addTab(tabLayout.newTab().setText("Behind Schedule"))
        }

        tabLayout.addOnTabSelectedListener(object : TabLayout.OnTabSelectedListener {
            override fun onTabSelected(tab: TabLayout.Tab?) {
                when (tab?.position) {
                    0 -> filterAlerts("All")
                    1 -> filterAlerts("Overdue Tasks")
                    2 -> filterAlerts("Risks")
                    3 -> filterAlerts("Behind Schedule")
                }
            }
            override fun onTabUnselected(tab: TabLayout.Tab?) {}
            override fun onTabReselected(tab: TabLayout.Tab?) {}
        })

        return view
    }

    private fun filterAlerts(category: String) {
        val filteredList = if (category == "All") {
            allAlerts
        } else {
            allAlerts.filter { it.category.equals(category, ignoreCase = true) }
        }
        alertsAdapter.submitList(filteredList)
    }
}