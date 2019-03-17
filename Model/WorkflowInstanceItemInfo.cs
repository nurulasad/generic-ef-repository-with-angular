using ManagementPortal.Model.Id;
using System;

namespace ManagementPortal.Model
{

    public class WorkflowInstanceItemInfo : IInfoObject
    {
        public WorkflowInstanceItemId Id { get; set; }
        public WorkflowInstanceId WorkflowInstanceId { get; set; }
        public WorkflowItemId WorkflowItemId { get; set; }
        public string Name { get; set; }
        public WorkflowState State { get; set; }

        public CertificateOrderId CertificateOrderId { get; set; }

        public AccountId AccountId { get; set; }
        public DateTime Created { get; set; }
        public string CreatedBy { get; set; }
    
        public DateTime Updated { get; set; }
        public string UpdatedBy { get; set; }
        public WorkflowInstanceItemInfo(WorkflowInstanceItemId id, WorkflowInstanceId workflowInstanceId, WorkflowItemId workflowItemId, string name, WorkflowState state, AccountId accountId, DateTime created, UserId createdBy, DateTime updated, UserId updatedBy)
        {
            Id = id;
            WorkflowInstanceId = workflowInstanceId;
            WorkflowItemId = workflowItemId;
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
