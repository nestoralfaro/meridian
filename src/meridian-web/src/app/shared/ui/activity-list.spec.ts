import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from '../../core/auth/auth.service';
import { ActivityList } from './activity-list';

const usd = (amount: number) => ({ amount, currency: 'USD' });
const items = [
  {
    id: '1',
    date: '2026-10-24',
    accountName: 'Individual Brokerage',
    description: 'Bought MDTOT',
    type: 'Buy',
    amount: usd(-490.6),
  },
  {
    id: '2',
    date: '2026-10-02',
    accountName: 'Roth IRA',
    description: 'Roth IRA contribution',
    type: 'Deposit',
    amount: usd(250),
  },
];

describe('ActivityList', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: AuthService,
          useValue: {
            user: signal({ displayName: 'Dana Lee', email: 'dana@meridian.test', isDemo: true }),
          },
        },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function render(limit: number, response: object): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(ActivityList);
    fixture.componentRef.setInput('limit', limit);
    fixture.detectChanges();
    TestBed.tick();
    const request = http.expectOne((r) => r.url === '/api/v1/activity');
    expect(request.request.params.get('limit')).toBe(String(limit));
    request.flush(response);
    await TestBed.inject(ApplicationRef).whenStable();
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('lists transactions in the order received (newest first) with date, account and signed amount', async () => {
    const page = await render(5, { items });

    const rows = Array.from(page.querySelectorAll('li')).map((li) =>
      li.textContent?.replace(/\s+/g, ' ').trim(),
    );
    expect(rows[0]).toContain('Oct 24, 2026');
    expect(rows[0]).toContain('Bought MDTOT');
    expect(rows[0]).toContain('Individual Brokerage');
    expect(rows[0]).toContain('−$490.60');
    expect(rows[1]).toContain('+$250.00');
    expect(page.querySelector('time')?.getAttribute('datetime')).toBe('2026-10-24');
  });

  it('explains that transactions will appear here when there are none', async () => {
    const page = await render(5, { items: [] });

    expect(page.textContent).toContain('Your transactions will appear here');
  });
});
