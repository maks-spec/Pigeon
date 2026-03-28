using System.Threading.Tasks;

namespace Backend.Services;

public interface ISmsService
{
    Task SendSmsAsync(string phoneNumber, string message);
}
