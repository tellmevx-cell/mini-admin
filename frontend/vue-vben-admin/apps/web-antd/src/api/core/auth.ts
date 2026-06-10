import { baseRequestClient, requestClient } from '#/api/request';

export namespace AuthApi {
  /** 登录接口参数 */
  export interface LoginParams {
    captchaCode?: string;
    captchaId?: string;
    password?: string;
    tenantCode?: string;
    username?: string;
  }

  /** 登录接口返回值 */
  export interface LoginResult {
    accessToken: string;
    sessionId: string;
    tenantCode?: null | string;
    tenantId?: null | string;
  }

  export interface RefreshTokenResult {
    data: string;
    status: number;
  }

  export interface CaptchaResult {
    expiresInSeconds: number;
    id: string;
    imageBase64: string;
  }

  export interface TenantOption {
    code: string;
    name: string;
  }
}

/**
 * 登录
 */
export async function loginApi(data: AuthApi.LoginParams) {
  return requestClient.post<AuthApi.LoginResult>('/auth/login', data, {
    skipReAuthenticate: true,
  });
}

export async function getCaptchaApi() {
  return requestClient.get<AuthApi.CaptchaResult>('/auth/captcha');
}

export async function getTenantOptionsApi() {
  return requestClient.get<AuthApi.TenantOption[]>('/auth/tenant-options', {
    skipReAuthenticate: true,
  });
}

/**
 * 刷新accessToken
 */
export async function refreshTokenApi() {
  return baseRequestClient.post<AuthApi.RefreshTokenResult>('/auth/refresh', {
    withCredentials: true,
  });
}

/**
 * 退出登录
 */
export async function logoutApi() {
  return requestClient.post('/auth/logout');
}

export interface ChangeCurrentPasswordParams {
  confirmPassword: string;
  newPassword: string;
  oldPassword: string;
}

export async function changeCurrentPasswordApi(
  data: ChangeCurrentPasswordParams,
) {
  return requestClient.post<boolean>('/user/change-password', data);
}

/**
 * 获取用户权限码
 */
export async function getAccessCodesApi() {
  return requestClient.get<string[]>('/auth/codes');
}
