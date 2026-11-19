using ProjectManagement.Api.DTOs;

namespace ProjectManagement.Api.Services.Dashboard;

public interface IProjectDashboardService
{
    Task<DashboardSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken);
}
