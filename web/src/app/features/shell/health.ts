export type Health = 'checking' | 'up' | 'down';

/** Maps a TanStack query status to the footer dot: green on a 2xx ping, red on anything else. */
export function healthOf(status: 'pending' | 'error' | 'success'): Health {
  return status === 'success' ? 'up' : status === 'error' ? 'down' : 'checking';
}
