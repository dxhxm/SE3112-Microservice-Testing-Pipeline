import axios, { AxiosInstance } from 'axios';
import { Config } from '../config/selenium.config';

export class ApiClient {
  private static userHttp: AxiosInstance = axios.create({
    baseURL: Config.userServiceUrl,
    validateStatus: () => true, // Don't throw on HTTP status >= 400
  });

  private static tournamentHttp: AxiosInstance = axios.create({
    baseURL: Config.tournamentServiceUrl,
    validateStatus: () => true,
  });

  private static streamHttp: AxiosInstance = axios.create({
    baseURL: Config.streamServiceUrl,
    validateStatus: () => true,
  });

  /**
   * Directly authenticates via the User Service to acquire a bearer token
   */
  public static async login(identifier: string, password: string):Promise<{ token: string; status: number; data: any }> {
    const res = await this.userHttp.post('/api/Auth/login', { identifier, password });
    return { token: res.data?.token || '', status: res.status, data: res.data };
  }

  /**
   * Promotes a registered user's role directly via User Service API
   */
  public static async changeRole(token: string, newRole: 'Viewer' | 'Organizer' | 'Streamer'): Promise<number> {
    const res = await this.userHttp.put(
      '/api/Users/me/role',
      { role: newRole },
      { headers: { Authorization: `Bearer ${token}` } }
    );
    return res.status;
  }

  /**
   * Automated Teardown: Deletes the user account and associated credentials completely
   */
  public static async deleteAccount(token: string): Promise<number> {
    const res = await this.userHttp.delete('/api/Users/me', {
      headers: { Authorization: `Bearer ${token}` },
    });
    return res.status;
  }

  /**
   * Queries tournaments directly from Tournament Service to assert cross-service state
   */
  public static async getTournaments(): Promise<any[]> {
    const res = await this.tournamentHttp.get('/api/tournaments');
    return Array.isArray(res.data) ? res.data : [];
  }

  /**
   * Queries matches directly from Stream Service
   */
  public static async getMatches(): Promise<any[]> {
    const res = await this.streamHttp.get('/api/matches');
    return Array.isArray(res.data) ? res.data : [];
  }
}
