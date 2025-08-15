using HealthcareAPI.Models;
using System.Threading.Tasks;

namespace HealthcareAPI.Services
{
    public interface IActivityLogService
    {
        Task LogActivityAsync(ActivityLog log);
    }
}
