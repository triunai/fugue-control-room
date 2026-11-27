import { assessmentGrid } from './claim-assessment-logic';
import { toCsv } from './csv-export';

describe('toCsv', () => {
  it('writes values, formula results, empty cells and quotes awkward text', () => {
    expect(toCsv([['a', 1.5, null, { formula: 'SUM(A1)', result: 7 }], ['x,y', 'say "hi"', 'l1\nl2', 0]])).toBe(
      'a,1.5,,7\r\n"x,y","say ""hi""","l1\nl2",0\r\n',
    );
  });

  it('exports the assessment sheet with Gross and Net as numbers', () => {
    const lines = toCsv(assessmentGrid('SGD', [100, 50, 0, 0, 0], 20)).trim().split('\r\n');
    expect(lines[0]).toBe('Item,Amount (SGD)');
    expect(lines.at(-3)).toBe('Gross estimate,150');
    expect(lines.at(-1)).toBe('Net estimate,130');
  });
});
