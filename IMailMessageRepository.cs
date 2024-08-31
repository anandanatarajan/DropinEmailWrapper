using System.Collections.Generic;

namespace EmailLibrary
{
    public interface IMailMessageRepository
    {
        int DeleteMailMessage();
        List<MailMessageDetails> GetMailMessages();
        void InsertMailMessage(MailMessageDetails message);
        int UpdateMailMessage(MailMessageDetails message);
    }
}