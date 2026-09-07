using HRMS.Domain.Enums;

namespace HRMS.Application.Expense;

// ---- Expense Category ----
public record ExpenseCategoryDto(Guid Id, string Name);
public record CreateExpenseCategoryRequest(string Name);

// ---- Expense Claim ----
public record ExpenseClaimDetailDto(Guid Id, Guid ExpenseCategoryId, string? CategoryName, DateOnly ExpenseDate, string? Description, decimal Amount);

public record ExpenseClaimDto(
    Guid Id, Guid EmployeeId, string? EmployeeName, DateOnly ClaimDate, decimal TotalAmount,
    ExpenseClaimStatus Status, Guid? ApprovedByEmployeeId, string? ApprovedByName, string? RejectionReason,
    List<ExpenseClaimDetailDto> Details
);

public record ExpenseClaimLineItem(Guid ExpenseCategoryId, DateOnly ExpenseDate, string? Description, decimal Amount);
public record CreateExpenseClaimRequest(Guid EmployeeId, List<ExpenseClaimLineItem> Lines);

// ---- Employee Advance ----
public record EmployeeAdvanceDto(
    Guid Id, Guid EmployeeId, string? EmployeeName, string PurposeDescription, decimal AdvanceAmount,
    DateOnly RequestDate, EmployeeAdvanceStatus Status, Guid? ApprovedByEmployeeId, string? ApprovedByName
);

public record CreateEmployeeAdvanceRequest(Guid EmployeeId, string PurposeDescription, decimal AdvanceAmount);

// ---- Travel Request ----
public record TravelRequestDto(
    Guid Id, Guid EmployeeId, string? EmployeeName, string Destination, string? Purpose,
    DateOnly FromDate, DateOnly ToDate, decimal? EstimatedCost, TravelRequestStatus Status,
    Guid? ApprovedByEmployeeId, string? ApprovedByName
);

public record CreateTravelRequestRequest(Guid EmployeeId, string Destination, string? Purpose, DateOnly FromDate, DateOnly ToDate, decimal? EstimatedCost);
