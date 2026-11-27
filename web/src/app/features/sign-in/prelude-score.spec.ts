import { describe, expect, it } from 'vitest';
import { ENTRY_GAP_MS, MOTIF_STEPS, preludeConnectors, preludeNotes } from './prelude-score';

describe('prelude score', () => {
  const notes = preludeNotes();

  it('places the same motif on three voices', () => {
    expect(notes).toHaveLength(MOTIF_STEPS.length * 3);
    const intervals = (v: number) => {
      const ys = notes.filter((n) => n.voice === v).map((n) => n.y);
      return ys.map((y) => y - ys[0]);
    };
    expect(intervals(1)).toEqual(intervals(0));
    expect(intervals(2)).toEqual(intervals(0));
  });

  it('enters each voice one entry gap after the previous', () => {
    const first = [0, 1, 2].map((v) => notes.find((n) => n.voice === v)!.delayMs);
    expect(first[1] - first[0]).toBe(ENTRY_GAP_MS);
    expect(first[2] - first[1]).toBe(ENTRY_GAP_MS);
  });

  it('links entries with two connectors that appear before the next entry', () => {
    const c = preludeConnectors(notes);
    expect(c).toHaveLength(2);
    expect(c[0].delayMs).toBeLessThan(notes.find((n) => n.voice === 1)!.delayMs);
  });
});
