import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { App } from './app';

describe('App', () => {
  let fixture: ComponentFixture<App>;
  let http: HttpTestingController;
  const first = { rank: 3, title: 'One two three four five six', points: 10, commentCount: 7 };
  const second = { rank: 1, title: 'A short title', points: 30, commentCount: 2 };
  const third = { rank: 2, title: 'Another title with more than five words', points: 5, commentCount: 10 };
  const data = { entries: [first, second, third], filterA: [third, first], filterB: [second] };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(App);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  function respond(response = data): void {
    const request = http.expectOne('/api/entries?filter=both');
    expect(request.request.method).toBe('GET');
    request.flush(response);
    fixture.detectChanges();
  }

  function button(text: string): HTMLButtonElement {
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(button => button.textContent?.trim() === text)!;
  }

  function titles(): string[] {
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('h2'))
      .map(element => element.textContent ?? '');
  }

  it('shows loading, then renders the title and all entry fields', () => {
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('[role="status"]')?.textContent).toContain('Loading stories');
    expect(button('Loading…').disabled).toBe(true);
    respond();
    expect(element.querySelector('h1')?.textContent).toBe('Hacker News Explorer');
    expect(titles()).toEqual(data.entries.map(entry => entry.title));
    expect(element.querySelector('.rank')?.textContent).toBe('3.');
    expect(element.querySelector('.metadata')?.textContent).toContain('10 points');
    expect(element.querySelector('.metadata')?.textContent).toContain('7 comments');
    expect(button('Refresh').disabled).toBe(false);
  });

  it('switches between backend results without downloading or sorting again', () => {
    respond();
    button('Filter A').click();
    fixture.detectChanges();
    expect(titles()).toEqual([third.title, first.title]);
    expect(button('Filter A').getAttribute('aria-pressed')).toBe('true');
    button('Filter B').click();
    fixture.detectChanges();
    expect(titles()).toEqual([second.title]);
    button('All stories').click();
    fixture.detectChanges();
    expect(titles()).toEqual(data.entries.map(entry => entry.title));
    http.expectNone('/api/entries?filter=both');
  });

  it('shows an empty state for an empty filter result', () => {
    respond({ ...data, filterA: [] });
    button('Filter A').click();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('[role="status"]')?.textContent)
      .toContain('No stories match this view.');
  });

  it('refreshes once and clears the previous snapshot while loading', () => {
    respond();
    button('Refresh').click();
    fixture.componentInstance.load();
    fixture.detectChanges();
    expect(titles()).toEqual([]);
    respond({ entries: [second], filterA: [], filterB: [second] });
    expect(titles()).toEqual([second.title]);
  });

  it.each([
    [502, 'Hacker News is unavailable'],
    [503, 'Filter usage could not be saved'],
    [504, 'Hacker News took too long'],
    [500, 'Check that the API is running'],
    [0, 'Check that the API is running']
  ])('shows a useful message for error %s', (status, message) => {
    const request = http.expectOne('/api/entries?filter=both');
    if (status === 0) request.error(new ProgressEvent('error'));
    else request.flush({ detail: 'Internal diagnostics must not appear' }, { status, statusText: 'Error' });
    fixture.detectChanges();
    const alert = (fixture.nativeElement as HTMLElement).querySelector('[role="alert"]');
    expect(alert?.textContent).toContain(message);
    expect(alert?.textContent).not.toContain('Internal diagnostics');
    expect(button('Refresh').disabled).toBe(false);
  });

  it('retries successfully after an error', () => {
    http.expectOne('/api/entries?filter=both').flush({}, { status: 503, statusText: 'Unavailable' });
    fixture.detectChanges();
    button('Try again').click();
    respond();
    expect((fixture.nativeElement as HTMLElement).querySelector('[role="alert"]')).toBeNull();
    expect(titles()).toHaveLength(3);
  });

  it('renders story titles as text, not executable HTML', () => {
    const entry = { ...second, title: '<img src=x onerror=alert(1)>' };
    respond({ entries: [entry], filterA: [], filterB: [entry] });
    expect(titles()).toEqual([entry.title]);
    expect((fixture.nativeElement as HTMLElement).querySelector('article img')).toBeNull();
  });

  it('cancels an outstanding request when destroyed', () => {
    const request = http.expectOne('/api/entries?filter=both');
    fixture.destroy();
    expect(request.cancelled).toBe(true);
  });
});
