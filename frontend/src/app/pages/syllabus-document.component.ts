import { Component, computed, input } from '@angular/core';
import { CalendarRowModel, CYCLE_TITLES, GradeScaleDto, ReferenceDto, SyllabusDetails } from '../core/models';
import { fmt } from '../shared/ui';

/** Силлабус в виде документа по форме Ф КазНИТУ 703-08 (просмотр и печать). */
@Component({
  selector: 'app-syllabus-document',
  standalone: true,
  template: `
  @let s = syllabus(); @let d = s.data; @let r = ref();
  <article class="syllabus-doc">
    <section class="title-page">
      <div class="tp-university">{{ r.settings.universityShortName }}</div>
      <div class="fw-bold">{{ s.instituteName }}</div>
      <div class="fw-bold">Кафедра «{{ s.departmentName }}»</div>
      <div class="tp-approve">
        <div class="fw-bold">УТВЕРЖДАЮ</div>
        <div>Директор института {{ s.instituteShortName }}</div>
        <div>_______________ {{ s.approvedByName ?? f.shortName(s.directorName) }}</div>
        <div>{{ approvedLine() }}</div>
      </div>
      <div class="tp-center">
        <div class="tp-title">СИЛЛАБУС</div>
        <div class="fw-bold">{{ s.discipline.code }} {{ s.discipline.name }}</div>
        <div class="my-3">@for (p of s.programs; track p.id) { <div class="fw-bold">{{ p.code }} «{{ p.name }}»</div> }</div>
        <div>Цикл дисциплины {{ cycles[s.discipline.cycle] }}, {{ s.discipline.credits }} ({{ s.discipline.creditStructure }}) {{ f.creditWord(s.discipline.credits) }}</div>
        <div class="mt-2">Курс {{ d.course }}, семестр {{ d.semester }}</div>
        <div class="mt-2">{{ d.academicYear }} учебный год</div>
      </div>
      <div class="tp-city fw-bold">{{ d.city }} {{ d.academicYear.split('-')[0] }}</div>
    </section>

    <h2>1. Информация о преподавателе</h2>
    @for (t of teachers(); track t.title) {
      <h3>{{ t.title }}</h3>
      <p class="indent">{{ t.info.fullName }}{{ t.info.regalia ? ', ' + t.info.regalia : '' }}</p>
      <div class="row teacher-contacts">
        <div class="col-6">{{ t.info.office ? 'офис: ' + t.info.office : '' }}</div>
        <div class="col-6">
          @if (t.info.officeHours) { <div>Офис-часы: {{ t.info.officeHours }}</div> }
          @if (t.info.email) { <div>e-mail: <a [href]="'mailto:' + t.info.email">{{ t.info.email }}</a></div> }
        </div>
      </div>
    }
    <p class="indent fw-bold">*Форма обучения – <u>{{ d.studyForm }}</u></p>

    <h2>2. Пререквизиты и постреквизиты</h2>
    <table class="doc-table">
      <tr><td style="width:28%">Пререквизиты</td><td>{{ requisites('Prerequisite') }}</td></tr>
      <tr><td>Постреквизиты</td><td>{{ requisites('Postrequisite') }}</td></tr>
    </table>

    <h2>3. Цель и задачи дисциплины</h2>
    <p class="indent"><strong>Цель:</strong> {{ d.goal }}</p>
    <p class="indent mb-0"><strong>Задачи:</strong></p>
    <ul class="dash">@for (l of f.lines(d.tasks); track $index) { <li>{{ l }}</li> }</ul>

    <h2>4. Описание курса:</h2>
    @for (p of f.paragraphs(d.description); track $index) { <p class="indent">{{ p }}</p> }

    <h2>5. Результаты обучения</h2>
    <p class="indent mb-0">После завершения курса обучающийся должен:</p>
    @for (o of outcomes(); track o[0]) {
      <p class="indent mb-0"><strong>{{ o[0] }}</strong></p>
      <ul class="dash">@for (l of f.lines(o[1]); track $index) { <li>{{ l }}</li> }</ul>
    }

    <h2>6. Календарно-тематический план</h2>
    <div class="table-responsive"><table class="doc-table">
      <thead><tr><th>Неделя</th><th>Тема лекции</th><th>Тема практической работы</th><th>СРО/СРОП</th><th>Ссылка на литературу</th><th>Срок сдачи</th></tr></thead>
      <tbody>
        @for (row of d.calendarRows; track $index) {
          @if (row.kind === 'Milestone') {
            <tr class="milestone"><td></td><td colspan="3" class="text-center fw-bold">{{ row.lectureTopic }}</td><td></td><td class="text-center">{{ row.deadline }}</td></tr>
          } @else {
            <tr><td class="text-center">{{ row.week }}</td><td>{{ row.lectureTopic }}</td><td>{{ row.practiceTopic }}</td><td>{{ row.sroTopic }}</td><td class="text-center">{{ row.literatureRefs }}</td><td class="text-center">{{ row.deadline }}</td></tr>
          }
        }
      </tbody>
    </table></div>

    <h2>7. Литература</h2>
    <table class="doc-table">
      <thead><tr><th style="width:50%">Основная литература</th><th>Дополнительная литература</th></tr></thead>
      <tbody>@for (pair of literature(); track $index) { <tr><td class="pre-line">{{ pair[0] }}</td><td class="pre-line">{{ pair[1] }}</td></tr> }</tbody>
    </table>

    <h2>8. Рамка компетенций</h2>
    <div class="table-responsive"><table class="doc-table small-table">
      <thead>
        <tr><th rowspan="2">Дескрипторы обучения</th><th [attr.colspan]="r.competencyCategories.length">Компетенции</th></tr>
        <tr>@for (c of r.competencyCategories; track c) { <th>{{ c }}</th> }</tr>
      </thead>
      <tbody>
        @for (desc of r.competencyDescriptors; track desc; let di = $index) {
          <tr><td>{{ desc }}</td>
            @for (c of r.competencyCategories; track c; let ci = $index) { <td>{{ d.competencies[di * r.competencyCategories.length + ci] }}</td> }
          </tr>
        }
      </tbody>
    </table></div>

    <h2>9. График сдачи требуемых работ</h2>
    <div class="table-responsive"><table class="doc-table small-table assessment-table">
      <thead>
        <tr><th rowspan="2">№ п/п</th><th rowspan="2">Виды контроля</th><th rowspan="2">Макс балл недели</th><th [attr.colspan]="d.weeksCount">Недели</th><th rowspan="2">Итого макс баллов</th></tr>
        <tr>@for (w of weeks(); track w) { <th>{{ w }}</th> }</tr>
      </thead>
      <tbody>
        @for (a of d.assessments; track $index; let i = $index) {
          <tr>
            <td class="text-center">{{ i + 1 }}</td><td>{{ a.name }}{{ a.isFinal ? '*' : '' }}</td><td class="text-center">{{ a.maxPerWeek }}</td>
            @if (a.isFinal) {
              @for (w of weeks(); track w) { <td></td> }
              <td class="text-center fw-bold">{{ f.num(a.finalPoints) }}</td>
            } @else {
              @for (w of weeks(); track w) { <td class="text-center">{{ f.num(a.weeks[w - 1]) }}</td> }
              @if (i === 0) { <td [attr.rowspan]="ratingRows()" class="text-center fw-bold align-middle">{{ f.num(s.ratingTotal) }}</td> }
            }
          </tr>
        }
        <tr class="fw-bold"><td></td><td>Итого</td><td></td>@for (w of weeks(); track w) { <td></td> }<td class="text-center">{{ f.num(s.ratingTotal + s.finalTotal) }}</td></tr>
      </tbody>
    </table></div>

    <h2>10. Оценивания итогового контроля</h2>
    <table class="doc-table small-table">
      <thead><tr><th>Оценка по буквенной системе</th><th>Цифровой эквивалент баллов</th><th>Процентное содержание</th><th>Критерий</th><th>Оценка по традиционной системе</th></tr></thead>
      <tbody>
        @for (g of scale(); track g.letter) {
          <tr><td class="text-center">{{ g.letter }}</td><td class="text-center">{{ f.num(g.gpa) }}</td><td class="text-center">{{ g.minPercent }}-{{ g.maxPercent }}</td><td>{{ g.criterion }}</td>
            @if (g.span) { <td [attr.rowspan]="g.span" class="text-center align-middle">{{ g.traditional }}</td> }</tr>
        }
      </tbody>
    </table>

    <h2>11. Критерии оценивания</h2>
    @for (p of f.paragraphs(r.criteriaIntro); track $index) { <p class="indent mb-0">{{ p }}</p> }
    <table class="doc-table small-table mt-2">
      <thead><tr><th>Критерии</th><th>Отлично (0.9-1.0)</th><th>Хорошо (0.7-0.9)</th><th>Удовлетворительно (0.4-0.7)</th><th>Неудовлетворительно (0-0.4)</th></tr></thead>
      <tbody>@for (row of r.criteriaRows; track $index) { <tr>@for (c of row; track $index) { <td>{{ c }}</td> }</tr> }</tbody>
    </table>
    <p class="indent mb-0 mt-2">Общая оценка будет рассчитана по формуле:</p>
    <p class="indent fw-bold fst-italic">Оценка = (А + Т + З)×О</p>
    @for (p of f.paragraphs(d.criteriaNote); track $index) { <p class="indent">{{ p }}</p> }

    <h2>12. Политика поздней сдачи работ</h2>
    @for (p of f.paragraphs(d.latePolicy); track $index) { <p class="indent">{{ p }}</p> }

    <h2>13. Политика академического поведения и этики</h2>
    @for (p of f.paragraphs(d.academicPolicy); track $index) { <p class="indent">{{ p }}</p> }
    @if (r.settings.acquaintanceNote) { <p class="indent">{{ r.settings.acquaintanceNote }}</p> }

    <section class="signatures">
      <p class="indent">Рассмотрено и одобрено на заседании кафедры «{{ s.departmentName }}» {{ protocolLine() }}</p>
      <table class="sign-table">
        <tr><td class="fw-bold">{{ d.headOfDepartmentTitle }}</td><td class="sign-line"></td><td class="fw-bold">{{ d.headOfDepartmentName }}</td></tr>
        <tr><td class="fw-bold">Составитель</td><td class="sign-line"></td><td class="fw-bold">{{ d.compilerName }}</td></tr>
      </table>
      @if (f.lines(d.agreedBy).length) {
        <p class="fw-bold mt-4 mb-2">Согласовано:</p>
        @for (n of f.lines(d.agreedBy); track $index; let i = $index) { <p class="mb-2">{{ i + 1 }}. _______________ <strong>{{ n }}</strong></p> }
      }
    </section>
    <div class="doc-footer">{{ r.settings.formCode }}. Силлабус</div>
  </article>`
})
export class SyllabusDocumentComponent {
  syllabus = input.required<SyllabusDetails>();
  ref = input.required<ReferenceDto>();
  f = fmt;
  cycles = CYCLE_TITLES;

