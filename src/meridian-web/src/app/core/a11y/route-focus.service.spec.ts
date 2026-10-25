import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { RouteFocusService } from './route-focus.service';

@Component({ template: '<main id="main-content" tabindex="-1"><h1>First</h1></main>' })
class FirstPage {}

@Component({ template: '<main id="main-content" tabindex="-1"><h1>Second</h1></main>' })
class SecondPage {}

describe('RouteFocusService', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: '', component: FirstPage },
          { path: 'second', component: SecondPage },
        ]),
      ],
    });
  });

  it('leaves focus alone on the first page load', async () => {
    const harness = await RouterTestingHarness.create();
    TestBed.inject(RouteFocusService).start();

    await harness.navigateByUrl('/');

    expect(document.activeElement).toBe(document.body);
  });

  it('moves focus to the new page heading after client-side navigation', async () => {
    const harness = await RouterTestingHarness.create();
    TestBed.inject(RouteFocusService).start();
    await harness.navigateByUrl('/');

    await TestBed.inject(Router).navigateByUrl('/second');
    harness.detectChanges();
    await harness.fixture.whenStable();

    const focused = document.activeElement as HTMLElement | null;
    expect(focused?.tagName).toBe('H1');
    expect(focused?.textContent).toBe('Second');
    expect(focused?.getAttribute('tabindex')).toBe('-1');
  });
});
