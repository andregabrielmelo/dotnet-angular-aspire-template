// Shared shape returned by /register, /login, and /refresh - the backend maps all three
// to the same AuthTokensResponse record.
export interface AuthTokensResponse {
  userId: number;
  name: string;
  email: string;
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
}
