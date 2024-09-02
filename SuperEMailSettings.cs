using System;
using MailKit;
using MimeKit;

using System.Linq;
using System.Threading.Tasks;
using System.IO;
using MailKit.Net.Smtp;
using System.Net.Mail;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;
using System.Security.Authentication;
using Hangfire;
using Newtonsoft.Json;
using System.Collections.Generic;
namespace SuperMarketRepository.EmailLibrary
{
    public class SuperEMailSettings
    {
        public string SmtpServer { get; set; }
        public int SmtpPort { get; set; }
        public string SmtpUsername { get; set; }
        public string SmtpPassword { get; set; }
        public bool TLS { get; set; } = false;
        public bool LogSMTP { get; set; } = false;
        public bool UseSmtpUserasFromMail { get; set; } = false;
        public string FromMail { get; set; } = string.Empty;

        public bool UseInbuiltDataStore { get; set; } = true;
        public IMailMessageRepository MailMessageRepository { get; set; }

        public MailKit.Security.SecureSocketOptions SecureSocketOptions { get; set; }
    }

    public class MailMessageDetails
    {
        public int _msgid { get; set; } // Add this property for serialization
       
        public string EmailFrom { get; set; }
        public string EmailTo { get; set; }

        public string EmailCC { get; set; }
        public string EmailBCC { get; set; }
        public string EmailSubject { get; set; }

        public string EmailBody { get; set; }
        public bool MessageIsHTML { get; set; } = false;

        public string AttachFilePath { get; set; }

        public MailMessageStateEnum State { get;  set; }
        public int Retries { get; set; } = 0;

        public string CreatedDate { get; set; }
        public string LastUpdatedDate { get; set; }

        public string ResponseMessage {  get; set; }

        public int MaxRetries { get; set; }
        public string FromAlias { get; set; } = "";


    }

   
    public enum MailMessageStateEnum
    {
        Queued,
        Sent,
        Failed,
        Retring,
        Sending
    }
    
    public class MailDatastoreOperations : IDisposable
    {
        
        private readonly SuperEMailSettings smtpSettings;
        private readonly IBackgroundJobClient _backgroundJobClient;
        public event EventHandler<MailMessageDetails> MessageReceived;
        private IMailMessageRepository dbrepo;
        private string emailfrom {  get; set; }
        private string dbpath = Path.Combine( AppDomain.CurrentDomain.BaseDirectory , "supermailstore.db");
        public virtual void OnMessageReceived(MailMessageDetails msg)
        {
            MessageReceived?.Invoke(this, msg);
        }
        public static MailDatastoreOperations NewDataStore(SuperEMailSettings eMailSettings)
        {
            return new MailDatastoreOperations(eMailSettings);
        }
        public MailDatastoreOperations()
        {
            dbrepo = new SQLiteMailMessageRepository(dbpath);
        }
        public MailDatastoreOperations(SuperEMailSettings eMailSettings, IBackgroundJobClient backgroundJobClient)
        {
            smtpSettings = eMailSettings;
            _backgroundJobClient = backgroundJobClient;
            if (smtpSettings.MailMessageRepository == null)
            {
                dbrepo = new SQLiteMailMessageRepository($"Data Source={dbpath}");
            }
            else
            {
                dbrepo=smtpSettings.MailMessageRepository;
            }
            if (smtpSettings.UseSmtpUserasFromMail == true)
            {
                this.emailfrom = smtpSettings.FromMail;
            }
            //dbrepo = _dbrepo;
        }

        public MailDatastoreOperations(IBackgroundJobClient backgroundJobClient)
        {
            _backgroundJobClient = backgroundJobClient;
        }
        public MailDatastoreOperations(SuperEMailSettings settings )
        {

            smtpSettings = settings;
            if (smtpSettings.MailMessageRepository == null)
            {
                dbrepo = new SQLiteMailMessageRepository($"Data Source={dbpath}");
            }
            else
            {
                dbrepo = smtpSettings.MailMessageRepository;
            }
            if (smtpSettings.UseSmtpUserasFromMail == true)
            {
                this.emailfrom = smtpSettings.FromMail;
            }
        }

