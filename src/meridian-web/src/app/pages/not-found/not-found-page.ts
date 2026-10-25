import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'mer-not-found-page',
  imports: [RouterLink],
  template: `
    <h1>Page not found</h1>
    <p>The page you're looking for doesn't exist. <a routerLink="/">Return home</a></p>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotFoundPage {}
