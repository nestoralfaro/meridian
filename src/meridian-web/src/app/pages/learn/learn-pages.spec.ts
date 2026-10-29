import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { ArticlePage } from './article-page';
import { LearnPage } from './learn-page';

const summary = {
  slug: 'asset-allocation',
  title: 'What is asset allocation?',
  summary: 'How the mix shapes growth.',
  published: '2026-10-29',
  readingMinutes: 2,
};

describe('Learn center', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function settle(fixture: { detectChanges(): void }) {
    fixture.detectChanges();
    await TestBed.inject(ApplicationRef).whenStable();
    fixture.detectChanges();
  }

  it('lists articles with links, reading time and date', async () => {
    const fixture = TestBed.createComponent(LearnPage);
    fixture.detectChanges();
    TestBed.tick();
    http.expectOne('/api/v1/learn/articles').flush({ items: [summary] });
    await settle(fixture);

    const element = fixture.nativeElement as HTMLElement;
    const link = element.querySelector<HTMLAnchorElement>('h2 a')!;
    expect(link.textContent).toContain('What is asset allocation?');
    expect(link.getAttribute('href')).toBe('/learn/asset-allocation');
    expect(element.textContent).toContain('2 min read');
    expect(element.querySelector('time')?.textContent).toContain('Oct 29, 2026');
  });

  it('renders an article and strips anything unsafe that slipped through', async () => {
    const fixture = TestBed.createComponent(ArticlePage);
    fixture.componentRef.setInput('slug', 'asset-allocation');
    fixture.detectChanges();
    TestBed.tick();
    http.expectOne('/api/v1/learn/articles/asset-allocation').flush({
      summary,
      html: '<h2 id="x">The short version</h2><p>Hi<img src="x" onerror="alert(1)"></p>',
    });
    await settle(fixture);

    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('h1')?.textContent).toContain('What is asset allocation?');
    expect(element.querySelector('.prose h2')?.textContent).toBe('The short version');
    expect(element.querySelector('.prose img')?.getAttribute('onerror')).toBeNull(); // Angular's sanitizer, as a second layer
  });

  it('says when an article does not exist', async () => {
    const fixture = TestBed.createComponent(ArticlePage);
    fixture.componentRef.setInput('slug', 'fees-draft');
    fixture.detectChanges();
    TestBed.tick();
    http
      .expectOne('/api/v1/learn/articles/fees-draft')
      .flush({ status: 404 }, { status: 404, statusText: 'Not Found' });
    await settle(fixture);

    expect((fixture.nativeElement as HTMLElement).querySelector('h1')?.textContent).toContain(
      'Article not found',
    );
  });
});
