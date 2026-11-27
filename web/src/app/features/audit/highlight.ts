export interface HighlightSegment {
  text: string;
  em: boolean;
}

const EM = /(<em>|<\/em>)/;

/**
 * The search API wraps matches in <em>...</em> and does NOT HTML-escape the rest. Split into plain segments and
 * `em` segments; everything else (including any other tag) stays literal text, which Angular's text binding
 * escapes, so no markup from an audited value can ever reach the DOM.
 */
export function highlightSegments(value: string | null | undefined): HighlightSegment[] {
  if (!value) return [];
  const out: HighlightSegment[] = [];
  let em = false;
  for (const part of value.split(EM)) {
    if (part === '<em>') em = true;
    else if (part === '</em>') em = false;
    else if (part) out.push({ text: part, em });
  }
  return out;
}
