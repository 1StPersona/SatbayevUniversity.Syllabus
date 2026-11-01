import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ApiService } from '../core/api.service';
import { ReferenceDto, STATUS_TITLES, SyllabusDetails } from '../core/models';
import { SyllabusDocumentComponent } from './syllabus-document.component';

/** Печатная форма A4: «Печать / Сохранить как PDF» средствами браузера. */
@Component({
  selector: 'app-syllabus-print',
  standalone: true,
  imports: [SyllabusDocumentComponent],
  template: `
  @if (s(); as s) { @if (ref(); as r) {
    <div class="no-print bg-light border-bottom py-2 mb-3">
      <div class="container d-flex gap-2 align-items-center">
        <button class="btn btn-primary btn-sm" (click)="print()">Печать / Сохранить как PDF</button>
        <button class="btn btn-outline-secondary btn-sm" (click)="api.docx(s.id)">Скачать DOCX</button>
        @if (s.status !== 'Approved') { <span class="badge text-bg-warning ms-2">Проект — силлабус ещё не утверждён ({{ titles[s.status] }})</span> }
      </div>
    </div>
    <div class="container print-page">
      <div class="print-header mb-3">{{ r.settings.universityName }}</div>
      <app-syllabus-document [syllabus]="s" [ref]="r" />
    </div>
  } }`
})
export class SyllabusPrintComponent implements OnInit {
  api = inject(ApiService);
  private route = inject(ActivatedRoute);
  s = signal<SyllabusDetails | null>(null);
  ref = signal<ReferenceDto | null>(null);
  titles = STATUS_TITLES;

  async ngOnInit() {
    const [s, r] = await Promise.all([this.api.syllabus(Number(this.route.snapshot.paramMap.get('id'))), this.api.reference()]);
    this.s.set(s);
    this.ref.set(r);
    document.title = `Силлабус ${s.discipline.code} ${s.data.academicYear}`;
  }
  print() { window.print(); }
}
