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
using VinayagaPlates.Application.Repositories;
using VinayagaPlates.Domain.Entities;
using System.Collections.Generic;

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

            // Ignore login/auth endpoints and WhatsApp broadcast endpoints
            if (request.Path.Value != null && (
                request.Path.Value.Contains("auth", StringComparison.OrdinalIgnoreCase) || 
                request.Path.Value.Contains("whatsapp", StringComparison.OrdinalIgnoreCase) ||
                request.Path.Value.Contains("broadcast", StringComparison.OrdinalIgnoreCase)
            )) return;

            // Only care if the status is successful (2xx)
            if (context.HttpContext.Response.StatusCode >= 200 && context.HttpContext.Response.StatusCode < 300)
            {
                var userName = context.HttpContext.User.Identity?.Name ?? "SYSTEM";
                var path = request.Path.Value;
                var moduleName = GetModuleName(path);
                
                var action = method == "POST" ? "Created" : method == "PUT" ? "Updated" : "Deleted";
                var color = method == "POST" ? "🟢" : method == "PUT" ? "🔵" : "🔴";
                var subject = $"{color} [VPMS] {moduleName} {action}";

                var actionDescription = $"A record in <b>{moduleName}</b> has been {action.ToLower()} in VPMS.";
                string detailsHtml = "No additional details available.";
                
                if (executedContext.Result is ObjectResult objectResult && objectResult.Value != null)
                {
                    var responseValue = objectResult.Value;
                    var dataType = responseValue.GetType();
                    
                    var msgProp = dataType.GetProperty("Message");
                    var msgObj = msgProp?.GetValue(responseValue);
                    if (msgObj != null && !string.IsNullOrWhiteSpace(msgObj.ToString()))
                    {
                        var msgString = msgObj.ToString();
                        actionDescription = $"<b>{msgString}</b>";
                        subject = $"{color} [VPMS] {msgString}";
                    }

                    var dataProp = dataType.GetProperty("Data");
                    if (dataProp != null)
                    {
                        var dataObj = dataProp.GetValue(responseValue);
                        if (dataObj != null)
                        {
                            detailsHtml = GenerateHtmlFromObject(dataObj);
                        }
                        else if (msgObj != null)
                        {
                            detailsHtml = $"<ul><li><b>Message:</b> {msgObj}</li></ul>";
                        }
                    }
                }
                
                // Try to extract entity ID from dataObj to fetch full details later
                int? entityId = null;
                if (executedContext.Result is ObjectResult objRes && objRes.Value != null)
                {
                    var dataProp = objRes.Value.GetType().GetProperty("Data");
                    if (dataProp != null)
                    {
                        var dataObj = dataProp.GetValue(objRes.Value);
                        if (dataObj != null)
                        {
                            var idProp = dataObj.GetType().GetProperty($"{moduleName}Id") ?? dataObj.GetType().GetProperty("Id") ?? dataObj.GetType().GetProperty("SaleId");
                            if (idProp != null && idProp.PropertyType == typeof(int))
                            {
                                entityId = (int)idProp.GetValue(dataObj)!;
                            }
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

                        if (userName.Contains("Pandiyan", StringComparison.OrdinalIgnoreCase))
                        {
                            senderEmail = config["EmailSettings:Users:Pandiyan:Email"];
                            senderPass = config["EmailSettings:Users:Pandiyan:AppPassword"];
                            recipientEmails = config["EmailSettings:Users:Ranjith:Email"];
                        }
                        else if (userName.Contains("Ranjith", StringComparison.OrdinalIgnoreCase))
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

                        string htmlBody = $@"
                        <div style='font-family: Arial, sans-serif; padding: 20px; max-width: 600px; border: 1px solid #ddd; border-radius: 8px;'>
                            <h2 style='color: #2c3e50; border-bottom: 2px solid #eee; padding-bottom: 10px;'>
                                Vinayaga Plates Notification
                            </h2>
                            <p><b>Hello Partners,</b></p>
                            <p>{actionDescription}</p>
                            
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

                        // If it's a Sale, try to fetch the full rich object and build the beautiful template
                        if (entityId.HasValue)
                        {
                            if (moduleName.Equals("Sales", StringComparison.OrdinalIgnoreCase) || (moduleName.Equals("Order", StringComparison.OrdinalIgnoreCase) && actionDescription.Contains("Sale")))
                            {
                                var salesRepo = scope.ServiceProvider.GetRequiredService<ISalesRepository>();
                                var fullSale = await salesRepo.GetSaleWithDetailsByIdAsync(entityId.Value);
                                if (fullSale != null)
                                {
                                    htmlBody = GenerateBeautifulSaleHtml(fullSale, actionDescription, userName, GetIstTime());
                                }
                            }
                            else if (moduleName.Equals("Order", StringComparison.OrdinalIgnoreCase))
                            {
                                var orderRepo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
                                var fullOrder = await orderRepo.GetOrderWithDetailsByIdAsync(entityId.Value);
                                if (fullOrder != null)
                                {
                                    htmlBody = GenerateBeautifulOrderHtml(fullOrder, actionDescription, userName, GetIstTime());
                                }
                            }
                            else if (moduleName.Equals("Purchase", StringComparison.OrdinalIgnoreCase))
                            {
                                var purchaseRepo = scope.ServiceProvider.GetRequiredService<IPurchaseRepository>();
                                var fullPurchase = await purchaseRepo.GetPurchaseWithDetailsByIdAsync(entityId.Value);
                                if (fullPurchase != null)
                                {
                                    htmlBody = GenerateBeautifulPurchaseHtml(fullPurchase, actionDescription, userName, GetIstTime());
                                }
                            }
                        }

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
            
            var ignoredFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { 
                "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt", "IsDeleted", "IsActive",
                "CategoryId", "VariantId", "UnitId", "ProductId", "CustomerId", "SupplierId", "BatchId", "PasswordHash"
            };

            foreach (var prop in properties)
            {
                // Skip system fields and internal foreign keys
                if (ignoredFields.Contains(prop.Name)) continue;

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

        private string GenerateBeautifulSaleHtml(Sale sale, string actionDescription, string userName, string time)
        {
            var sb = new StringBuilder();
            sb.Append($@"
            <div style='font-family: Arial, sans-serif; padding: 20px; max-width: 600px; border: 1px solid #ddd; border-radius: 8px;'>
                <h2 style='color: #2c3e50; border-bottom: 2px solid #eee; padding-bottom: 10px;'>
                    Hello Partners 👋
                </h2>
                <p style='font-size: 16px; color: #27ae60;'><b>🛒 {actionDescription.Replace("<b>", "").Replace("</b>", "")}</b></p>
                
                <h3 style='color: #34495e; margin-top: 20px;'>Sale Information</h3>
                <ul style='list-style-type: none; padding: 0;'>
                    <li style='padding: 4px 0;'><b>Sale ID:</b> {sale.SaleId}</li>
                    <li style='padding: 4px 0;'><b>Sale Number:</b> {sale.SaleNumber}</li>
                    <li style='padding: 4px 0;'><b>Customer:</b> {(sale.Customer?.CustomerName ?? "Unknown")}</li>
                    <li style='padding: 4px 0;'><b>Date & Time:</b> {time}</li>
                    <li style='padding: 4px 0;'><b>Recorded By:</b> {userName}</li>
                    <li style='padding: 4px 0;'><b>Status:</b> {(sale.Status == "COMPLETED" ? "✅ COMPLETED" : sale.Status)}</li>
                </ul>

                <h3 style='color: #34495e; margin-top: 20px;'>📦 Sale Details</h3>
                <table style='width: 100%; border-collapse: collapse; margin-bottom: 20px; font-size: 14px;'>
                    <thead>
                        <tr style='background-color: #f1f1f1;'>
                            <th style='padding: 8px; border: 1px solid #ddd; text-align: left;'>Plate Size</th>
                            <th style='padding: 8px; border: 1px solid #ddd; text-align: right;'>Quantity</th>
                            <th style='padding: 8px; border: 1px solid #ddd; text-align: right;'>Rate/Plate</th>
                            <th style='padding: 8px; border: 1px solid #ddd; text-align: right;'>Amount</th>
                        </tr>
                    </thead>
                    <tbody>");

            int totalQuantity = 0;
            decimal totalAmount = 0;
            if (sale.Details != null)
            {
                foreach (var detail in sale.Details)
                {
                    var pName = detail.Product?.ProductName ?? "Unknown";
                    var qty = detail.Quantity;
                    var rate = detail.UnitPrice;
                    var amt = qty * rate;
                    totalQuantity += qty;
                    totalAmount += amt;

                    sb.Append($@"
                        <tr>
                            <td style='padding: 8px; border: 1px solid #ddd; text-align: left;'>{pName}</td>
                            <td style='padding: 8px; border: 1px solid #ddd; text-align: right;'>{qty:N0}</td>
                            <td style='padding: 8px; border: 1px solid #ddd; text-align: right;'>₹{rate:N2}</td>
                            <td style='padding: 8px; border: 1px solid #ddd; text-align: right;'>₹{amt:N2}</td>
                        </tr>");
                }
            }

            sb.Append($@"
                    </tbody>
                    <tfoot>
                        <tr style='background-color: #f9f9f9; font-weight: bold;'>
                            <td style='padding: 8px; border: 1px solid #ddd; text-align: left;'>Total</td>
                            <td style='padding: 8px; border: 1px solid #ddd; text-align: right;'>{totalQuantity:N0}</td>
                            <td style='padding: 8px; border: 1px solid #ddd; text-align: right;'></td>
                            <td style='padding: 8px; border: 1px solid #ddd; text-align: right;'>₹{totalAmount:N2}</td>
                        </tr>
                    </tfoot>
                </table>

                <h3 style='color: #34495e; margin-top: 20px;'>💰 Payment Details</h3>
                <ul style='list-style-type: none; padding: 0;'>
                    <li style='padding: 4px 0;'><b>Total Amount:</b> ₹{sale.TotalAmount:N2}</li>
                    <li style='padding: 4px 0;'><b>Amount Paid:</b> ₹{sale.PaidAmount:N2}</li>
                    <li style='padding: 4px 0;'><b>Balance Due:</b> ₹{(sale.TotalAmount - sale.PaidAmount):N2}</li>
                    <li style='padding: 4px 0;'><b>Payment Status:</b> {(sale.PaymentStatus == "PARTIALLY_PAID" ? "⚠️ PARTIALLY PAID" : sale.PaymentStatus)}</li>
                </ul>

                <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0 15px;' />
                <p style='font-size: 12px; color: #7f8c8d; text-align: center;'>
                    <i>Thank you.<br><b>VPMS – Vinayaga Plates Management System</b></i>
                </p>
            </div>");

            return sb.ToString();
        }

        private string GenerateBeautifulOrderHtml(Order order, string actionDescription, string userName, string time)
        {
            var sb = new StringBuilder();
            sb.Append($@"
            <div style='font-family: Arial, sans-serif; padding: 20px; max-width: 600px; border: 1px solid #ddd; border-radius: 8px;'>
                <h2 style='color: #2c3e50; border-bottom: 2px solid #eee; padding-bottom: 10px;'>
                    Hello Partners 👋
                </h2>
                <p style='font-size: 16px; color: #2980b9;'><b>📋 {actionDescription.Replace("<b>", "").Replace("</b>", "")}</b></p>
                
                <h3 style='color: #34495e; margin-top: 20px;'>Order Information</h3>
                <ul style='list-style-type: none; padding: 0;'>
                    <li style='padding: 4px 0;'><b>Order ID:</b> {order.OrderId}</li>
                    <li style='padding: 4px 0;'><b>Order Number:</b> {order.OrderNo}</li>
                    <li style='padding: 4px 0;'><b>Customer:</b> {(order.Customer?.CustomerName ?? "Unknown")}</li>
                    <li style='padding: 4px 0;'><b>Date & Time:</b> {time}</li>
                    <li style='padding: 4px 0;'><b>Expected Date:</b> {order.ExpectedDate:dd-MMM-yyyy}</li>
                    <li style='padding: 4px 0;'><b>Recorded By:</b> {userName}</li>
                    <li style='padding: 4px 0;'><b>Status:</b> {order.Status}</li>
                </ul>

                <h3 style='color: #34495e; margin-top: 20px;'>📦 Order Details</h3>
                <table style='width: 100%; border-collapse: collapse; margin-bottom: 20px; font-size: 14px;'>
                    <thead>
                        <tr style='background-color: #f1f1f1;'>
                            <th style='padding: 8px; border: 1px solid #ddd; text-align: left;'>Plate Size</th>
                            <th style='padding: 8px; border: 1px solid #ddd; text-align: right;'>Quantity</th>
                            <th style='padding: 8px; border: 1px solid #ddd; text-align: right;'>Rate/Plate</th>
                            <th style='padding: 8px; border: 1px solid #ddd; text-align: right;'>Amount</th>
                        </tr>
                    </thead>
                    <tbody>");

            int totalQuantity = 0;
            decimal totalAmount = 0;
            if (order.Details != null)
            {
                foreach (var detail in order.Details)
                {
                    var pName = detail.Product?.ProductName ?? "Unknown";
                    var qty = detail.OrderedQuantity;
                    var rate = detail.SellingPrice;
                    var amt = qty * rate;
                    totalQuantity += qty;
                    totalAmount += amt;

                    sb.Append($@"
                        <tr>
                            <td style='padding: 8px; border: 1px solid #ddd; text-align: left;'>{pName}</td>
                            <td style='padding: 8px; border: 1px solid #ddd; text-align: right;'>{qty:N0}</td>
                            <td style='padding: 8px; border: 1px solid #ddd; text-align: right;'>₹{rate:N2}</td>
                            <td style='padding: 8px; border: 1px solid #ddd; text-align: right;'>₹{amt:N2}</td>
                        </tr>");
                }
            }

            sb.Append($@"
                    </tbody>
                    <tfoot>
                        <tr style='background-color: #f9f9f9; font-weight: bold;'>
                            <td style='padding: 8px; border: 1px solid #ddd; text-align: left;'>Total</td>
                            <td style='padding: 8px; border: 1px solid #ddd; text-align: right;'>{totalQuantity:N0}</td>
                            <td style='padding: 8px; border: 1px solid #ddd; text-align: right;'></td>
                            <td style='padding: 8px; border: 1px solid #ddd; text-align: right;'>₹{totalAmount:N2}</td>
                        </tr>
                    </tfoot>
                </table>

                <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0 15px;' />
                <p style='font-size: 12px; color: #7f8c8d; text-align: center;'>
                    <i>Thank you.<br><b>VPMS – Vinayaga Plates Management System</b></i>
                </p>
            </div>");

            return sb.ToString();
        }

        private string GenerateBeautifulPurchaseHtml(Purchase purchase, string actionDescription, string userName, string time)
        {
            var sb = new StringBuilder();
            sb.Append($@"
            <div style='font-family: Arial, sans-serif; padding: 20px; max-width: 600px; border: 1px solid #ddd; border-radius: 8px;'>
                <h2 style='color: #2c3e50; border-bottom: 2px solid #eee; padding-bottom: 10px;'>
                    Hello Partners 👋
                </h2>
                <p style='font-size: 16px; color: #e67e22;'><b>🏭 {actionDescription.Replace("<b>", "").Replace("</b>", "")}</b></p>
                
                <h3 style='color: #34495e; margin-top: 20px;'>Purchase Information</h3>
                <ul style='list-style-type: none; padding: 0;'>
                    <li style='padding: 4px 0;'><b>Purchase ID:</b> {purchase.PurchaseId}</li>
                    <li style='padding: 4px 0;'><b>Purchase No:</b> {purchase.PurchaseNumber}</li>
                    <li style='padding: 4px 0;'><b>Supplier:</b> {(purchase.Supplier?.SupplierName ?? "Unknown")}</li>
                    <li style='padding: 4px 0;'><b>Date & Time:</b> {time}</li>
                    <li style='padding: 4px 0;'><b>Recorded By:</b> {userName}</li>
                    <li style='padding: 4px 0;'><b>Status:</b> {(purchase.Status == "COMPLETED" ? "✅ COMPLETED" : purchase.Status)}</li>
                </ul>

                <h3 style='color: #34495e; margin-top: 20px;'>📦 Purchase Details</h3>
                <table style='width: 100%; border-collapse: collapse; margin-bottom: 20px; font-size: 14px;'>
                    <thead>
                        <tr style='background-color: #f1f1f1;'>
                            <th style='padding: 8px; border: 1px solid #ddd; text-align: left;'>Product Name</th>
                            <th style='padding: 8px; border: 1px solid #ddd; text-align: right;'>Quantity</th>
                            <th style='padding: 8px; border: 1px solid #ddd; text-align: right;'>Unit Cost</th>
                            <th style='padding: 8px; border: 1px solid #ddd; text-align: right;'>Amount</th>
                        </tr>
                    </thead>
                    <tbody>");

            int totalQuantity = 0;
            decimal totalAmount = 0;
            if (purchase.Details != null)
            {
                foreach (var detail in purchase.Details)
                {
                    var pName = detail.Product?.ProductName ?? "Unknown";
                    var qty = detail.Quantity;
                    var cost = detail.UnitCost;
                    var amt = qty * cost;
                    totalQuantity += qty;
                    totalAmount += amt;

                    sb.Append($@"
                        <tr>
                            <td style='padding: 8px; border: 1px solid #ddd; text-align: left;'>{pName}</td>
                            <td style='padding: 8px; border: 1px solid #ddd; text-align: right;'>{qty:N0}</td>
                            <td style='padding: 8px; border: 1px solid #ddd; text-align: right;'>₹{cost:N2}</td>
                            <td style='padding: 8px; border: 1px solid #ddd; text-align: right;'>₹{amt:N2}</td>
                        </tr>");
                }
            }

            sb.Append($@"
                    </tbody>
                    <tfoot>
                        <tr style='background-color: #f9f9f9; font-weight: bold;'>
                            <td style='padding: 8px; border: 1px solid #ddd; text-align: left;'>Total</td>
                            <td style='padding: 8px; border: 1px solid #ddd; text-align: right;'>{totalQuantity:N0}</td>
                            <td style='padding: 8px; border: 1px solid #ddd; text-align: right;'></td>
                            <td style='padding: 8px; border: 1px solid #ddd; text-align: right;'>₹{totalAmount:N2}</td>
                        </tr>
                    </tfoot>
                </table>

                <h3 style='color: #34495e; margin-top: 20px;'>💰 Payment Details</h3>
                <ul style='list-style-type: none; padding: 0;'>
                    <li style='padding: 4px 0;'><b>Total Amount:</b> ₹{purchase.TotalAmount:N2}</li>
                    <li style='padding: 4px 0;'><b>Amount Paid:</b> ₹{purchase.PaidAmount:N2}</li>
                    <li style='padding: 4px 0;'><b>Balance Due:</b> ₹{(purchase.TotalAmount - purchase.PaidAmount):N2}</li>
                    <li style='padding: 4px 0;'><b>Payment Status:</b> {(purchase.PaymentStatus == "PARTIALLY_PAID" ? "⚠️ PARTIALLY PAID" : purchase.PaymentStatus)}</li>
                </ul>

                <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0 15px;' />
                <p style='font-size: 12px; color: #7f8c8d; text-align: center;'>
                    <i>Thank you.<br><b>VPMS – Vinayaga Plates Management System</b></i>
                </p>
            </div>");

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
