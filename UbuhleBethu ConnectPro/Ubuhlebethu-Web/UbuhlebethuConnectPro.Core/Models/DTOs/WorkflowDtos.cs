using System;
using System.Collections.Generic;
using System.Text;

namespace UbuhlebethuConnectPro.Core.Models.DTOs
{
    public class SendToManagerDto
    {
        public decimal? EstimatedBudget { get; set; }
    }

    public class ManagerRejectDto
    {
        public string Reason { get; set; } = string.Empty;
    }

    public class QsFeedbackDto
    {
        public string Feedback { get; set; } = string.Empty;
    }
}