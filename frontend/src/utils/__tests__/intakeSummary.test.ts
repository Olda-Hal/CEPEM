import { describe, it, expect } from 'vitest';
import { parseIntakeSummary, formSubmissionToIntakeSummary } from '../intakeSummary';

describe('parseIntakeSummary', () => {
  it('returns an empty summary for null/undefined comment', () => {
    expect(parseIntakeSummary(null).medications).toEqual([]);
    expect(parseIntakeSummary(undefined).rawLines).toEqual([]);
  });

  it('parses weight, height, medications and notes', () => {
    const summary = parseIntakeSummary(
      ['Váha: 80 kg', 'Výška: 180 cm', 'Léky: Aspirin', 'Poznámky: nic'].join('\n')
    );
    expect(summary.weight).toBe('80 kg');
    expect(summary.height).toBe('180 cm');
    expect(summary.medications).toEqual(['Aspirin']);
    expect(summary.notes).toBe('nic');
  });

  it('collects multiple medication lines', () => {
    const summary = parseIntakeSummary('Léky: A\nLéky: B');
    expect(summary.medications).toEqual(['A', 'B']);
  });

  it('routes covid and gynecology lines into their sections', () => {
    const summary = parseIntakeSummary(
      [
        'COVID: ano',
        'Vakcinace (Chřipka): 2021',
        'Rodila: ano (2x)',
        'Kojila: ano',
        'RTG mamograf: ano (1x)'
      ].join('\n')
    );
    expect(summary.covid).toEqual(['COVID: ano', 'Vakcinace (Chřipka): 2021']);
    expect(summary.gynecology).toEqual([
      'Rodila: ano (2x)',
      'Kojila: ano',
      'RTG mamograf: ano (1x)'
    ]);
  });

  it('skips blank lines and keeps raw lines', () => {
    const summary = parseIntakeSummary('Váha: 75\n\n  \nVýška: 175');
    expect(summary.rawLines).toEqual(['Váha: 75', 'Výška: 175']);
    expect(summary.weight).toBe('75');
    expect(summary.height).toBe('175');
  });
});

describe('formSubmissionToIntakeSummary', () => {
  it('returns an empty summary without a submission', () => {
    const summary = formSubmissionToIntakeSummary(null);
    expect(summary.medications).toEqual([]);
    expect(summary.gynecology).toEqual([]);
  });

  it('maps medication and lifestyle flags', () => {
    const summary = formSubmissionToIntakeSummary({
      medication: { medHeart: true, medPain: true },
      lifestyle: { smoking: true, additionalHealthInfo: 'pozor' }
    } as never);
    expect(summary.medications).toEqual(['Léky na srdce', 'Bolest léky']);
    expect(summary.healthStatus).toEqual(['Kouření']);
    expect(summary.notes).toBe('pozor');
  });

  it('maps reproductive health entries with counts', () => {
    const summary = formSubmissionToIntakeSummary({
      reproductiveHealth: { gaveBirth: true, birthCount: 2, mammogram: true, mammogramCount: 1 }
    } as never);
    expect(summary.gynecology).toEqual(['Rodila: ano (2x)', 'RTG mamograf: ano (1x)']);
  });
});
