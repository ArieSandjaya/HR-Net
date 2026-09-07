namespace HRMS.Domain.Enums;

public enum Gender
{
    Male,
    Female,
    Other,
    PreferNotToSay
}

public enum MaritalStatus
{
    Single,
    Married,
    Divorced,
    Widowed
}

public enum EmployeeStatus
{
    Active,
    Inactive,
    Suspended,
    LeftOrganization
}

public enum LeaveAllocationType
{
    AllocatedManually,
    EarnedLeave,
    CompensatoryLeave
}

public enum AttendanceStatus
{
    Present,
    Absent,
    OnLeave,
    HalfDay,
    WorkFromHome,
    Holiday
}

public enum LeaveApplicationStatus
{
    Open,
    Approved,
    Rejected,
    Cancelled
}

public enum ShiftAssignmentStatus
{
    Active,
    Inactive
}

public enum JobOpeningStatus
{
    Open,
    OnHold,
    Closed
}

public enum JobApplicantStatus
{
    Open,
    InterviewScheduled,
    Rejected,
    OnHold,
    Accepted
}

public enum InterviewStatus
{
    Pending,
    UnderReview,
    Cleared,
    Rejected
}

public enum JobOfferStatus
{
    AwaitingResponse,
    Accepted,
    Rejected,
    Withdrawn
}

public enum SalaryComponentType
{
    Earning,
    Deduction
}

public enum SalaryCalculationType
{
    FixedAmount,
    PercentageOfBasic
}

public enum AppraisalCycleStatus
{
    Draft,
    Active,
    Completed
}

public enum GoalStatus
{
    Pending,
    InProgress,
    Completed
}

public enum AppraisalStatus
{
    Draft,
    SelfAssessmentSubmitted,
    Completed
}

public enum FeedbackReviewerRelation
{
    Self,
    Manager,
    Peer
}

public enum ExpenseClaimStatus
{
    Pending,
    Approved,
    Rejected
}

public enum EmployeeAdvanceStatus
{
    Pending,
    Approved,
    Rejected,
    Paid,
    Settled
}

public enum TravelRequestStatus
{
    Pending,
    Approved,
    Rejected
}

public enum SeparationType
{
    Resignation,
    Termination,
    Retirement,
    EndOfContract
}

public enum SeparationStatus
{
    Pending,
    Approved,
    Completed
}

public enum FnFStatus
{
    Draft,
    Finalized
}
