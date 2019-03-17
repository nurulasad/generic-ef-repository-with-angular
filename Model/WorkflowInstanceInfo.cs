using ManagementPortal.Model.Id;
using System;
using System.Collections.Generic;

namespace ManagementPortal.Model
{

    public class WorkflowInstanceInfo : IInfoObject
    {
        public WorkflowInstanceId Id { get; set; }
        public WorkflowId WorkflowId { get; set; }
        public string Name { get; set; }
        public WorkflowState State { get; set; }

        public CertificateId CertificateId { get; set; }

        public AccountId AccountId { get; set; }

        public List<WorkflowInstanceItemInfo> InstanceItems {get;set;}
        public DateTime Created { get; set; }
        public string CreatedBy { get; set; }
    
        public DateTime Updated { get; set; }
        public string UpdatedBy { get; set; }
        public WorkflowInstanceInfo(WorkflowInstanceId id, WorkflowId workflowId, string name, WorkflowState state, AccountId accountId, DateTime created, string createdBy, DateTime updated, string updatedBy)
        {
            Id = id;
            WorkflowId = workflowId;
            Name = name;
            State = state;
            
            AccountId = accountId;
            Created = created;
            CreatedBy = createdBy;
            Updated = updated;
            UpdatedBy = updatedBy;
        }
    }
}
