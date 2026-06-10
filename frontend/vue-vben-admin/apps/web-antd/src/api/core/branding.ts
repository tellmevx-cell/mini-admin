import { requestClient } from '#/api/request';

export interface AppBranding {
  copyright?: null | string;
  loginTitle: string;
  name: string;
  shortName: string;
  watermark: {
    enabled: boolean;
    text?: null | string;
  };
}

export async function getAppBrandingApi() {
  return requestClient.get<AppBranding>('/public/app-branding');
}
