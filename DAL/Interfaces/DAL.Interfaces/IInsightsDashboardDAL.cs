using ManagementPortal.Model.InsightsReportModel;
using System.Collections.Generic;

namespace ManagementPortal.DAL.Interfaces.Core
{
    public interface IInsightsDashboardDAL
    {
        List<PlatformCampaignStatistics> GetPlatformStatisticsEmailCampaigns();

        List<PlatformStatisticsCount> GetPlatformStatisticsCount();

        List<PlatformCampaignStatistics> GetPlatformStatisticsSMSCampaigns();
    }
}