  weeks = computed(() => Array.from({ length: this.syllabus().data.weeksCount }, (_, i) => i + 1));
  ratingRows = computed(() => this.syllabus().data.assessments.filter(a => !a.isFinal).length);
  teachers = computed(() => [
    { title: '1.1 Лектор:', info: this.syllabus().data.lecturer },
    { title: '1.2 Преподаватель, ведущий практические/лабораторные занятия', info: this.syllabus().data.practiceTeacher }
  ]);
  outcomes = computed(() => {
    const d = this.syllabus().data;
    return [['Знать:', d.outcomesKnow], ['Уметь:', d.outcomesCan], ['Владеть навыками:', d.outcomesSkills]];
  });
  literature = computed(() => {
    const lit = [...this.syllabus().data.literature].sort((a, b) => a.number - b.number);
    const main = lit.filter(l => l.kind === 'Main'), add = lit.filter(l => l.kind === 'Additional');
    return Array.from({ length: Math.max(main.length, add.length) }, (_, i) => [
      main[i] ? `[${main[i].number}] ${main[i].text}` : '', add[i] ? `[${add[i].number}] ${add[i].text}` : ''
    ]);
  });
  scale = computed(() => {
    const rows = this.ref().gradeScale;
    return rows.map((g: GradeScaleDto, i) => ({
      ...g,
      span: i > 0 && rows[i - 1].traditional === g.traditional ? 0 : rows.slice(i).findIndex(x => x.traditional !== g.traditional) < 0
        ? rows.length - i : rows.slice(i).findIndex(x => x.traditional !== g.traditional)
    }));
  });

  requisites(kind: 'Prerequisite' | 'Postrequisite') {
    return this.syllabus().data.requisites.filter(r => r.kind === kind)
      .map(r => r.code ? `${r.code} - ${r.name}` : r.name).join('; ');
  }
  approvedLine() {
    const d = fmt.longDate(this.syllabus().approvedDate);
    return d ? `«${d.day}» ${d.month} ${d.year} г.` : '«____» ______________ 20___ г.';
  }
  protocolLine() {
    const s = this.syllabus();
    const d = fmt.longDate(s.protocolDate);
    return d ? `протокол № ${s.protocolNumber} от «${d.day}» ${d.month} ${d.year} г.` : 'протокол № ____ от «____» ____________ 20___ г.';
  }
}

export type { CalendarRowModel };
