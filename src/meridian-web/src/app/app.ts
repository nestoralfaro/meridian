import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterOutlet } from '@angular/router';
import { catchError, of } from 'rxjs';
import { BuildInfoService } from './core/build-info/build-info.service';

@Component({
  selector: 'mer-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  /** Build info is decorative: if the API is unavailable the footer simply omits it. */
  private readonly buildInfo = toSignal(
    inject(BuildInfoService)
      .get()
      .pipe(catchError(() => of(null))),
    { initialValue: null },
  );

  protected readonly shortCommit = computed(() => this.buildInfo()?.commit.slice(0, 7) ?? null);
}
