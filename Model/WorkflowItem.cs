using ManagementPortal.Model.Id;
using System;

namespace ManagementPortal.Model
{

    public class WorkflowItemInfo : IInfoObject
    {
        public WorkflowItemId Id { get; set; }
        public WorkflowId WorkflowId { get; set; }
        public int SequenceNo { get; set; }
        
        public AccountId AccountId { get; set; }
        public DateTime Created { get; set; }
        public string CreatedBy { get; set; }
    
        public DateTime Updated { get; set; }
        public string UpdatedBy { get; set; }
        public WorkflowItemInfo(WorkflowItemId id, WorkflowId workflowId, int sequenceNo, AccountId accountId, DateTime created, UserId createdBy, DateTime updated, UserId updatedBy)
        {
            Id = id;
            WorkflowId = workflowId;
            SequenceNo = sequenceNo;
            
            AccountId = accountId;
            Created = created;
            CreatedBy = createdBy;
            Updated = updated;
            UpdatedBy = updatedBy;
        }
    }
}
