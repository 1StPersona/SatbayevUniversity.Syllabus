// Контракты API SatbayevUniversity.Syllabus (см. Contracts/Dtos.cs).

export type SyllabusStatus = 'Draft' | 'OnDepartmentReview' | 'Returned' | 'DepartmentApproved' | 'Approved' | 'Archived';
export type WorkflowAction = 'Submit' | 'Withdraw' | 'DepartmentApprove' | 'Return' | 'Approve' | 'Archive';
export type RequisiteKind = 'Prerequisite' | 'Postrequisite';
export type CalendarRowKind = 'Week' | 'Milestone';
export type LiteratureKind = 'Main' | 'Additional';
export type DisciplineCycle = 'OOD' | 'BD' | 'PD';
export type StudyLevel = 'Bachelor' | 'Master' | 'Doctoral';
export type MaterialReviewStatus = 'Pending' | 'Accepted' | 'Rejected';

export const ROLES = ['Admin', 'Director', 'HeadOfDepartment', 'Teacher', 'Student'] as const;
export type Role = typeof ROLES[number];
export const ROLE_TITLES: Record<string, string> = {
  Admin: 'Администратор', Director: 'Директор института', HeadOfDepartment: 'Заведующий кафедрой',
  Teacher: 'Преподаватель', Student: 'Обучающийся'
};
export const STATUS_TITLES: Record<SyllabusStatus, string> = {
  Draft: 'Черновик', OnDepartmentReview: 'На рассмотрении кафедры', Returned: 'Возвращён на доработку',
  DepartmentApproved: 'Одобрен кафедрой', Approved: 'Утверждён', Archived: 'В архиве'
};
export const CYCLE_TITLES: Record<DisciplineCycle, string> = { OOD: 'ООД', BD: 'БД', PD: 'ПД' };
export const LEVEL_TITLES: Record<StudyLevel, string> = { Bachelor: 'Бакалавриат', Master: 'Магистратура', Doctoral: 'Докторантура' };

export interface UserDto {
  id: string; email: string; fullName: string; regalia?: string | null; roles: string[];
  departmentId?: number | null; instituteId?: number | null; educationalProgramId?: number | null;
}
export interface LoginResponse { token: string; expiresAt: string; user: UserDto; }

export interface InstituteDto { id: number; name: string; shortName: string; directorName: string; }
export interface DepartmentDto { id: number; name: string; headName: string; headTitle: string; instituteId: number; }
export interface ProgramDto { id: number; code: string; name: string; level: StudyLevel; departmentId: number; }
export interface DisciplineDto {
  id: number; code: string; name: string; cycle: DisciplineCycle; credits: number; creditStructure: string;
  departmentId: number; departmentName?: string | null;
}
export interface GradeScaleDto { letter: string; gpa: number; minPercent: number; maxPercent: number; criterion: string; traditional: string; }
export interface UmkdTypeDto {
  id: number; name: string; description: string; isRequired: boolean; order: number; isSyllabus: boolean; visibleToStudentsByDefault: boolean;
}
export interface SettingsDto {
  universityName: string; universityShortName: string; formCode: string; defaultCriteriaNote: string;
  defaultLatePolicy: string; defaultAcademicPolicy: string; acquaintanceNote: string;
}
export interface ReferenceDto {
  settings: SettingsDto; gradeScale: GradeScaleDto[]; competencyDescriptors: string[]; competencyCategories: string[];
  criteriaIntro: string; criteriaRows: string[][]; disciplines: DisciplineDto[]; programs: ProgramDto[];
  umkdTypes: UmkdTypeDto[]; statuses: { value: SyllabusStatus; name: string }[];
}

