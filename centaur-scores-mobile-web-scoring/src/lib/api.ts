import type {
  ApiErrorCode,
  ParticipantOptionsResponse,
  ParticipantScoreUpdates,
  ScoreConflictEntry,
  ScorekeeperMatch,
  ScorekeeperParticipantUpdate,
  TimeResponse,
} from './types';

export class ApiError extends Error {
  status: number;
  code: ApiErrorCode | null;
  conflicts: ScoreConflictEntry[] | null;

  constructor(status: number, code: ApiErrorCode | null, message: string, conflicts: ScoreConflictEntry[] | null = null) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
    this.conflicts = conflicts;
  }
}

export class NetworkError extends Error {
  constructor(message = 'Network request failed') {
    super(message);
    this.name = 'NetworkError';
  }
}

async function parseErrorBody(response: Response): Promise<{ code: ApiErrorCode | null; conflicts: ScoreConflictEntry[] | null }> {
  try {
    const body = await response.json();
    if (Array.isArray(body)) {
      // Tolerate a bare list of conflict entries as the whole body.
      return { code: 'UPDATE_SCORE_CONFLICT', conflicts: body as ScoreConflictEntry[] };
    }
    // The API responds with { error: { code, message }, conflicts?: [...] };
    // tolerate a flat { error: "CODE" } / { code: "CODE" } shape too.
    const code = typeof body?.error === 'string' ? body.error : (body?.error?.code ?? body?.code ?? null);
    const conflicts = body?.conflicts ?? null;
    return { code, conflicts };
  } catch {
    return { code: null, conflicts: null };
  }
}

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(url, {
      ...init,
      headers: {
        accept: 'application/json',
        ...(init?.body ? { 'content-type': 'application/json' } : {}),
        ...init?.headers,
      },
    });
  } catch {
    throw new NetworkError();
  }

  if (!response.ok) {
    const { code, conflicts } = await parseErrorBody(response);
    throw new ApiError(response.status, code, `Request to ${url} failed with status ${response.status}`, conflicts);
  }

  if (response.status === 204) {
    return undefined as T;
  }
  return (await response.json()) as T;
}

export class ScorekeeperApi {
  constructor(private baseUrl: string) {}

  private url(suffix = ''): string {
    return `${this.baseUrl.replace(/\/+$/, '')}${suffix}`;
  }

  getMatchInfo(): Promise<ScorekeeperMatch> {
    return request<ScorekeeperMatch>(this.url());
  }

  putParticipants(participants: ScorekeeperParticipantUpdate[]): Promise<ScorekeeperMatch | void> {
    return request(this.url('/participants'), {
      method: 'PUT',
      body: JSON.stringify(participants),
    });
  }

  putScores(updates: ParticipantScoreUpdates[]): Promise<void> {
    return request(this.url('/scores'), {
      method: 'PUT',
      body: JSON.stringify(updates),
    });
  }

  getParticipantOptions(): Promise<ParticipantOptionsResponse> {
    return request<ParticipantOptionsResponse>(this.url('/participant-options'));
  }

  getTime(): Promise<TimeResponse> {
    return request<TimeResponse>(this.url('/time'));
  }
}
