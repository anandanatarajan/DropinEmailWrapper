using System;

using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Hangfire;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;
namespace SuperMarketRepository.EmailLibrary
{
    /// <summary>
    /// Provides operations for managing and processing email messages in a datastore.
    /// </summary>
    /// <remarks>This class supports inserting, updating, purging, and retrieving email messages, as well as
    /// sending unsent emails. It also raises the <see cref="MessageReceived"/> event when an email message is
    /// processed. Instances of this class can be created using the parameterized constructors or the <see
    /// cref="NewDataStore(SuperEMailSettings)"/> factory method.</remarks>
    public class MailDatastoreOperations : IDisposable
    {

        private readonly SuperEMailSettings smtpSettings;
        private readonly IBackgroundJobClient _backgroundJobClient;
        public event EventHandler<MailMessageDetails> MessageReceived;
        private readonly IMailMessageRepository dbrepo;
        
        private string emailfrom { get; set; }
        private readonly string dbpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "supermailstore.db");
        private bool disposedValue;

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
                dbrepo = smtpSettings.MailMessageRepository;
            }
            if (smtpSettings.UseSmtpUserasFromMail )
            {
                this.emailfrom = smtpSettings.FromMail;
            }
            //dbrepo = _dbrepo;
        }

        public MailDatastoreOperations(IBackgroundJobClient backgroundJobClient)
        {
            _backgroundJobClient = backgroundJobClient;
        }
        public MailDatastoreOperations(SuperEMailSettings settings)
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
            if (smtpSettings.UseSmtpUserasFromMail )
            {
                this.emailfrom = smtpSettings.FromMail;
            }
        }

        public static MailMessageDetails Create(string to, string subject, string body, string cc = null, bool isHtml = false)
        {
            return new MailMessageDetails
            {
                EmailTo = to,
                EmailCC = cc,
                EmailSubject = subject,
                EmailBody = body,
                MessageIsHTML = isHtml,
                CreatedDate = DateTime.UtcNow.ToString("o"),
               
            };
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
            int ret = dbrepo.UpdateMailMessage(msg);
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
            var ret = dbrepo.GetMailMessages();
            return JsonConvert.SerializeObject(ret);

        }



        public async Task CheckUnsentEmails()
        {
            var lstmails = dbrepo.GetMailMessages().Where(x => x.State != MailMessageStateEnum.Sent && x.Retries > 0);
            int count = 0;
            foreach (var mail in lstmails)
            {
                if (mail.MaxRetries > count)
                {
                    count++;
                    mail.Retries = count;
                    await Mail.NewMail(smtpSettings).SendMailAsync(mail);


                }


            }

        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (dbrepo is IDisposable disposableRepo)
                    {
                        disposableRepo.Dispose();
                    }
                    if (_backgroundJobClient is IDisposable disposableBackgroundJobClient)
                    {
                        disposableBackgroundJobClient.Dispose();
                    }

                }

              
                disposedValue = true;
            }
        }

      

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }


}
