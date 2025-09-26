using System.Collections.Generic;

namespace SuperMarketRepository.EmailLibrary
{
    /// <summary>
    /// Mail Message Repository Interface
    /// </summary>
    public interface IMailMessageRepository
    {
        int DeleteMailMessage();
        List<MailMessageDetails> GetMailMessages();
        void InsertMailMessage(MailMessageDetails message);
        int UpdateMailMessage(MailMessageDetails message);
    }
}