        public void Insert(MailMessageDetails msg)
        {
            msg.EmailFrom = this.emailfrom;
          if (!EmailValidator.ValidateMessage(msg))
            {
                msg.ResponseMessage = "Invalid Message format";
                OnMessageReceived(msg);
                return;
            }
            dbrepo.InsertMailMessage(msg);
            if (msg._msgid > 0)
            {
                OnMessageReceived(msg);
                _backgroundJobClient.Enqueue(() => Mail.NewMail(smtpSettings).SendMailAsync(msg));
            }
         
        }

        

        public void Update(MailMessageDetails msg)
        {
            msg.EmailFrom = this.emailfrom;
          int ret=  dbrepo.UpdateMailMessage(msg);
            if (ret > 0)
            {
                OnMessageReceived(msg);
                LogMail.LogMessage($" Updated with status {msg.ResponseMessage}");
            }

         
        }

        public int Purge()
        {

            return dbrepo.DeleteMailMessage();

        }

        public string SelectAll()
        {
            var ret= dbrepo.GetMailMessages();
            return JsonConvert.SerializeObject(ret);

        }

        

        public async Task CheckUnsentEmails()
        {
            var lstmails= dbrepo.GetMailMessages().Where(x => x.State != MailMessageStateEnum.Sent && x.Retries>0);
            int count = 0;
            foreach (var mail in lstmails) { 
                if (mail.MaxRetries > count)
                {
                    count++;
                    mail.Retries =count;
                    await Mail.NewMail(smtpSettings).SendMailAsync(mail);
                    
                    
                }
                
                
            }

        }


        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~DatastoreOperations()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        void IDisposable.Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

        

    public interface IMail
    {
        Task  SendMailAsync(MailMessageDetails mail);
        //Task SendMailAsync (MailMessageDetails mail);

    }


    public class Mail : IMail, IDisposable
    {
        private readonly SuperEMailSettings _settings;

        #region factoryregion
        public static Mail NewMail(SuperEMailSettings superEMailSettings)
        {
            return new Mail(superEMailSettings);
        } 
        #endregion


        public Mail(SuperEMailSettings settings)
        {
            _settings = settings;
        }

       

        public async Task SendMailAsync(MailMessageDetails mail)
        {
            
            if (!EmailValidator.ValidateMessage(mail))
            {
                string responsemsg = "Validation Failed Please check From,To mail address, body and subject.";
                LogMail.LogMessage(responsemsg);
                UpdateMailState(mail, MailMessageStateEnum.Failed, responsemsg);
                
                return;
            }
            try
            {
                var msgstatus = MailMessageStateEnum.Queued;
                var message = new MimeMessage();
                var tomails = mail.EmailTo.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                
                
                message.From.Add(new MailboxAddress(mail.FromAlias, mail.EmailFrom));
                
                //message.To.Add(new MailboxAddress("", mail.EmailTo));
                var ToEmail=tomails.Select(email =>  InternetAddress.Parse(email));
                
                message.To.AddRange(ToEmail);
                if (mail.EmailCC != null) {
                    var ccmails = mail.EmailCC.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                    var CCEmail = ccmails.Select(email => InternetAddress.Parse(email));
                    message.Cc.AddRange(CCEmail);
                }
                message.Subject = mail.EmailSubject;
                var builder = new BodyBuilder();
                if (mail.MessageIsHTML)
                {
                    builder.HtmlBody = mail.EmailBody;
                }
                else
                {
                    builder.TextBody = mail.EmailBody;
                }

                if (!string.IsNullOrWhiteSpace(mail.AttachFilePath))
                {
                    builder.Attachments.Add(mail.AttachFilePath);

                }
                message.Body = builder.ToMessageBody();
                using (SmtpClient client = new SmtpClient())
                {
                    string response = string.Empty;
                    try
                    {
                        if (_settings.TLS)
                        {
                            client.SslProtocols = SslProtocols.Tls12;
                        }

                        await client.ConnectAsync(_settings.SmtpServer, _settings.SmtpPort, _settings.SecureSocketOptions);
                        await client.AuthenticateAsync(_settings.SmtpUsername, _settings.SmtpPassword);
                        await client.SendAsync(message);
                        response = "Mail Sent Successfully";
                        msgstatus =MailMessageStateEnum.Sent;

                    }
                    catch (SmtpProtocolException spex)
                    {

                        response = spex.ToString();
                        msgstatus = MailMessageStateEnum.Failed;
                    }
                    catch (SmtpCommandException sce)
                    {
                        response = sce.ToString();
                        msgstatus = MailMessageStateEnum.Failed;
                    }
                    catch (SmtpFailedRecipientException sre)
                    {
                        response = sre.ToString();
                        msgstatus = MailMessageStateEnum.Failed;
                    }
                    catch (SmtpException se)
                    {
                        response = se.ToString();
                        msgstatus = MailMessageStateEnum.Failed;
                    }
                    catch (Exception ex)
                    {

                        response = ex.ToString();
                        msgstatus = MailMessageStateEnum.Failed;
                    }
       
                    LogMail.LogMessage(response);
                    UpdateMailState(mail, msgstatus, response);
                    
                }

            }
            catch (Exception ex)
            {
       
                LogMail.LogMessage(ex.ToString());
            }


        }
        private void UpdateMailState(MailMessageDetails mail, MailMessageStateEnum state, string responseMessage)
        {
            mail.State = state;
            mail.ResponseMessage = responseMessage;
            mail.LastUpdatedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            try
            {
                using (var dso = new MailDatastoreOperations(_settings))
                dso.Update(mail);

                // Notify clients via SignalR
               // _hubContext.Clients.All.SendAsync("ReceiveMailUpdate", mail);
            }
            catch (Exception ex)
            {
                LogMail.LogMessage($"Failed to update mail status: {ex.Message}");
            }
        }




       



        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~Mail()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        void IDisposable.Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

       
    }

