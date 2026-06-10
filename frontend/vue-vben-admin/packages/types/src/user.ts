import type { BasicUserInfo } from '@vben-core/typings';

/** 用户信息 */
interface UserInfo extends BasicUserInfo {
  departmentId?: null | string;
  departmentName?: null | string;
  /**
   * 用户描述
   */
  desc: string;
  /**
   * 首页地址
   */
  homePath: string;
  positionId?: null | string;
  positionName?: null | string;

  /**
   * accessToken
   */
  token: string;
}

export type { UserInfo };
