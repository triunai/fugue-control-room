import { workbookClass } from './xlsx-export';

class FakeWorkbook {}

describe('workbookClass (ExcelJS is CommonJS)', () => {
  it('finds Workbook on the namespace, as the dev server exposes it', () => {
    expect(workbookClass({ Workbook: FakeWorkbook })).toBe(FakeWorkbook);
  });

  it('finds Workbook under default, as the production bundle exposes it (the "is not a constructor" bug)', () => {
    expect(workbookClass({ default: { Workbook: FakeWorkbook } })).toBe(FakeWorkbook);
  });

  it('fails loudly when neither shape has it', () => {
    expect(() => workbookClass({ default: {} })).toThrowError(/no Workbook export/);
  });
});
