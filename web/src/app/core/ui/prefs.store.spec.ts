import { TestBed } from '@angular/core/testing';
import { PrefsStore } from './prefs.store';

describe('PrefsStore sidebar state', () => {
  beforeEach(() => localStorage.clear());

  it('writes the collapsed rail and folded groups to localStorage, and unfolds again', () => {
    const prefs = TestBed.inject(PrefsStore);
    prefs.toggleSidebar();
    prefs.toggleGroup('Data');
    TestBed.tick();
    expect(JSON.parse(localStorage.getItem('fugue.console.prefs') ?? '{}')).toMatchObject({
      sidebarCollapsed: true,
      collapsedGroups: ['Data'],
    });

    prefs.toggleGroup('Data');
    expect(prefs.collapsedGroups()).toEqual([]);
  });
});
