/** Geometry and timing for the sign-in "Prelude": one motif, entering on three voices in delayed imitation. */

export interface PreludeNote {
  voice: number;
  x: number;
  y: number;
  delayMs: number;
}

export interface PreludeConnector {
  d: string;
  delayMs: number;
}

/** Motif as staff steps above the bottom line (one step is half a line gap). The same five notes on every voice. */
export const MOTIF_STEPS = [2, 6, 4, 7, 3] as const;
export const STAFF_TOP = [34, 104, 174] as const;
export const LINE_GAP = 8;
export const NOTE_SPACING = 30;
export const VOICE_OFFSET_X = 84;
export const FIRST_X = 44;
export const ENTRY_GAP_MS = 2000;
export const NOTE_GAP_MS = 220;
export const START_MS = 400;

const staffBottom = (voice: number): number => STAFF_TOP[voice] + LINE_GAP * 4;

export function preludeNotes(): PreludeNote[] {
  return STAFF_TOP.flatMap((_, voice) =>
    MOTIF_STEPS.map((step, i) => ({
      voice,
      x: FIRST_X + voice * VOICE_OFFSET_X + i * NOTE_SPACING,
      y: staffBottom(voice) - step * (LINE_GAP / 2),
      delayMs: START_MS + voice * ENTRY_GAP_MS + i * NOTE_GAP_MS,
    })),
  );
}

/** Dashed link from the last note of one entry to the first note of the next. */
export function preludeConnectors(notes: PreludeNote[] = preludeNotes()): PreludeConnector[] {
  const n = MOTIF_STEPS.length;
  return [0, 1].map((voice) => {
    const from = notes[voice * n + n - 1];
    const to = notes[(voice + 1) * n];
    const midY = (from.y + to.y) / 2;
    return {
      d: `M${from.x + 8} ${from.y} C${from.x + 40} ${midY - 6} ${to.x - 40} ${midY + 6} ${to.x - 8} ${to.y}`,
      delayMs: to.delayMs - NOTE_GAP_MS,
    };
  });
}