    public static class LogMail
    {
        private static readonly object _lock = new object();
        public static void LogMessage(string message)
        {
            string logFileName = $"maillog_{DateTime.Now:yyyy-MM-dd}.txt"; // e.g., log_2023-10-01.txt
            string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, logFileName);

            lock (_lock) // Ensure thread safety
            {
                using (StreamWriter writer = new StreamWriter(logFilePath, true))
                {
                    writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: {message}");
                }
            }
        }
    }


public class EmailValidator
    {
        public static bool ValidateMessage(MailMessageDetails msg)
        {
            if (msg == null)
            {
                return false;
            }

            if (!EmailValidator.ValidateEmailAddresses(msg.EmailFrom))
            { return false; }
            if (!EmailValidator.ValidateEmailAddresses(msg.EmailTo))
            { return false; }
            if (!string.IsNullOrWhiteSpace(msg.EmailCC))
            {

                if (!EmailValidator.ValidateEmailAddresses(msg.EmailCC))
                { return false; }
            }
        

            if (string.IsNullOrWhiteSpace(msg.EmailBody))
                return false;
            if (string.IsNullOrWhiteSpace(msg.EmailSubject))
                return false;



            return true;
        }
        public static bool ValidateEmailAddresses(string emailAddresses)
        {
            if (string.IsNullOrWhiteSpace(emailAddresses))
            {
                return false; // Empty or null input is not valid
            }

            // Split the input string by commas
            var emails = emailAddresses.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var validEmails = new List<string>();

            foreach (var email in emails)
            {
                // Trim whitespace and validate each email
                var trimmedEmail = email.Trim();
                if (IsValidEmail(trimmedEmail))
                {
                    validEmails.Add(trimmedEmail);
                }
                else
                {
                    return false; // If any email is invalid, return false
                }
            }

            // Optionally, you can return the list of valid emails or just true
            return true;
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var mailAddress = new MailAddress(email);
                return true; // If no exception is thrown, the email is valid
            }
            catch (FormatException)
            {
                return false; // Invalid email format
            }
        }
    }

    class Program
    {
        static void Main()
        {
            string input = "test@example.com, valid.email@domain.com, invalid-email@";
            bool isValid = EmailValidator.ValidateEmailAddresses(input);
            Console.WriteLine($"Are all email addresses valid? {isValid}");
        }
    }


}
