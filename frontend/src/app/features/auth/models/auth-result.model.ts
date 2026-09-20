import { AuthenticatedUser } from './authenticated-user.model';

export interface AuthResult {
  accessToken: string;
  refreshToken: string;
  expiresInSeconds: number;
  user: AuthenticatedUser;
}
