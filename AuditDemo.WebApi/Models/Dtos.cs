using System;
using System.Collections.Generic;

namespace AuditDemo.WebApi.Models
{
    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class LoginResponse
    {
        public string Token { get; set; }
        public Guid UserId { get; set; }
        public int Role { get; set; }
        public string RoleName { get; set; }
    }

    public class CreateAuditRequest
    {
        public int AuditType { get; set; } // 1 Annual, 2 New
        public int Year { get; set; }
        public Guid FactoryId { get; set; }
        public bool CopyLastYear { get; set; }
    }

    public class AssignModulesRequest
    {
        public List<ModuleAssignmentDto> Assignments { get; set; }
    }

    public class ModuleAssignmentDto
    {
        public Guid ModuleId { get; set; }
        public Guid? OwnerUserId { get; set; }
    }

    public class SaveClauseRequest
    {
        public int Status { get; set; } // 0..4
        public string Comment { get; set; }
    }

    public class UpdateCapaRequest
    {
        public string CorrectiveAction { get; set; }
        public string ExternalOwnerName { get; set; }
        public string ExternalOwnerPhone { get; set; }
        public DateTime? DueDate { get; set; }
        public string ReviewConclusion { get; set; }
    }

    public class CloseCapaRequest
    {
        public string ReviewConclusion { get; set; }
    }

    public class CreateReAuditRequest
    {
        public Guid FromAuditId { get; set; }
    }

    public class SaveReAuditClauseRequest
    {
        public int Status { get; set; } // 0..4
        public string Comment { get; set; }
    }

    public class CloseReAuditRequest
    {
        public string CloseConclusion { get; set; }
    }


    public class CreateCertificateRequest
    {
        public Guid FactoryId { get; set; }
        public string CertName { get; set; }
        public string CertNo { get; set; }
        public string CertType { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? ExpireDate { get; set; }
        public string Remark { get; set; }
    }

    public class UpdateCertificateRequest
    {
        public string CertName { get; set; }
        public string CertNo { get; set; }
        public string CertType { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? ExpireDate { get; set; }
        public string Remark { get; set; }
        public bool? IsActive { get; set; }
    }
}
