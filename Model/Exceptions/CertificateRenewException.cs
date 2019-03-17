using System;
using System.Security.Principal;

namespace ManagementPortal.Model
{

    public class CertificateRenewException : Exception
    {
        public AuditType AuditType { get; set; }
        public StackType? Stack { get; set; }
        public CertificateRenewException() : base()
		{

		}


        public CertificateRenewException(string massage, AuditType auditType, StackType? stack) : base(massage)
        {
            AuditType = auditType;
            Stack = stack;
        }

        public CertificateRenewException(string massage, AuditType auditType, StackType? stack, Exception innerException) : base(massage, innerException)
        {
            AuditType = auditType;
            Stack = stack;
        }
        
    }
}
