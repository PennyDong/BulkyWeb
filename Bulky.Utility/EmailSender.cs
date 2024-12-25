using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using RestSharp;


namespace Bulky.Utility
{
    public class EmailSender : IEmailSender
    {
        public string ElasticSecret { get; set; }
        public string FromSender { get; set; }
        private const string ApiUrl = "https://api.elasticemail.com/v2/email/send";
        public EmailSender(IConfiguration _config)
        {
            ElasticSecret = _config.GetValue<string>("Elastic:SecretKey");
            FromSender = _config.GetValue<string>("Elastic:Sender");
        }
        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            //login to send email

            var client = new RestClient(ApiUrl);

            var request = new RestRequest
            {
                Method = Method.Post // Use Method.Post directly as a property
            };
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");

            request.AddParameter("apikey", ElasticSecret); // API Key for authentication
            request.AddParameter("from", FromSender); // 傳送信箱
            request.AddParameter("to", email); // 接收者信箱
            request.AddParameter("subject", subject); // 郵件主題
            request.AddParameter("bodyHtml", htmlMessage); //郵件內容


            try
            {
                // Execute the request asynchronously
                var response = await client.ExecuteAsync(request);

                // Check if the response was successful
                if (response.IsSuccessful)
                {
                    Console.WriteLine("Email sent successfully.");
                }
                else
                {
                    // Log or handle failure response
                    Console.WriteLine($"Failed to send email. Response: {response.Content}");
                }
            }
            catch (Exception ex)
            {
                // Handle any exceptions that occur during the request
                Console.WriteLine($"Error occurred while sending email: {ex.Message}");
            }
        }
    }
}
