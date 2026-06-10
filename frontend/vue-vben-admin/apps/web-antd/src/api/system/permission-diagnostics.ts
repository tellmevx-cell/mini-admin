import { requestClient } from '#/api/request';

export interface PermissionDiagnosticsCache {
  menusKey: string;
  permissionCodesKey: string;
  securityStampKey: string;
}

export interface PermissionDiagnosticsDataScope {
  departmentId?: null | string;
  departmentIds: string[];
  departmentNames: string[];
  description: string;
  level: string;
}

export interface PermissionDiagnosticsMenu {
  id: string;
  isVisible: boolean;
  path: string;
  permissionCode?: null | string;
  title: string;
}

export interface PermissionDiagnosticsRole {
  buttonPermissionCount: number;
  code: string;
  dataScope: string;
  customDepartmentIds?: null | string[];
  customDepartmentNames?: null | string[];
  id: string;
  isEnabled: boolean;
  menuCount: number;
  name: string;
  visibleMenuCount: number;
}

export interface PermissionDiagnosticsTenant {
  isPackageLimited: boolean;
  isTenant: boolean;
  packageId?: null | string;
  packageMenuCount: number;
  packageName?: null | string;
  tenantCode?: null | string;
  tenantId?: null | string;
  tenantName?: null | string;
}

export interface PermissionDiagnosticsUser {
  departmentName?: null | string;
  id: string;
  isEnabled: boolean;
  positionName?: null | string;
  realName: string;
  userName: string;
}

export interface PermissionDiagnosticsEffective {
  buttonPermissionCount: number;
  finalMenuCount: number;
  packageMenuCount: number;
  permissionCodeCount: number;
  roleMenuCount: number;
  visibleMenuCount: number;
}

export interface PermissionDiagnosticsWarning {
  code: string;
  level: string;
  message: string;
  suggestion: string;
}

export interface PermissionDiagnosticsResult {
  cache: PermissionDiagnosticsCache;
  dataScope: PermissionDiagnosticsDataScope;
  effective: PermissionDiagnosticsEffective;
  menuItems: PermissionDiagnosticsMenu[];
  permissionCodes: string[];
  roles: PermissionDiagnosticsRole[];
  tenant: PermissionDiagnosticsTenant;
  user: PermissionDiagnosticsUser;
  warnings: PermissionDiagnosticsWarning[];
}

export async function getPermissionDiagnosticsApi(userName: string) {
  return requestClient.get<PermissionDiagnosticsResult>(
    `/system/permission-diagnostics/user/${encodeURIComponent(userName)}`,
  );
}

export async function refreshPermissionDiagnosticsCacheApi(userName: string) {
  return requestClient.post<boolean>(
    `/system/permission-diagnostics/user/${encodeURIComponent(userName)}/refresh-cache`,
  );
}
