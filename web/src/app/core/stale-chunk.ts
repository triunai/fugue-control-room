import { NavigationError } from '@angular/router';

/**
 * A tab opened before a redeploy still runs the old bundle; its lazy route chunks were renamed by the new build, so a
 * navigation fails to fetch them and nothing happens. Detect that one error and load the target URL fresh instead.
 */
export function isStaleChunkError(error: unknown): boolean {
  const text = error instanceof Error ? `${error.name} ${error.message}` : String(error);
  return /ChunkLoadError|Loading chunk [\w-]+ failed|Failed to fetch dynamically imported module|Importing a module script failed|error loading dynamically imported module/i.test(
    text,
  );
}

export function reloadOnStaleChunk(
  event: NavigationError,
  reload: (url: string) => void = (url) => location.assign(url),
): void {
  if (isStaleChunkError(event.error)) reload(event.url);
}
