using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            var senderEmail = "pandimsd1603@gmail.com";
            var senderPassword = "aaxy axmm hmhl lbyh";
            var toEmail = "pandimsd1603@gmail.com"; // sending to self for testing

            Console.WriteLine("Connecting to SMTP...");
            using var client = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential(senderEmail, senderPassword),
                EnableSsl = true,
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail, "VPMS System Test"),
                Subject = "Test Email Localhost",
                Body = "If you see this, the SMTP is working perfectly.",
                IsBodyHtml = true,
            };

            mailMessage.To.Add(toEmail);

            Console.WriteLine("Sending email...");
            await client.SendMailAsync(mailMessage);
            Console.WriteLine("Email sent successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to send email.");
            Console.WriteLine("Error: " + ex.Message);
            if (ex.InnerException != null)
            {
                Console.WriteLine("Inner Error: " + ex.InnerException.Message);
            }
        }
    }
}
