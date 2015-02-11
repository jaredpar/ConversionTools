using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using OneWayMirror.Core;

namespace OneWayMirror
{
    internal sealed class ReportingConsoleHost : ConsoleHost
    {
        private readonly string _reportEmailAddress;

        internal ReportingConsoleHost(bool verbose, string reportEmailAddress) : base(verbose)
        {
            _reportEmailAddress = reportEmailAddress;
        }

        private void SendMail(string body)
        {
            using (var msg = new MailMessage())
            {
                msg.To.Add(new MailAddress(_reportEmailAddress));
                msg.From = new MailAddress(Environment.UserDomainName + @"@microsoft.com");
                msg.IsBodyHtml = false;
                msg.Subject = "Git to TFS Mirror Error";
                msg.Body = body;

                var client = new SmtpClient("smtphost");
                client.UseDefaultCredentials = true;
                client.Send(msg);
            }
        }

        public override void Error(string format, params object[] args)
        {
            base.Error(format, args);
            SendMail(string.Format(format, args));
        }
    }
}
