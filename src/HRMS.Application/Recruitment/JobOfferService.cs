using HRMS.Application.Common.Interfaces;
using HRMS.Application.Common.Models;
using HRMS.Application.Employees;
using HRMS.Domain.Common;
using HRMS.Domain.Entities.Organization;
using HRMS.Domain.Entities.Recruitment;
using HRMS.Domain.Enums;

namespace HRMS.Application.Recruitment;

public class JobOfferService : IJobOfferService
{
    private readonly IUnitOfWork _uow;
    private readonly IEmployeeService _employeeService;

    public JobOfferService(IUnitOfWork uow, IEmployeeService employeeService)
    {
        _uow = uow;
        _employeeService = employeeService;
    }

    public async Task<List<JobOfferDto>> GetAllAsync(CancellationToken ct = default)
    {
        var offers = await _uow.Repository<JobOffer>().GetAllAsync(ct);
        var applicants = await _uow.Repository<JobApplicant>().GetAllAsync(ct);
        var designations = await _uow.Repository<Designation>().GetAllAsync(ct);
        var departments = await _uow.Repository<Department>().GetAllAsync(ct);

        var applicantNames = applicants.ToDictionary(a => a.Id, a => a.ApplicantName);
        var desigNames = designations.ToDictionary(d => d.Id, d => d.Name);
        var deptNames = departments.ToDictionary(d => d.Id, d => d.Name);

        return offers
            .OrderByDescending(o => o.OfferDate)
            .Select(o => new JobOfferDto(
                o.Id, o.JobApplicantId, applicantNames.GetValueOrDefault(o.JobApplicantId),
                o.DesignationId, desigNames.GetValueOrDefault(o.DesignationId),
                o.DepartmentId, deptNames.GetValueOrDefault(o.DepartmentId),
                o.OfferDate, o.ExpectedJoiningDate, o.AnnualCtc, o.Status, o.CreatedEmployeeId))
            .ToList();
    }

    public async Task<Result<JobOfferDto>> CreateAsync(CreateJobOfferRequest request, CancellationToken ct = default)
    {
        var applicant = await _uow.Repository<JobApplicant>().GetByIdAsync(request.JobApplicantId, ct);
        if (applicant is null)
            return Result<JobOfferDto>.Failure("Pelamar tidak ditemukan.");

        var existingOffers = await _uow.Repository<JobOffer>().FindAsync(
            o => o.JobApplicantId == request.JobApplicantId && o.Status == JobOfferStatus.AwaitingResponse, ct);
        if (existingOffers.Count > 0)
            return Result<JobOfferDto>.Failure("Pelamar ini sudah punya penawaran kerja yang masih menunggu respons.");

        var offer = new JobOffer
        {
            JobApplicantId = request.JobApplicantId,
            DesignationId = request.DesignationId,
            DepartmentId = request.DepartmentId,
            OfferDate = DateOnly.FromDateTime(DateTime.Today),
            ExpectedJoiningDate = request.ExpectedJoiningDate,
            AnnualCtc = request.AnnualCtc,
            Status = JobOfferStatus.AwaitingResponse,
            DocStatus = DocumentStatus.Submitted
        };

        await _uow.Repository<JobOffer>().AddAsync(offer, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<JobOfferDto>.Success(new JobOfferDto(
            offer.Id, offer.JobApplicantId, applicant.ApplicantName, offer.DesignationId, null,
            offer.DepartmentId, null, offer.OfferDate, offer.ExpectedJoiningDate, offer.AnnualCtc,
            offer.Status, null));
    }

    public async Task<Result> RespondAsync(Guid jobOfferId, bool accepted, CancellationToken ct = default)
    {
        var repo = _uow.Repository<JobOffer>();
        var offer = await repo.GetByIdAsync(jobOfferId, ct);
        if (offer is null)
            return Result.Failure("Penawaran kerja tidak ditemukan.");
        if (offer.Status != JobOfferStatus.AwaitingResponse)
            return Result.Failure("Penawaran ini sudah direspons sebelumnya.");

        offer.Status = accepted ? JobOfferStatus.Accepted : JobOfferStatus.Rejected;
        repo.Update(offer);

        if (accepted)
        {
            var applicant = await _uow.Repository<JobApplicant>().GetByIdAsync(offer.JobApplicantId, ct);
            if (applicant is not null)
            {
                applicant.Status = JobApplicantStatus.Accepted;
                _uow.Repository<JobApplicant>().Update(applicant);
            }
        }

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<Guid>> ConvertToEmployeeAsync(Guid jobOfferId, Guid employmentTypeId, DateOnly dateOfJoining, CancellationToken ct = default)
    {
        var offer = await _uow.Repository<JobOffer>().GetByIdAsync(jobOfferId, ct);
        if (offer is null)
            return Result<Guid>.Failure("Penawaran kerja tidak ditemukan.");
        if (offer.Status != JobOfferStatus.Accepted)
            return Result<Guid>.Failure("Hanya penawaran yang sudah diterima yang bisa dikonversi jadi pegawai.");
        if (offer.CreatedEmployeeId.HasValue)
            return Result<Guid>.Failure("Penawaran ini sudah pernah dikonversi menjadi pegawai.");

        var applicant = await _uow.Repository<JobApplicant>().GetByIdAsync(offer.JobApplicantId, ct);
        if (applicant is null)
            return Result<Guid>.Failure("Data pelamar tidak ditemukan.");

        // BUG FIX: this used to take a separately-chosen `companyId` parameter from the UI,
        // independent from offer.DepartmentId — a mismatched pair (department belongs to a
        // different company than the one picked) would either silently corrupt data or, after
        // the EmployeeService cross-company validation fix, always fail. Deriving the company
        // straight from the offer's department removes the redundant, error-prone selection
        // entirely — there's only one correct company for a given department anyway.
        var department = await _uow.Repository<Department>().GetByIdAsync(offer.DepartmentId, ct);
        if (department is null)
            return Result<Guid>.Failure("Departemen pada penawaran ini tidak ditemukan.");

        // Best-effort split of "Nama Lengkap" into first/last — Employee wants separate fields.
        var nameParts = applicant.ApplicantName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var firstName = nameParts.Length > 0 ? nameParts[0] : applicant.ApplicantName;
        var lastName = nameParts.Length > 1 ? nameParts[1] : "-";

        var createRequest = new CreateEmployeeRequest(
            firstName, null, lastName,
            DateOnly.FromDateTime(DateTime.Today.AddYears(-25)), // unknown at this stage — HR updates after onboarding paperwork
            Gender.PreferNotToSay,
            applicant.Email, applicant.Phone,
            dateOfJoining, department.CompanyId, offer.DepartmentId, offer.DesignationId, employmentTypeId,
            ReportsToEmployeeId: null, BranchId: null, HolidayListId: null);

        var employeeResult = await _employeeService.CreateAsync(createRequest, ct);
        if (!employeeResult.Succeeded)
            return Result<Guid>.Failure(employeeResult.Errors.ToArray());

        offer.CreatedEmployeeId = employeeResult.Value!.Id;
        _uow.Repository<JobOffer>().Update(offer);
        await _uow.SaveChangesAsync(ct);

        return Result<Guid>.Success(employeeResult.Value.Id);
    }
}