export interface TeacherInfo { fullName: string; regalia: string; office: string; officeHours: string; email: string; }
export interface RequisiteRow { kind: RequisiteKind; code?: string | null; name?: string | null; }
export interface CalendarRowModel {
  kind: CalendarRowKind; week?: number | null; lectureTopic?: string | null; practiceTopic?: string | null;
  sroTopic?: string | null; literatureRefs?: string | null; deadline?: string | null;
}
export interface LiteratureRow { number: number; kind: LiteratureKind; text?: string | null; }
export interface AssessmentRow {
  name?: string | null; maxPerWeek?: string | null; isFinal: boolean; finalPoints?: number | null; weeks: (number | null)[];
}
export interface SyllabusEditModel {
  id: number; disciplineId: number; academicYear: string; course: number; semester: number; studyForm: string;
  language: string; city: string; weeksCount: number; programIds: number[];
  lecturer: TeacherInfo; practiceTeacher: TeacherInfo; requisites: RequisiteRow[];
  goal: string; tasks: string; description: string; outcomesKnow: string; outcomesCan: string; outcomesSkills: string;
  calendarRows: CalendarRowModel[]; literature: LiteratureRow[]; competencies: (string | null)[]; assessments: AssessmentRow[];
  criteriaNote: string; latePolicy: string; academicPolicy: string;
  headOfDepartmentName: string; headOfDepartmentTitle: string; compilerName: string; agreedBy: string;
}

export interface UmkdProgressDto { done: number; total: number; percent: number; isComplete: boolean; }
export interface SyllabusListItem {
  id: number; disciplineCode: string; disciplineName: string; credits: number; academicYear: string; course: number;
  semester: number; programs: string[]; lecturer: string; status: SyllabusStatus; statusName: string; updatedAt: string;
  isMine: boolean; umkd?: UmkdProgressDto | null;
}
export interface IssueDto { severity: 'error' | 'warning'; section: string; message: string; }
export interface HistoryDto { at: string; action: string; toStatus: SyllabusStatus; statusName: string; userName: string; comment?: string | null; }
export interface SyllabusDetails {
  id: number; data: SyllabusEditModel; discipline: DisciplineDto; departmentName: string; instituteName: string;
  instituteShortName: string; directorName: string; programs: ProgramDto[]; status: SyllabusStatus; statusName: string;
  protocolNumber?: string | null; protocolDate?: string | null; approvedByName?: string | null; approvedByTitle?: string | null;
  approvedDate?: string | null; authorName: string; createdAt: string; updatedAt: string; ratingTotal: number; finalTotal: number;
  returnComment?: string | null;
  permissions: { canEdit: boolean; canDelete: boolean; canClone: boolean; canUploadMaterials: boolean; canReviewMaterials: boolean };
  actions: { action: WorkflowAction; title: string }[]; issues: IssueDto[]; history: HistoryDto[]; umkd: UmkdProgressDto;
}

export interface MaterialDto {
  id: number; componentTypeId: number; title: string; description?: string | null; fileName?: string | null; fileSize: number;
  externalUrl?: string | null; hasFile: boolean; visibleToStudents: boolean; reviewStatus: MaterialReviewStatus;
  reviewStatusName: string; reviewComment?: string | null; uploadedByName: string; uploadedAt: string; canDelete: boolean;
}
export interface UmkdComponentDto { type: UmkdTypeDto; isComplete: boolean; stateText: string; materials: MaterialDto[]; }
export interface UmkdDto { syllabusId: number; progress: UmkdProgressDto; canUpload: boolean; canReview: boolean; components: UmkdComponentDto[]; }
export interface UmkdOverviewRow {
  syllabusId: number; disciplineCode: string; disciplineName: string; lecturer: string; status: SyllabusStatus;
  statusName: string; progress: UmkdProgressDto; cells: ('done' | 'pending' | 'rejected' | 'missing' | 'optional')[]; canUpload: boolean;
}
export interface UmkdOverviewDto { years: string[]; year?: string | null; types: UmkdTypeDto[]; rows: UmkdOverviewRow[]; }

export interface DashboardDto {
  byStatus: Record<string, number>; mine: SyllabusListItem[]; awaitingMe: SyllabusListItem[];
  materialsToReview: number; published: SyllabusListItem[];
}

export interface AdminUserDto {
  id: string; email: string; fullName: string; regalia?: string | null; roles: string[]; departmentId?: number | null;
  departmentName?: string | null; instituteId?: number | null; educationalProgramId?: number | null; programCode?: string | null;
}
export interface SaveUserRequest {
  email: string; fullName: string; regalia?: string | null; roles: string[]; departmentId?: number | null;
  instituteId?: number | null; educationalProgramId?: number | null; password?: string | null;
}
