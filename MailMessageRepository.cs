using System;
using System.Collections.Generic;
using System.Net.Mail;
using Microsoft.Data.Sqlite;
namespace SuperMarketRepository.EmailLibrary
{
    public class SQLiteMailMessageRepository : IMailMessageRepository
    {
        private readonly string _connectionString;

        public SQLiteMailMessageRepository(string connectionString)
        {
            _connectionString = connectionString;
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = @"
                CREATE TABLE IF NOT EXISTS MailMessages (
                    _msgid INTEGER PRIMARY KEY ,
                    EmailFrom TEXT,
                    EmailTo TEXT,
                    EmailCC TEXT,
                    EmailBCC TEXT,
                    EmailSubject TEXT,
                    EmailBody TEXT,
                    MessageIsHTML INTEGER,
                    AttachFilePath TEXT,
                    State INTEGER,
                    Retries INTEGER,
                    CreatedDate TEXT,
                    LastUpdatedDate TEXT,
                    ResponseMessage TEXT,
                    MaxRetries INTERGER
                )";
                command.ExecuteNonQuery();
            }
        }
        private int AutoIncrement()
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                    var command = connection.CreateCommand();
                    command.CommandText = @"select IFNULL( MAX(mm._msgid),0)+1 from MailMessages mm";
                    var obj = command.ExecuteScalar();
                    return Convert.ToInt32(obj);
                }
            }
            catch (Exception)
            {

                throw;
            }
        }

        public void InsertMailMessage(MailMessageDetails message)
        {
            try
            {
                int id = AutoIncrement();
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                    var command = connection.CreateCommand();
                    command.CommandText = @"
                INSERT INTO MailMessages (_msgid,EmailFrom, EmailTo,EmailCC,EmailBCC, EmailSubject, EmailBody, MessageIsHTML, AttachFilePath, State, Retries, CreatedDate, LastUpdatedDate, ResponseMessage,MaxRetries)
                                VALUES (@msgid,@EmailFrom, @EmailTo,@EmailCC,@EmailBCC, @EmailSubject, @EmailBody, @MessageIsHTML, @AttachFilePath, @State, @Retries, @CreatedDate, @LastUpdatedDate, @ResponseMessage,@MaxRetries);";
                    // SELECT last_insert_rowid();";

                    command.Parameters.AddWithValue("@msgid", id);
                    command.Parameters.AddWithValue("@EmailFrom", message.EmailFrom);
                    command.Parameters.AddWithValue("@EmailTo", message.EmailTo);
                    command.Parameters.AddWithValue("@EmailCC", message.EmailCC);
                    command.Parameters.AddWithValue("@EmailBCC", message.EmailBCC ?? "");
                    command.Parameters.AddWithValue("@EmailSubject", message.EmailSubject);
                    command.Parameters.AddWithValue("@EmailBody", message.EmailBody);
                    command.Parameters.AddWithValue("@MessageIsHTML", message.MessageIsHTML ? 1 : 0);
                    command.Parameters.AddWithValue("@AttachFilePath", message.AttachFilePath ?? "");
                    command.Parameters.AddWithValue("@State", (int)message.State);
                    command.Parameters.AddWithValue("@Retries", message.Retries);
                    command.Parameters.AddWithValue("@CreatedDate", DateTime.UtcNow.ToString("o"));
                    command.Parameters.AddWithValue("@LastUpdatedDate", DateTime.UtcNow.ToString("o"));
                    command.Parameters.AddWithValue("@ResponseMessage", message.ResponseMessage ?? "");
                    command.Parameters.AddWithValue("@MaxRetries", message.MaxRetries);
                    try
                    {
                        var ret = command.ExecuteNonQuery();
                        if (ret > 0)
                        {
                            // Optionally retrieve the last inserted row ID
                            command.CommandText = "SELECT last_insert_rowid();";
                            var obj = command.ExecuteScalar();
                            message._msgid = Convert.ToInt32(obj);
                        }
                        else
                        {
                            // Handle the case where no rows were inserted
                            Console.WriteLine("No rows were inserted.");
                            LogMail.LogMessage($"Error Occured : No Rows were Inserted");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log or handle the exception
                        Console.WriteLine($"An error occurred: {ex.Message}");
                        LogMail.LogMessage($"Error Occured : {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogMail.LogMessage($"Error Occured : {ex.Message}");
                // throw;
            }
        }

        public int UpdateMailMessage(MailMessageDetails message)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                    var command = connection.CreateCommand();
                    command.CommandText = @"
                UPDATE MailMessages
                SET 
                    State = @State,
                    Retries = Retries + @Retries,
                    LastUpdatedDate = @LastUpdatedDate,
                    ResponseMessage = @ResponseMessage
                WHERE _msgid = @MsgId;";

                    command.Parameters.AddWithValue("@State", (int)message.State);
                    command.Parameters.AddWithValue("@Retries", message.Retries);
                    command.Parameters.AddWithValue("@LastUpdatedDate", DateTime.UtcNow.ToString("o"));
                    command.Parameters.AddWithValue("@ResponseMessage", message.ResponseMessage ?? "");
                    command.Parameters.AddWithValue("@MsgId", message._msgid);

                    int ret = command.ExecuteNonQuery();
                    return ret;
                }
            }
            catch (Exception ex)
            {

                LogMail.LogMessage($"Error Occured : {ex.Message}");
            }
            return -1;
        }



        public int DeleteMailMessage()
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                    var command = connection.CreateCommand();
                    command.CommandText = @"
                truncate table  MailMessages";



                    int ret = command.ExecuteNonQuery();
                    return ret;
                }
            }
            catch (Exception ex)
            {

                LogMail.LogMessage($"Error Occured : {ex.Message}");
            }
            return -1;
        }

        public List<MailMessageDetails> GetMailMessages()
        {
            try
            {
                var mailMessages = new List<MailMessageDetails>();
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                    var command = connection.CreateCommand();
                    command.CommandText = "Select * from MailMessages";
                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var mailMessage = new MailMessageDetails
                        {
                            _msgid = reader.GetInt32(0), // Assuming _msgid is the first column
                            EmailFrom = reader.GetString(1),
                            EmailTo = reader.GetString(2),
                            EmailCC = reader.IsDBNull(3) ? null : reader.GetString(3),
                            EmailBCC = reader.IsDBNull(4) ? null : reader.GetString(4),
                            EmailSubject = reader.GetString(5),
                            EmailBody = reader.GetString(6),
                            MessageIsHTML = reader.GetBoolean(7),
                            AttachFilePath = reader.IsDBNull(8) ? null : reader.GetString(8),
                            State = (MailMessageStateEnum)reader.GetInt32(9), // Assuming State is an enum stored as int
                            Retries = reader.GetInt32(10),
                            CreatedDate = reader.GetString(11),
                            LastUpdatedDate = reader.GetString(12),
                            ResponseMessage = reader.IsDBNull(13) ? null : reader.GetString(13)
                        };

                        mailMessages.Add(mailMessage);
                    }
                    return mailMessages;
                }
            }
            catch (Exception ex)
            {

                LogMail.LogMessage($"Error Occured : {ex.Message}");
            }
            return null;
        }
    }

}
