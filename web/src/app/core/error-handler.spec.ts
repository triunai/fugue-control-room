import { ErrorHandler } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { FugueErrorHandler, RESIZE_OBSERVER_LOOP, provideFugueErrorHandler } from './error-handler';

describe('FugueErrorHandler', () => {
  let logged: ReturnType<typeof vi.spyOn>;
  let handler: ErrorHandler;

  beforeEach(() => {
    logged = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    TestBed.configureTestingModule({ providers: [provideFugueErrorHandler()] });
    handler = TestBed.inject(ErrorHandler);
  });
  afterEach(() => vi.restoreAllMocks());

  it('is the app-wide ErrorHandler', () => {
    expect(handler).toBeInstanceOf(FugueErrorHandler);
  });

  it('drops the ResizeObserver loop notice, plain or wrapped as Angular does in dev', () => {
    handler.handleError(new Error(RESIZE_OBSERVER_LOOP));
    handler.handleError(
      new Error('An ErrorEvent with no error occurred.', { cause: { message: RESIZE_OBSERVER_LOOP } }),
    );
    expect(logged).not.toHaveBeenCalled();
  });

  it('still reports every other error, including other ResizeObserver texts', () => {
    handler.handleError(new Error('boom'));
    handler.handleError(new Error('ResizeObserver loop limit exceeded'));
    handler.handleError(new Error(`${RESIZE_OBSERVER_LOOP} (and more)`));
    handler.handleError('a string');
    expect(logged).toHaveBeenCalledTimes(4);
  });
});
