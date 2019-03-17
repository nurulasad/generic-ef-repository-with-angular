using System;
using System.Collections.Generic;

namespace ManagementPortal.Model.InsightsReportModel
{
    public class PlatformCampaignStatistics
    {
        public int TotalSent { get; set; }
        public DateTime SentTime { get; set; }
    }

    public class PlatformStatisticsCount
    {
        public string TotalCountName { get; set; }
        public int TotalCounts { get; set; }
    }
}
