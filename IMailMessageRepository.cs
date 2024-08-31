using System.Collections.Generic;

namespace SuperMarketRepository.EmailLibrary
{
    public interface IMailMessageRepository
    {
        int DeleteMailMessage();
        List<MailMessageDetails> GetMailMessages();
        void InsertMailMessage(MailMessageDetails message);
        int UpdateMailMessage(MailMessageDetails message);
    }
}