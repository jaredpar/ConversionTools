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
        private readonly string _gitRemoteName;
        private readonly string _gitBranchName;

        internal ReportingConsoleHost(bool verbose, string reportEmailAddress, string gitRemoteName, string gitBranchName) : base(verbose)
        {
            _reportEmailAddress = reportEmailAddress;
            _gitRemoteName = gitRemoteName;
            _gitBranchName = gitBranchName;
        }

        private void SendMail(string body)
        {
            using (var msg = new MailMessage())
            {
                msg.To.Add(new MailAddress(_reportEmailAddress));
                msg.From = new MailAddress(Environment.UserName + @"@microsoft.com");
                msg.IsBodyHtml = false;
                msg.Subject = string.Format("Git to TFS Mirror Error ({0}/{1})", _gitRemoteName, _gitBranchName);
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
