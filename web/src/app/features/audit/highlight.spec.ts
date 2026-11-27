import { highlightSegments } from './highlight';

describe('highlightSegments', () => {
  it('marks only <em> spans', () => {
    expect(highlightSegments('email_<em>taken</em> twice')).toEqual([
      { text: 'email_', em: false },
      { text: 'taken', em: true },
      { text: ' twice', em: false },
    ]);
  });

  it('keeps every other tag as literal text', () => {
    const hostile = '<img src=x onerror=alert(1)><em>hit</em><script>alert(2)</script><EM>x</EM>';
    const segs = highlightSegments(hostile);
    expect(segs.filter((s) => s.em).map((s) => s.text)).toEqual(['hit']);
    expect(segs.map((s) => s.text).join('')).toBe('<img src=x onerror=alert(1)>hit<script>alert(2)</script><EM>x</EM>');
  });

  it('is empty for no value and survives an unclosed tag', () => {
    expect(highlightSegments(null)).toEqual([]);
    expect(highlightSegments('<em>open')).toEqual([{ text: 'open', em: true }]);
  });
});
