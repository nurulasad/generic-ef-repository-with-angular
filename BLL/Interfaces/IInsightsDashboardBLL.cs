using ManagementPortal.Model.InsightsReportModel;
using System.Collections.Generic;

namespace ManagementPortal.BLL.Interfaces.Core
{
    public interface IInsightsDashboardBLL
    {
        List<PlatformCampaignStatistics> GetPlatformStatisticsEmailCampaigns();
        List<PlatformCampaignStatistics> GetPlatformStatisticsSMSCampaigns();
        List<PlatformStatisticsCount> GetPlatformStatisticsCount();
    }
}
