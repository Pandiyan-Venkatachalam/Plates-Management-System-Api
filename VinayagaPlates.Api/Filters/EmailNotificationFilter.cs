using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VinayagaPlates.Application.Services;

namespace VinayagaPlates.Api.Filters
{
    public class EmailNotificationFilter : IAsyncResultFilter
    {
        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            var executedContext = await next();

            var request = context.HttpContext.Request;
            var method = request.Method.ToUpper();
            
            // Only care about CREATE, UPDATE, DELETE (POST, PUT, DELETE)
            if (method != "POST" && method != "PUT" && method != "DELETE")
                return;

            // Ignore login/auth endpoints
            if (request.Path.Value != null && request.Path.Value.Contains("auth", StringComparison.OrdinalIgnoreCase))
                return;

            // Only care if the status is successful (2xx)
            if (context.HttpContext.Response.StatusCode >= 200 && context.HttpContext.Response.StatusCode < 300)
            {
                var userName = context.HttpContext.User.Identity?.Name ?? "SYSTEM";
                var path = request.Path.Value;
                var moduleName = GetModuleName(path);
                
                var action = method == "POST" ? "Created" : method == "PUT" ? "Updated" : "Deleted";
                var color = method == "POST" ? "🟢" : method == "PUT" ? "🔵" : "🔴";
                var subject = $"{color} [VPMS] {moduleName} {action}";

                string detailsHtml = "No additional details available.";
                
                if (executedContext.Result is ObjectResult objectResult && objectResult.Value != null)
                {
                    var responseValue = objectResult.Value;
                    var dataType = responseValue.GetType();
                    var dataProp = dataType.GetProperty("Data");
                    if (dataProp != null)
                    {
                        var dataObj = dataProp.GetValue(responseValue);
                        if (dataObj != null)
                        {
                            detailsHtml = GenerateHtmlFromObject(dataObj);
                        }
                        else
                        {
                            var msgProp = dataType.GetProperty("Message");
                            var msgObj = msgProp?.GetValue(responseValue);
                            if (msgObj != null) detailsHtml = $"<ul><li><b>Message:</b> {msgObj}</li></ul>";
                        }
                    }
                }

                var scopeFactory = context.HttpContext.RequestServices.GetRequiredService<IServiceScopeFactory>();

                // Process email in the background so it doesn't delay the API response
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = scopeFactory.CreateScope();
                        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                        
                        string senderEmail = "";
                        string senderPass = "";
                        string recipientEmails = "";

                        if (userName.Equals("Pandiyan", StringComparison.OrdinalIgnoreCase))
                        {
                            senderEmail = config["EmailSettings:Users:Pandiyan:Email"];
                            senderPass = config["EmailSettings:Users:Pandiyan:AppPassword"];
                            recipientEmails = config["EmailSettings:Users:Ranjith:Email"];
                        }
                        else if (userName.Equals("Ranjith", StringComparison.OrdinalIgnoreCase))
                        {
                            senderEmail = config["EmailSettings:Users:Ranjith:Email"];
                            senderPass = config["EmailSettings:Users:Ranjith:AppPassword"];
                            recipientEmails = config["EmailSettings:Users:Pandiyan:Email"];
                        }
                        else 
                        {
                            senderEmail = config["EmailSettings:Users:Pandiyan:Email"];
                            senderPass = config["EmailSettings:Users:Pandiyan:AppPassword"];
                            recipientEmails = $"{config["EmailSettings:Users:Pandiyan:Email"]},{config["EmailSettings:Users:Ranjith:Email"]}";
                        }

                        if (string.IsNullOrEmpty(senderEmail) || string.IsNullOrEmpty(recipientEmails)) return;

                        string htmlBody = $@"
                        <div style='font-family: Arial, sans-serif; padding: 20px; max-width: 600px; border: 1px solid #ddd; border-radius: 8px;'>
                            <h2 style='color: #2c3e50; border-bottom: 2px solid #eee; padding-bottom: 10px;'>
                                Vinayaga Plates Notification
                            </h2>
                            <p><b>Hello Partners,</b></p>
                            <p>A record in <b>{moduleName}</b> has been {action.ToLower()} in VPMS.</p>
                            
                            <table style='width: 100%; margin-top: 15px; margin-bottom: 20px; background: #f9f9f9; padding: 10px; border-radius: 5px;'>
                                <tr><td style='padding: 5px 0;'><b>Action By:</b> {userName}</td></tr>
                                <tr><td style='padding: 5px 0;'><b>Time:</b> {GetIstTime()}</td></tr>
                            </table>

                            <h3 style='color: #34495e;'>📋 Details:</h3>
                            {detailsHtml}

                            <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0 15px;' />
                            <p style='font-size: 12px; color: #7f8c8d; text-align: center;'>
                                <i>This is an automated notification from Vinayaga Plates Management System.</i>
                            </p>
                        </div>";

                        await emailService.SendEmailAsync(senderEmail, senderPass, recipientEmails, subject, htmlBody);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending background email notification: {ex.Message}");
                    }
                });
            }
        }

        private string GetModuleName(string path)
        {
            if (string.IsNullOrEmpty(path)) return "System";
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[0].Equals("api", StringComparison.OrdinalIgnoreCase))
            {
                var module = parts[1];
                return char.ToUpper(module[0]) + module.Substring(1); // Capitalize first letter
            }
            return "System";
        }

        private string GenerateHtmlFromObject(object obj)
        {
            if (obj == null) return "";
            
            var properties = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            if (properties.Length == 0) return "";

            var sb = new StringBuilder();
            sb.Append("<ul style='list-style-type: none; padding: 0;'>");
            
            foreach (var prop in properties)
            {
                // Skip collections and complex nested objects to keep email clean
                if (prop.PropertyType.IsGenericType || prop.PropertyType.IsArray) continue;
                if (prop.PropertyType.Namespace != null && prop.PropertyType.Namespace.StartsWith("VinayagaPlates.Domain")) continue;

                try
                {
                    var val = prop.GetValue(obj);
                    if (val != null)
                    {
                        sb.Append($"<li style='padding: 4px 0; border-bottom: 1px solid #f1f1f1;'><b>{prop.Name}:</b> {val}</li>");
                    }
                }
                catch { }
            }
            sb.Append("</ul>");
            return sb.ToString();
        }

        private string GetIstTime()
        {
            try
            {
                TimeZoneInfo istZone;
                try
                {
                    istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
                }
                catch (TimeZoneNotFoundException)
                {
                    istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                }
                var istTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
                return istTime.ToString("dd-MMM-yyyy, hh:mm tt") + " IST";
            }
            catch
            {
                return DateTime.UtcNow.AddHours(5).AddMinutes(30).ToString("dd-MMM-yyyy, hh:mm tt") + " IST";
            }
        }
    }
}
