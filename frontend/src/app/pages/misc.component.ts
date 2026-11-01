import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-forbidden', standalone: true, imports: [RouterLink],
  template: `<div class="text-center py-5"><h1 class="h3">Недостаточно прав</h1><p class="text-muted">У вашей роли нет доступа к этой странице.</p><a class="btn btn-primary" routerLink="/">На главную</a></div>`
})
export class ForbiddenComponent {}

@Component({
  selector: 'app-not-found', standalone: true, imports: [RouterLink],
  template: `<div class="text-center py-5"><h1 class="h3">Страница не найдена</h1><p class="text-muted">Проверьте адрес или вернитесь на главную.</p><a class="btn btn-primary" routerLink="/">На главную</a></div>`
})
export class NotFoundComponent {}
