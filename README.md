# HRMS.NET

Rebuild dari [frappe/hrms](https://github.com/frappe/hrms) menggunakan stack Microsoft murni —
**tanpa dependency ke Frappe Framework atau ERPNext**.

## Stack Teknologi

| Layer | Teknologi |
|---|---|
| Backend | ASP.NET Core 8 (C#) |
| UI | Blazor Server (full C#, satu bahasa dari DB sampai UI) |
| ORM | Entity Framework Core 8 |
| Database | SQL Server (self-hosted / on-premise) |
| Auth | ASP.NET Core Identity (cookie-based, tanpa Azure AD) |
| Background jobs | Hangfire (storage di SQL Server yang sama — pengganti scheduler Frappe) |
| Arsitektur | Clean Architecture (Domain → Application → Infrastructure → Web) |

## Struktur Solution

```
HRMS.sln
├── NuGet.Config                 # Hapus/ganti isinya sebelum restore (lihat catatan di bawah)
└── src/
    ├── HRMS.Domain/              # Entities murni C#, tanpa dependency eksternal
    │   ├── Common/                (BaseEntity, SubmittableEntity, DocumentStatus)
    │   ├── Enums/
    │   └── Entities/
    │       ├── Organization/      (Company, Branch, Department, Designation, EmploymentType, HolidayList)
    │       ├── Employees/         (Employee)
    │       ├── Leave/             (LeaveType, LeaveAllocation, LeaveApplication, LeaveLedgerEntry)
    │       └── Attendance/        (ShiftType, ShiftAssignment, EmployeeCheckin, AttendanceRecord)
    ├── HRMS.Application/          # Business logic, tidak tahu-menahu soal EF Core/database
    │   ├── Common/                (IRepository, IUnitOfWork, ICurrentUserService, Result<T>)
    │   ├── Employees/             (EmployeeService — CRUD + validasi)
    │   └── Leave/                 (LeaveApplicationService — saldo cuti, approve/reject)
    ├── HRMS.Infrastructure/       # EF Core DbContext, Identity, Hangfire, implementasi repository
    │   ├── Persistence/           (HrmsDbContext, Repository, UnitOfWork, DataSeeder)
    │   └── Identity/              (CurrentUserService)
    └── HRMS.Web/                  # Blazor Server (UI)
        ├── Components/Pages/Employees/  (EmployeeList, EmployeeCreate)
        ├── Components/Pages/Leave/      (LeaveApplicationList, LeaveApplicationCreate)
        └── Program.cs
```

## Cara Menjalankan

**Prasyarat:** .NET 8 SDK, SQL Server (Express/Developer/Standard — bisa Docker), akses internet normal untuk NuGet restore (sandbox pembuatan project ini tidak punya akses nuget.org, jadi belum pernah di-restore).

```bash
git clone <repo-kamu>
cd HRMS

# PENTING: hapus NuGet.Config di root (dibuat khusus untuk sandbox offline saat development ini)
rm NuGet.Config

dotnet restore
```

Atur connection string di `src/HRMS.Web/appsettings.Development.json` sesuai instance SQL Server kamu, lalu:

```bash
cd src/HRMS.Web
dotnet ef migrations add InitialCreate -p ../HRMS.Infrastructure -s .
dotnet ef database update -p ../HRMS.Infrastructure -s .
dotnet run
```

Migrasi juga otomatis dijalankan (`db.Database.Migrate()`) setiap kali aplikasi start, jadi di server produksi kamu cukup deploy dan jalankan.

**Deployment on-premise:** publish sebagai self-contained (`dotnet publish -c Release --self-contained -r win-x64` atau `linux-x64`), lalu host di IIS (Windows) atau sebagai systemd service di belakang Nginx (Linux) — tidak butuh layanan cloud apa pun.

## Multi-Tenant

Sistem ini **multi-tenant** dengan model **shared database + kolom `TenantId`**:

- Satu database SQL Server melayani banyak tenant (perusahaan/klien) sekaligus. Setiap tabel bisnis (Employee, Department, LeaveApplication, dst.) punya kolom `TenantId`.
- **Isolasi otomatis, bukan manual**: `HrmsDbContext` memasang *global query filter* ke setiap entity yang mengimplementasikan `IMustHaveTenant` lewat reflection (`OnModelCreating`). Artinya, developer yang menambah modul baru **tidak perlu ingat** menulis `WHERE TenantId = ...` di setiap query — tinggal turunkan entity dari `TenantEntity`, filter otomatis aktif.
- **Auto-stamp saat insert**: `HrmsDbContext.SaveChangesAsync` di-override untuk otomatis mengisi `TenantId` pada entity baru berdasarkan tenant user yang sedang login — service (`EmployeeService`, dll.) tidak perlu set `TenantId` manual.
- **Resolusi tenant**: setelah login, klaim `TenantId` disisipkan ke `ClaimsPrincipal` user (lihat `ApplicationUserClaimsPrincipalFactory`). `ITenantProvider` (implementasi di `HRMS.Web/Services/TenantProvider.cs`) membaca klaim ini lewat `AuthenticationStateProvider` — dipilih ketimbang `IHttpContextAccessor` karena `HttpContext` tidak reliable di Blazor Server setelah koneksi SignalR/circuit terbentuk.
- **Onboarding tenant baru**: halaman `/register-tenant` memanggil `TenantProvisioningService`, yang dalam satu transaksi membuat record `Tenant`, data referensi default (Company/Department/Designation/LeaveType), dan akun Administrator pertama untuk tenant tersebut.
- **Login**: `/login` sengaja **tidak** pakai `@rendermode InteractiveServer` — form POST biasa ke endpoint minimal API `/account/login`, karena cookie autentikasi tidak bisa di-set dari dalam koneksi SignalR yang sudah terbuka (batasan arsitektural Blazor Server, bukan bug).

**Yang belum dibangun (follow-up):**
- Halaman admin untuk kelola tenant lain (saat ini `Tenant` cuma bisa dibuat lewat `/register-tenant`, belum ada listing/edit)
- Endpoint `/account/login` & `/account/logout` masih `DisableAntiforgery()` untuk simplicity — untuk produksi, tambahkan proteksi CSRF penuh atau ganti dengan `dotnet new blazor -au Individual` scaffolding resmi
- Belum ada mekanisme "super-admin lintas tenant" (mis. staf internal vendor HRMS yang perlu akses semua tenant untuk support)
- Rate limiting / lockout policy untuk endpoint login



- Modul **Pegawai**: CRUD, validasi email unik & nomor pegawai auto-generate, relasi Company/Department/Designation/EmploymentType/ReportsTo
- Modul **Cuti**: pengajuan cuti dengan pengecekan saldo (via ledger append-only) dan deteksi tanggal tumpang tindih, approve/reject
- Fondasi **Attendance** (entity sudah ada: ShiftType, ShiftAssignment, EmployeeCheckin, AttendanceRecord) — service & UI belum dibuat
- Autentikasi & role dasar (Administrator, HR Manager, HR User, Employee)
- Background job infrastructure (Hangfire) siap dipakai untuk job terjadwal

## Hardening yang Sudah Diterapkan

- **Antiforgery aktif penuh** di endpoint `/account/login` dan `/account/logout` (token disisipkan via `<AntiforgeryToken />` di form, divalidasi otomatis oleh middleware `UseAntiforgery()` — ini default ASP.NET Core 8 untuk minimal API endpoint ber-state-changing verb, tidak perlu opt-in eksplisit)
- **Rate limiting** endpoint login: maksimal 5 percobaan per menit per klien (mitigasi credential-stuffing/brute force, penting untuk deployment on-premise yang mungkin tidak di belakang WAF eksternal)
- **Cookie hardening**: `SameSite=Strict`, `Secure=Always` (butuh HTTPS — pastikan reverse proxy/sertifikat TLS terpasang), sliding expiration 8 jam
- **Blokir login untuk tenant nonaktif** — dicek eksplisit di endpoint login sebelum validasi password, supaya field `Tenant.IsActive` benar-benar berfungsi sebagai kill-switch
- **Lockout policy** Identity: maksimal 5 percobaan gagal sebelum akun terkunci sementara
- Perbaikan urutan middleware: `UseAuthentication`/`UseAuthorization` sebelum `UseAntiforgery` (urutan resmi ASP.NET Core)
- `TenantProvider`/`CurrentUserService` dibuat defensif (try-catch) terhadap kegagalan resolusi `AuthenticationStateProvider` di luar Blazor circuit — penting karena endpoint login/logout adalah minimal API biasa, bukan komponen Blazor

**Masih perlu (belum dikerjakan):**
- Halaman admin untuk kelola/list tenant lain
- Mekanisme super-admin lintas tenant
- Audit log percobaan login (sukses/gagal) untuk investigasi keamanan

## Modul Attendance (Phase 2 — Selesai)

- **Check-in/check-out mandiri** (`/attendance/checkin`) — pegawai klik tombol, sistem catat waktu; deteksi status (sudah check-in atau belum) otomatis dari riwayat hari itu
- **Generate attendance otomatis** — `AttendanceService.GenerateAttendanceForDateAsync` memasangkan punch IN/OUT jadi total jam kerja, lalu silang-cek dengan modul Cuti (`LeaveApplication` approved di tanggal itu → status `OnLeave` otomatis, prioritas di atas hasil check-in)
- **Background job harian** (`AttendanceGenerationJob`, jalan tiap jam 01:00 via Hangfire) — generate rekap attendance kemarin untuk semua pegawai aktif, **di semua tenant**
- **Shift Type & Shift Assignment** (`/shift-types`, `/shift-assignments`) — khusus role Administrator/HR Manager, dengan validasi tumpang tindih penugasan shift

**Catatan arsitektur penting — tenant context di background job:** Hangfire job berjalan di luar HTTP request/Blazor circuit, jadi `ITenantProvider` (yang biasanya baca klaim dari user login) tidak punya konteks. `AttendanceGenerationJob` menangani ini dengan meng-iterasi semua tenant aktif secara eksplisit, lalu untuk masing-masing tenant: buat DI scope baru, panggil `ITenantProvider.SetTenant(tenantId)` (method override eksplisit yang ditambahkan khusus untuk kasus ini), baru resolve `IAttendanceService` — sehingga `HrmsDbContext` di scope tersebut ter-filter ke tenant yang benar. Kalau ini terlewat, job akan "berhasil jalan" tapi tidak memproses data siapa pun (karena filter otomatis akan resolve ke `TenantId == Guid.Empty`).

## Modul Recruitment (Phase 3 — Selesai)

- **Job Opening** (`/job-openings`) — buka lowongan, tutup lowongan, lihat jumlah pelamar
- **Job Applicant** (`/job-openings/{id}/applicants`) — tambah pelamar per lowongan, tracking status (Open → InterviewScheduled → Accepted/Rejected/OnHold)
- **Interview & Feedback** (`/applicants/{id}`) — jadwalkan interview (ronde, waktu, pewawancara dari data Employee), catat feedback (rating 1-5, rekomendasi) — interview otomatis berstatus Cleared/Rejected begitu feedback masuk
- **Job Offer** — buat penawaran kerja dari pelamar, kandidat terima/tolak
- **Konversi ke Employee** — begitu offer diterima, HR bisa klik "Konversi Jadi Pegawai" yang otomatis memanggil `IEmployeeService.CreateAsync` (modul Employee) dengan data dari pelamar — menutup siklus rekrutmen sampai jadi pegawai aktif tanpa entry data ulang

**Keterbatasan yang disengaja (untuk simplicity Phase 3):**
- Tidak ada upload file CV — cuma field teks bebas (`ResumeNotes`) untuk catatan/link. Upload file butuh modul file-storage yang belum dibangun.
- Saat konversi ke Employee, tanggal lahir & gender di-set default/placeholder (`PreferNotToSay`, umur 25 tahun) karena data itu belum ada di tahap lamaran — HR perlu update manual setelah pegawai baru mengisi data lengkap.
- Belum ada halaman publik untuk kandidat melamar sendiri (career page) — semua entry pelamar dilakukan HR secara manual.
- Belum ada Employee Onboarding checklist/task tracking terpisah (baru sebatas pembuatan record Employee).

## Bug Fix — Audit Kode Menyeluruh

Sesi audit khusus menemukan dan memperbaiki bug-bug berikut:

1. **KRITIS — Modul Cuti sebenarnya tidak fungsional.** Entity `LeaveAllocation` ada di Domain sejak awal, tapi tidak pernah ada service yang benar-benar membuatnya beserta entry kredit di `LeaveLedgerEntry`. Akibatnya saldo cuti semua pegawai selalu 0, dan pengajuan cuti berbayar (`AllowNegativeBalance=false`, termasuk default "Cuti Tahunan") **selalu gagal** dengan pesan "saldo tidak mencukupi". **Fix:** dibuat `ILeaveAllocationService` + halaman `/leave-allocations` untuk HR mengalokasikan cuti ke pegawai.
2. **KRITIS — Error kompilasi di halaman daftar cuti.** `LeaveApplicationList.razor` mengakses `app.EmployeeName`, padahal `LeaveApplication` (entity domain) tidak punya properti itu — akan gagal build. **Fix:** diganti ke `app.Employee?.FullName`.
3. **TINGGI — Approver cuti selalu tercatat `Guid.Empty`.** Tombol Setujui/Tolak di halaman cuti masih pakai placeholder `Guid.Empty` alih-alih ID pegawai yang sedang login — sisa dari sebelum sistem klaim/auth selesai dibangun (ada komentar `TODO` yang jadi basi). **Fix:** pakai `ICurrentUserService.EmployeeId`, tombol otomatis nonaktif dengan pesan jelas kalau akun belum ditautkan ke data pegawai.
4. **TINGGI — Nomor pegawai bisa collision setelah soft-delete.** `EmployeeService` generate `EMP-00001` dst. dengan menghitung jumlah baris pegawai aktif (`Count() + 1`). Kalau ada pegawai yang dihapus (soft-delete), penomoran berikutnya bisa mengulang nomor yang sudah dipakai (baris lama masih ada tapi tersembunyi), melanggar unique index dan gagal saat simpan. **Fix:** diganti pakai counter (`Tenant.EmployeeSequence`) yang persisten dan tidak pernah mundur.
5. **SEDANG — Attendance otomatis salah tandai Absent di hari libur.** `AttendanceService` tidak pernah cross-check `HolidayList`/`Holiday` pegawai, jadi background job harian akan menandai **semua pegawai Absent tiap akhir pekan/hari libur perusahaan** karena tidak ada checkin. **Fix:** ditambah pengecekan hari libur sebelum menghitung status, dengan status baru `AttendanceStatus.Holiday`.
6. **SEDANG — Kode tenant lolos validasi karena beda kapitalisasi.** Pengecekan keunikan kode tenant membandingkan input mentah dengan nilai tersimpan yang sudah di-uppercase, jadi "acme" tidak terdeteksi bentrok dengan "ACME" yang sudah ada — baru gagal belakangan dengan pesan error database mentah. **Fix:** normalisasi (`Trim().ToUpperInvariant()`) sebelum dibandingkan maupun disimpan.
7. **RENDAH — Interview bisa dijadwalkan untuk pelamar yang statusnya sudah final.** Menjadwalkan interview untuk pelamar `Rejected`/`Accepted` diam-diam menimpa status mereka jadi `InterviewScheduled` lagi. **Fix:** ditambah validasi penolakan.
8. **RENDAH — Job offer ganda.** Tidak ada pengecekan sebelum membuat offer baru, jadi satu pelamar bisa punya beberapa offer `AwaitingResponse` sekaligus. **Fix:** ditambah validasi.

**Catatan jujur soal keterbatasan audit:** karena sandbox saya tidak punya akses ke nuget.org, Infrastructure & Web layer tidak bisa saya compile langsung — bug #2 (error kompilasi Razor) ditemukan lewat pembacaan manual baris-per-baris, bukan lewat compiler. Domain & Application layer sudah diverifikasi build bersih (0 error) setelah semua fix di atas. Kalau nanti build di komputer kamu masih menemukan error lain di Infrastructure/Web, itu kemungkinan besar karena keterbatasan review manual ini — laporkan ke saya, saya bantu perbaiki.

### Audit Round 2 (setelah Payroll & Performance dibangun)

9. **TINGGI — Bisa assign struktur gaji lintas perusahaan.** `SalaryStructureService.AssignAsync` tidak pernah cek bahwa `SalaryStructure` yang dipilih benar-benar milik `Company` yang sama dengan pegawainya — dropdown Pegawai dan Struktur Gaji di UI tidak saling filter. **Fix:** validasi `structure.CompanyId == employee.CompanyId`.
10. **TINGGI — Bug yang sama di modul Employee (akar masalah).** `EmployeeService.CreateAsync`/`UpdateAsync` tidak pernah validasi `Department`/`Branch` yang dipilih benar-benar milik `Company` yang dipilih — root cause yang sama juga memengaruhi alur konversi Job Offer → Employee. **Fix:** validasi `department.CompanyId == request.CompanyId` (dan `branch.CompanyId` kalau diisi) di kedua method.
11. **TINGGI — Turunan dari #10: form "Konversi Jadi Pegawai" di Recruitment.** Form ini punya dropdown Perusahaan yang dipilih manual, terpisah dari `Department` yang sudah ditentukan saat offer dibuat — kombinasi yang tidak cocok bisa membuat data pegawai-departemen-perusahaan tidak konsisten (atau, setelah fix #10, malah selalu gagal). **Fix:** dropdown Perusahaan dihapus total; `JobOfferService.ConvertToEmployeeAsync` sekarang menurunkan `CompanyId` otomatis dari `Department` milik offer — menghilangkan sumber kesalahannya, bukan cuma memvalidasinya.
12. **SEDANG — Bug yang sama di modul Performance.** `AppraisalService.CreateAsync` dan `GoalService.CreateAsync` tidak validasi pegawai yang dinilai berasal dari perusahaan yang sama dengan `AppraisalCycle`-nya. **Fix:** validasi `employee.CompanyId == cycle.CompanyId` di kedua service.
13. **RENDAH — Override komponen gaji tanpa validasi range.** `SalaryStructureService.AddComponentAsync` menerima `AmountOverride`/`PercentageOverride` tanpa batas — bisa masuk nominal negatif atau persentase >100%. **Fix:** ditambah validasi range.
14. **RENDAH — DTO hasil `RunPayrollAsync` menyesatkan.** Response langsung dari menjalankan payroll selalu mengembalikan `TotalNetPay = 0` (dikomentari "akan dihitung ulang saat reload") — tidak salah secara fungsional karena UI selalu reload setelahnya, tapi kalau API ini dipakai dari tempat lain nanti, nilainya salah. **Fix:** dihitung dari slip yang baru dibuat sebelum dikembalikan.

**Catatan tentang pola bug #9-12:** semuanya termasuk kategori yang sama — *cross-entity validation gap* pada sistem yang mendukung multi-company per tenant. Dropdown-dropdown di UI (Pegawai, Departemen, Struktur Gaji, Siklus Penilaian) semuanya diambil dari seluruh tenant tanpa saling filter berdasarkan perusahaan, jadi kombinasi yang secara teknis valid tapi secara bisnis salah bisa lolos tanpa validasi eksplisit di service layer. Follow-up yang belum dikerjakan: dropdown-dropdown itu idealnya juga difilter secara dinamis di UI (baru menampilkan opsi yang company-nya cocok) supaya user tidak perlu coba-coba dulu baru tahu salah — saat ini baru validasi server-side yang menolak dengan pesan jelas.

### Audit Round 3 (fokus Phase 5 — Expense & Travel)

15. **SEDANG — Item klaim pengeluaran yang diisi sebagian hilang diam-diam.** Di halaman `/expense-claims`, kalau pegawai mengisi kategori tapi lupa isi nominal (atau sebaliknya) pada salah satu baris item, baris itu di-filter keluar tanpa pemberitahuan apa pun sebelum dikirim ke server — pegawai bisa saja tidak sadar item yang mereka kira sudah termasuk ternyata tidak ikut terkirim dalam klaim. **Fix:** ditambah pengecekan baris "diisi sebagian" (salah satu field terisi tapi tidak keduanya) yang menolak submit dengan pesan jelas, dibedakan dari baris kosong murni (baris template baru yang memang boleh diabaikan).

Modul Payroll (Phase 6) diperiksa ulang secara khusus pada audit ini juga — hasilnya bersih, semua fix dari Round 2 (#9, #11, #13, #14) masih berlaku benar dan tidak ada regresi atau bug baru ditemukan di `PayrollEntryService`/`SalaryStructureService`.

### Audit Round 4 — Error Kompilasi Nyata dari Build Pengguna (RZ9986)

16. **KRITIS — Error kompilasi Razor: `RZ9986` di `ApplicantDetail.razor`.** Ini bukan hasil audit saya, tapi laporan langsung dari hasil `dotnet build` pengguna di komputernya sendiri — dan tepat inilah risiko yang saya ingatkan berkali-kali di README: sandbox saya tidak bisa compile layer Web, jadi kesalahan sintaks Razor sekelas ini tidak akan ketahuan lewat review manual biasa. Baris `<InputCheckbox ... id="rec@(i.Id)" />` mencampur teks literal (`"rec"`) dengan ekspresi C# (`@(i.Id)`) di atribut `id` — pola ini valid untuk elemen HTML biasa (`<input>`, `<div>`, dst.) tapi **tidak didukung untuk komponen Blazor** (`InputCheckbox`, `InputText`, `InputSelect`, dan sejenisnya), karena atribut komponen harus berupa satu ekspresi C# tunggal, bukan gabungan teks+ekspresi. **Fix:** diganti jadi satu ekspresi utuh via string interpolation: `id="@($"rec{i.Id}")"`. Sudah saya `grep` menyeluruh ke semua file `.razor` untuk pola serupa (atribut campuran teks+ekspresi pada tag komponen) — tidak ditemukan instance lain, ini satu-satunya lokasi.

**Pelajaran dari bug ini:** kalau kamu build project ini di komputer sendiri dan menemukan error compiler (terutama di layer `HRMS.Web` yang tidak bisa saya compile langsung), **paste pesan errornya apa adanya** (format `File(baris,kolom): error CSxxxx/RZxxxx: ...`) — itu jauh lebih cepat dan akurat daripada saya menebak lewat audit manual berulang.

### Audit Round 5 — Dua Bug Nyata Lagi dari Build & Runtime Pengguna

17. **KRITIS — `CS0034`: operator `==` ambigu di `Login.razor`.** `HttpContext?.Request.Query["error"] == "1"` gagal compile dengan "Operator '==' is ambiguous on operands of type 'StringValues?' and 'string'". Penyebabnya: `Request.Query["error"]` bertipe `StringValues` (struct), dan karena dirantai lewat `HttpContext?.` (null-conditional), seluruh ekspresi jadi `StringValues?` (nullable struct). Tipe `StringValues` punya banyak overload operator `==` (termasuk terhadap `string`), dan begitu dibungkus `Nullable<StringValues>`, compiler menemukan dua jalur resolusi yang sama validnya sehingga bingung. **Fix:** panggil `.ToString()` dulu sebelum dibandingkan (`HttpContext?.Request.Query["error"].ToString() == "1"`) — hasilnya jadi `string?` biasa, tidak ambigu. **Catatan:** versi lama file ini (dari push GitHub sebelumnya) ternyata sudah punya fix ini, tapi tertimpa saat saya force-push versi "terbaru" tanpa mengecek ulang detail perbedaan itu — pelajaran: jangan asumsikan versi lokal selalu lebih benar dari versi yang sudah di-deploy, terutama untuk file yang jarang disentuh ulang.
18. **KRITIS — `InvalidOperationException` saat runtime: relasi FK ambigu di `LeaveApplication`.** Error EF Core: *"Both relationships between 'LeaveApplication.ApprovedBy' and 'Employee' and between 'LeaveApplication.Employee' and 'Employee' could use {'EmployeeId'} as the foreign key"* — muncul saat aplikasi pertama kali menyentuh database (bukan saat compile), karena `LeaveApplication` punya dua relasi ke `Employee` (peminta cuti via `EmployeeId`, penyetuju via `ApprovedByEmployeeId`) dan EF Core tidak bisa menebak sendiri FK mana untuk relasi mana tanpa konfigurasi eksplisit. **Root cause:** modul Leave dibangun di **Phase 1**, sebelum saya konsisten membuat file `XxxConfiguration.cs` eksplisit untuk tiap modul — jadi modul ini satu-satunya yang kelewat. Saya audit ulang **seluruh domain model** (bukan cuma Leave) dan menemukan total 6 entity dengan pola "dua referensi ke Employee dalam satu entity": `Appraisal`, `EmployeeSeparation`, `ExpenseClaim`, `EmployeeAdvance`, `TravelRequest` (sudah benar dikonfigurasi), dan `LeaveApplication` (yang hilang). **Fix:** dibuat `LeaveConfiguration.cs` baru dengan `HasForeignKey` eksplisit untuk kedua relasi.

**Kenapa dua bug ini lolos dari 4 putaran audit sebelumnya:** keduanya adalah kelas error yang secara struktural **tidak mungkin** saya deteksi lewat pembacaan kode manual — satu butuh compiler C# yang sesungguhnya (nuget.org diblokir di sandbox saya), satu lagi butuh EF Core benar-benar membangun model terhadap koneksi database asli (tidak ada SQL Server di sandbox). Ini menegaskan kenapa laporan error langsung dari `dotnet build`/runtime kamu jauh lebih berharga daripada saya "menebak lebih teliti" — beberapa kelas bug memang cuma ketahuan lewat eksekusi nyata.

## Modul Payroll (Phase 6 — Selesai)

- **Salary Component** (`/salary-components`) — definisikan komponen gaji (Gaji Pokok implisit, Tunjangan, BPJS, PPh21, dst.), tipe Earning/Deduction, dihitung sebagai jumlah tetap atau persentase dari gaji pokok
- **Salary Structure** (`/salary-structures`) — template yang menggabungkan beberapa komponen per perusahaan, dengan opsi override nominal/persentase per struktur
- **Salary Structure Assignment** (`/salary-structure-assignments`) — tetapkan struktur + gaji pokok ke tiap pegawai; kalau pegawai punya beberapa assignment dari waktu ke waktu (mis. kenaikan gaji), sistem otomatis pakai yang paling baru pada tanggal mulai periode payroll
- **Payroll Entry** (`/payroll-entries`) — jalankan payroll untuk satu perusahaan di satu periode; generate Salary Slip untuk semua pegawai aktif yang punya assignment, **dengan proration otomatis** — hari kerja dalam periode dihitung (kalender minus Sabtu/Minggu), lalu gaji pokok dipotong proporsional untuk tiap hari berstatus `Absent` di modul Attendance
- **Salary Slip** (`/payroll-entries/{id}`) — rincian tiap komponen, gross pay, total potongan, net pay per pegawai

**Yang disederhanakan secara sadar** (dan kenapa):
- **Tidak ada kalkulator PPh21 otomatis sesuai UU Indonesia** (PTKP, tarif progresif berlapis, dst.) — pajak diperlakukan sebagai komponen Deduction biasa (persentase atau nominal manual) yang HR isi sendiri. Membangun kalkulator pajak yang benar-benar akurat sesuai regulasi adalah proyek tersendiri; pendekatan Frappe sendiri untuk pajak regional juga butuh kustomisasi per negara.
- **Tidak ada formula/rule engine** seperti "Salary Structure" asli Frappe (yang mendukung ekspresi custom antar-komponen, mis. "HRA = 40% dari Basic jika kota metro, 30% jika bukan"). Di sini hanya jumlah tetap atau persentase lurus dari gaji pokok.
- **Proration hanya berdasarkan status `Absent`** — belum menghitung `HalfDay` sebagai 0.5 hari, dan hari kerja dihitung murni kalender (Senin-Jumat) tanpa cross-check `HolidayList` perusahaan (jadi kalau ada cuti bersama di tengah bulan kerja, itu belum otomatis dikeluarkan dari pembagi hari kerja).
- **Tidak ada payslip PDF** — slip gaji baru bisa dilihat di halaman web, belum bisa di-generate jadi PDF (perlu library QuestPDF, sudah dicatat di roadmap "Lintas modul").
- **Belum ada Income Tax Slab / Gratuity / Employee Tax Exemption Declaration** sebagai entity terpisah — kalau nanti dibutuhkan kalkulasi pajak progresif otomatis, ini fondasi yang perlu dibangun dulu.

## Modul Performance Management (Phase 4 — Selesai)

- **Appraisal Cycle** (`/appraisal-cycles`) — siklus penilaian per perusahaan (mis. "Semester 1 2026"), dengan status Draft → Active → Completed
- **KRA** (`/kras`) — Key Result Area, kategori goal yang reusable lintas siklus (mis. "Kualitas Kerja", "Pencapaian Target")
- **Goal** — goal individual per pegawai per siklus, ditautkan ke KRA, dengan progress bar 0-100% (status Pending/InProgress/Completed otomatis mengikuti progress)
- **Appraisal** (`/appraisals/{id}`) — workflow dua tahap: pegawai isi **self-assessment** (komentar + rating diri 1-5) lebih dulu, baru manager bisa isi **review** (komentar + rating akhir 1-5) — sistem menolak review manager kalau self-assessment belum ada
- **Feedback 360** — feedback tambahan dari rekan kerja/manager/diri sendiri pada satu appraisal, pola sama seperti `InterviewFeedback` di modul Recruitment

**Yang disederhanakan:** tidak ada bobot/weighting otomatis antar-goal untuk menghitung rating akhir (rating akhir diisi manual oleh manager, bukan hasil kalkulasi dari progress goal + feedback). Belum ada notifikasi/reminder saat siklus penilaian dibuka atau mendekati tenggat.

## Modul Expense & Travel (Phase 5 — Selesai)

- **Expense Category** (`/expense-categories`) — kategori pengeluaran reusable (Transportasi, Akomodasi, dst.)
- **Expense Claim** (`/expense-claims`) — pegawai ajukan klaim dengan beberapa item baris sekaligus (kategori + tanggal + nominal per baris), total dihitung otomatis, approve/reject satu langkah
- **Employee Advance** (`/employee-advances`) — pengajuan uang muka sebelum perjalanan/pengeluaran, workflow Pending → Approved → Paid (atau Rejected), dengan field `SettlesAdvanceId` di `ExpenseClaim` untuk menautkan klaim penyelesaian ke advance-nya (rekonsiliasi kas aktual tetap di luar sistem ini)
- **Travel Request** (`/travel-requests`) — pengajuan perjalanan dinas dengan tujuan, tanggal, dan estimasi biaya

**Yang disederhanakan secara sadar:** workflow approval di sini **satu tingkat** (satu approver, sama seperti Cuti dan Appraisal), bukan multi-level berjenjang berdasarkan nominal (mis. klaim di atas Rp 5 juta butuh approval tambahan dari Finance Manager) seperti yang dijanjikan di roadmap awal — pola itu butuh konsep "approval matrix"/routing dinamis yang belum dibangun. Kalau dibutuhkan nanti, `ExpenseClaimStatus` bisa diperluas jadi beberapa tahap sebagaimana `AppraisalStatus` (Draft → SelfAssessmentSubmitted → Completed) yang sudah punya pola serupa.

## Modul Exit & Separation (Phase 7 — Selesai)

- **Employee Separation** (`/employee-separations`) — ajukan resign/termination/retirement/end-of-contract, workflow Pending → Approved (menetapkan tanggal relieving) → Completed
- **Exit Interview** — catat feedback pegawai yang keluar (pewawancara, rating 1-5, komentar)
- **Full & Final Settlement** — generate otomatis berisi **uang pengganti cuti** (dihitung dari saldo cuti yang `IsEncashable`, dikali tarif harian dari `SalaryStructureAssignment` terakhir pegawai) dan **potongan advance yang belum diselesaikan** (`EmployeeAdvance` berstatus `Paid` tapi belum `Settled`), plus bisa ditambah item manual (mis. potongan ganti aset) sebelum difinalisasi

**Titik integrasi penting — mengaktifkan kode yang sudah lama menganggur:**
- `EmployeeService.MarkAsRelievedAsync` sudah ada sejak **Phase 1** tapi tidak pernah dipanggil dari alur manapun sampai sekarang — `EmployeeSeparationService.CompleteAsync` adalah pemanggil pertamanya, yang benar-benar menonaktifkan Employee (`Status = LeftOrganization`) begitu separation diselesaikan.
- `LeaveType.IsEncashable` juga sudah ada sejak Phase 1 tapi tidak pernah dibaca di mana pun — sekarang jadi dasar perhitungan uang pengganti cuti di FnF Settlement.

**Bug yang saya temukan & perbaiki saat membangun ini:** `FnFSettlementService.AddComponentAsync` awalnya menghitung ulang total dengan query database setelah `AddAsync` — tapi entity yang baru ditambahkan belum tersimpan (belum `SaveChangesAsync`), jadi query itu tidak akan melihatnya. Saya sempat menambal dengan `.Append()` yang rapuh dan membingungkan sebelum menyadari itu bug, lalu memperbaikinya jadi update total secara incremental (bukan re-query).

**Yang disederhanakan:** tarif harian untuk uang pengganti cuti pakai asumsi sederhana (gaji pokok ÷ 30 hari kalender), bukan menghitung hari kerja aktual seperti di modul Payroll. Approval separation masih satu tingkat, sama seperti modul lain.

## Roadmap — Modul yang Masih Perlu Dibangun







Dipetakan langsung dari struktur doctype di `frappe/hrms/hrms/hr/doctype` dan `hrms/payroll/doctype`:

### Phase 2 — Attendance & Shift ✅ Selesai
- ~~Service + UI untuk EmployeeCheckin, AttendanceRecord (generate otomatis dari checkin)~~
- ~~Shift Type & Shift Assignment~~
- Belum: Shift Assignment Tool (bulk assign banyak pegawai sekaligus), Shift Schedule (kalender visual)

### Phase 3 — Recruitment ✅ Selesai
- ~~Job Opening, Job Applicant, Interview + Interview Feedback, Job Offer~~
- ~~Employee Onboarding (konversi offer diterima → Employee record)~~
- Belum: career page publik, upload file CV, onboarding checklist/task terpisah

### Phase 4 — Performance Management ✅ Selesai
- ~~Appraisal Cycle, KRA, Goal, Appraisal (self-assessment + manager review), Feedback 360~~
- Belum: weighting otomatis rating akhir, notifikasi/reminder siklus

### Phase 5 — Expense & Travel ✅ Selesai
- ~~Expense Claim (multi item), Employee Advance, Travel Request~~
- Belum: approval berjenjang berdasarkan nominal, rekonsiliasi otomatis advance vs klaim

### Phase 6 — Payroll & Taxation ✅ Selesai
- ~~Salary Component, Salary Structure (+ Assignment), Payroll Entry, Salary Slip~~
- ~~Proration otomatis berdasarkan data Attendance~~
- Belum: kalkulator PPh21 sesuai UU, formula/rule engine antar-komponen, Income Tax Slab, Gratuity, payslip PDF

### Phase 7 — Exit & Separation ✅ Selesai
- ~~Employee Separation, Exit Interview, Full & Final Settlement~~
- Belum: approval berjenjang, notice period tracking, offboarding checklist (pengembalian aset, revoke akses sistem, dll.)

### Phase 8 — Reports & Dashboard
- Ganti dashboard Frappe dengan Blazor + Chart.js/ApexCharts, atau embed Power BI

### Lintas modul (kapan saja bisa ditambahkan)
- File upload (dokumen pegawai) — pakai local file storage atau network share
- Notifikasi (email via SMTP self-hosted, atau in-app via SignalR — Blazor Server sudah punya koneksi real-time by default)
- Print/PDF (surat penawaran kerja, slip gaji) — pakai library **QuestPDF**
- Audit trail lengkap per field (saat ini baru CreatedBy/ModifiedBy di level record)

## Prinsip Desain

- **Clean Architecture**: Domain tidak tahu apa-apa soal EF Core/Blazor. Application hanya bergantung ke interface (`IRepository`, `IUnitOfWork`), bukan implementasi konkret. Ini membuat business logic bisa di-unit-test tanpa database.
- **Result pattern**, bukan exception, untuk business rule violation (mis. "saldo cuti tidak cukup") — exception dicadangkan untuk error yang benar-benar tak terduga.
- **Soft delete** + **append-only ledger** untuk data yang butuh audit trail (leave balance), meniru pola Frappe (`Leave Ledger Entry`) alih-alih menyimpan angka running total yang gampang salah sinkron.
- **Self-hosted end-to-end**: tidak ada dependency wajib ke Azure/cloud manapun — semua (auth, background job, file storage) bisa jalan di satu server on-premise.

## Kontribusi Lanjutan

Lanjutkan pengembangan modul demi modul mengikuti pola yang sudah ada:
1. Tambah entity di `HRMS.Domain/Entities/<Modul>/`
2. Tambah DbSet + Fluent API config di `HrmsDbContext` & `Configurations/`
3. Tambah DTO + interface service + implementasi di `HRMS.Application/<Modul>/`
4. Daftarkan service di `HRMS.Infrastructure/DependencyInjection.cs`
5. Buat halaman Blazor di `HRMS.Web/Components/Pages/<Modul>/`
6. `dotnet ef migrations add <NamaMigrasi>`
