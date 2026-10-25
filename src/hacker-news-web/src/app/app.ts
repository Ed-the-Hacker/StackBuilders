import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

interface Entry {
  rank: number;
  title: string;
  points: number;
  commentCount: number;
  url: string;
}

interface EntriesResponse {
  entries: Entry[];
  filterA: Entry[];
  filterB: Entry[];
}

type View = 'all' | 'a' | 'b';

@Component({
  selector: 'app-root',
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);
  readonly data = signal<EntriesResponse | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly view = signal<View>('all');
  readonly entries = computed(() => {
    const data = this.data();
    if (!data) return [];
    return this.view() === 'a' ? data.filterA : this.view() === 'b' ? data.filterB : data.entries;
  });
  readonly description = computed(() => this.view() === 'a'
    ? 'More than five words in the title · Most comments first'
    : this.view() === 'b'
      ? 'Five words or fewer in the title · Most points first'
      : 'The first 30 stories, in their original order');

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    if (this.loading()) return;
    this.loading.set(true);
    this.error.set('');
    this.data.set(null);
    this.http.get<EntriesResponse>('/api/entries?filter=both')
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: data => {
          this.data.set(data);
          this.loading.set(false);
        },
        error: (response: HttpErrorResponse) => {
          const messages: Record<number, string> = {
            502: 'Hacker News is unavailable or returned an unexpected page. Please try again.',
            503: 'Filter usage could not be saved. Please try again later.',
            504: 'Hacker News took too long to respond. Please try again.'
          };
          this.error.set(messages[response.status] ?? 'Could not load stories. Check that the API is running and try again.');
          this.loading.set(false);
        }
      });
  }
}
