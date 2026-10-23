import { TestBed } from '@angular/core/testing';
import { App } from './app';

describe('App', () => {
  it('shows the application name and initial state', async () => {
    await TestBed.configureTestingModule({ imports: [App] }).compileComponents();
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('h1')?.textContent).toBe('Hacker News Crawler');
    expect(element.querySelector('section')?.textContent).toContain('Story retrieval and filtering will be added next.');
  });
});
