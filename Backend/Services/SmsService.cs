using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Backend.Services;

public class SmsService : ISmsService
{
    private readonly ILogger<SmsService> _logger;
    
    public SmsService(ILogger<SmsService> logger)
    {
        _logger = logger;
    }
    
    public async Task SendSmsAsync(string phoneNumber, string message)
    {
        _logger.LogInformation($"[SMS] To: {phoneNumber}, Message: {message}");
        await Task.CompletedTask;
    }
}
