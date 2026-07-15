import { requestClient } from '#/api/request';

const rawResponse = { responseReturn: 'body' as const };

export interface OpenPlatformApplicationItem {
  allowsClientCredentials: boolean;
  apiPermissions: string[];
  clientId: string;
  clientType: 'Confidential' | 'Public';
  createdAt?: null | string;
  displayName: string;
  id: string;
  postLogoutRedirectUris: string[];
  redirectUris: string[];
  scopes: string[];
  tenantId?: null | string;
}

export interface CreateOpenPlatformApplicationParams {
  allowClientCredentials: boolean;
  apiPermissions: string[];
  clientType: 'Confidential' | 'Public';
  displayName: string;
  postLogoutRedirectUris: string[];
  redirectUris: string[];
  scopes: string[];
}

export interface OpenPlatformApplicationSecret {
  application: OpenPlatformApplicationItem;
  clientSecret: string;
}

export interface OpenApiCredentialItem {
  appKey: string;
  createdAt: string;
  expiresAt?: null | string;
  id: string;
  isEnabled: boolean;
  lastUsedAt?: null | string;
  name: string;
  permissions: string[];
}

export interface CreateOpenApiCredentialParams {
  expiresAt?: null | string;
  name: string;
  permissions: string[];
}

export interface OpenApiCredentialSecret {
  appSecret: string;
  credential: OpenApiCredentialItem;
}

export async function getOpenPlatformApplicationsApi() {
  return requestClient.get<OpenPlatformApplicationItem[]>(
    '/open-platform/applications',
    rawResponse,
  );
}

export async function createOpenPlatformApplicationApi(
  data: CreateOpenPlatformApplicationParams,
) {
  return requestClient.post<OpenPlatformApplicationSecret>(
    '/open-platform/applications',
    data,
    rawResponse,
  );
}

export async function rotateOpenPlatformApplicationSecretApi(id: string) {
  return requestClient.post<string>(
    `/open-platform/applications/${id}/rotate-secret`,
    undefined,
    rawResponse,
  );
}

export async function deleteOpenPlatformApplicationApi(id: string) {
  return requestClient.delete<boolean>(
    `/open-platform/applications/${id}`,
    rawResponse,
  );
}

export async function getMyOpenApiCredentialsApi() {
  return requestClient.get<OpenApiCredentialItem[]>(
    '/open-platform/credentials/my',
    rawResponse,
  );
}

export async function createMyOpenApiCredentialApi(
  data: CreateOpenApiCredentialParams,
) {
  return requestClient.post<OpenApiCredentialSecret>(
    '/open-platform/credentials/my',
    data,
    rawResponse,
  );
}

export async function revokeMyOpenApiCredentialApi(id: string) {
  return requestClient.delete<boolean>(
    `/open-platform/credentials/my/${id}`,
    rawResponse,
  );
}
