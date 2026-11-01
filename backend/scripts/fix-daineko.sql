-- Исправление фамилии «Дайненко» → «Дайнеко» в уже заполненной базе (демо-данные).
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
UPDATE AspNetUsers SET FullName = REPLACE(FullName, N'Дайненко', N'Дайнеко') WHERE FullName LIKE N'%Дайненко%';
PRINT CONCAT(N'Пользователи: ', @@ROWCOUNT);
UPDATE Institutes  SET DirectorName = REPLACE(DirectorName, N'Дайненко', N'Дайнеко') WHERE DirectorName LIKE N'%Дайненко%';
PRINT CONCAT(N'Институты: ', @@ROWCOUNT);
UPDATE Syllabi SET
    LecturerFullName        = REPLACE(LecturerFullName,        N'Дайненко', N'Дайнеко'),
    PracticeTeacherFullName = REPLACE(PracticeTeacherFullName, N'Дайненко', N'Дайнеко'),
    CompilerName            = REPLACE(CompilerName,            N'Дайненко', N'Дайнеко'),
    HeadOfDepartmentName    = REPLACE(HeadOfDepartmentName,    N'Дайненко', N'Дайнеко'),
    ApprovedByName          = REPLACE(ApprovedByName,          N'Дайненко', N'Дайнеко'),
    AgreedBy                = REPLACE(AgreedBy,                N'Дайненко', N'Дайнеко')
WHERE LecturerFullName LIKE N'%Дайненко%' OR PracticeTeacherFullName LIKE N'%Дайненко%' OR CompilerName LIKE N'%Дайненко%'
   OR HeadOfDepartmentName LIKE N'%Дайненко%' OR ApprovedByName LIKE N'%Дайненко%' OR AgreedBy LIKE N'%Дайненко%';
PRINT CONCAT(N'Силлабусы: ', @@ROWCOUNT);
UPDATE ApprovalLog SET UserName = REPLACE(UserName, N'Дайненко', N'Дайнеко') WHERE UserName LIKE N'%Дайненко%';
PRINT CONCAT(N'История согласования: ', @@ROWCOUNT);
UPDATE UmkdMaterials SET UploadedByName = REPLACE(UploadedByName, N'Дайненко', N'Дайнеко') WHERE UploadedByName LIKE N'%Дайненко%';
PRINT CONCAT(N'Материалы УМКД: ', @@ROWCOUNT);
