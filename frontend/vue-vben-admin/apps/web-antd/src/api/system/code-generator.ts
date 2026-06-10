import { requestClient } from '#/api/request';

export interface CodeGeneratorColumn {
  columnComment: string;
  columnName: string;
  columnType: string;
  dotNetType: string;
  isNullable: boolean;
  isPrimaryKey: boolean;
  sort: number;
  tsType: string;
}

export interface CodeGeneratorTable {
  columns: CodeGeneratorColumn[];
  existingModule?: CodeGeneratorExistingModule | null;
  generationBlockReason?: null | string;
  tableComment: string;
  tableName: string;
}

export interface CodeGeneratorExistingModule {
  component?: null | string;
  files: string[];
  moduleKind: 'Generated' | 'System' | string;
  moduleName: string;
  routePath?: null | string;
  tableName: string;
}

export interface CodeGeneratorFieldConfig {
  columnName: string;
  controlType: string;
  createVisible: boolean;
  dictionaryCode?: null | string;
  displayName: string;
  dotNetType: string;
  defaultValue?: null | string;
  isPrimaryKey: boolean;
  isRequired: boolean;
  isUnique?: boolean;
  listVisible: boolean;
  maxLength?: null | number;
  propertyName: string;
  queryMode?: string;
  queryVisible: boolean;
  sort: number;
  tsType: string;
  updateVisible: boolean;
}

export interface CodeGeneratorPreviewRequest {
  businessName: string;
  dataScopeField?: null | string;
  dataScopeMode?: string;
  enableAudit?: boolean;
  enableImportExport?: boolean;
  enableWorkflow?: boolean;
  fields: CodeGeneratorFieldConfig[];
  moduleName: string;
  parentMenuId?: null | string;
  permissionPrefix: string;
  routePath: string;
  tableName: string;
  tenantMode: string;
  workflowBusinessType?: null | string;
}

export interface CodeGeneratorPreviewFile {
  content: string;
  hasConflict: boolean;
  relativePath: string;
}

export interface CodeGeneratorInstallStep {
  description: string;
  key: string;
  status: 'Done' | 'Pending' | 'Warning' | string;
  title: string;
}

export interface CodeGeneratorInstallPlan {
  createTableSql?: null | string;
  steps: CodeGeneratorInstallStep[];
  tableExists: boolean;
}

export interface CodeGeneratorPreviewResult {
  files: CodeGeneratorPreviewFile[];
  hasConflicts: boolean;
  installPlan: CodeGeneratorInstallPlan;
  permissionCodes: string[];
}

export interface CodeGenerationHistory {
  businessName: string;
  createdAt: string;
  errorMessage?: null | string;
  files: CodeGeneratorPreviewFile[];
  id: string;
  moduleName: string;
  permissionPrefix: string;
  status: string;
  tableName: string;
  tenantMode: string;
}

export interface CodeGenerationHistoryDetail extends CodeGenerationHistory {
  installPlan: CodeGeneratorInstallPlan;
  operatorUserName?: null | string;
  preview: CodeGeneratorPreviewRequest;
}

export interface CodeGeneratorRollbackResult {
  deletedFileCount: number;
  deletedMenuCount: number;
  id: string;
  status: string;
  tableDropped: boolean;
  tableDropMessage?: null | string;
  tableDropSkipped: boolean;
}

export interface CodeGeneratorRollbackRequest {
  dropTable?: boolean;
}

export interface CodeGeneratorArtifactGovernance {
  component?: null | string;
  files: string[];
  hasHistory: boolean;
  hasMenuPermissions: boolean;
  isMapped: boolean;
  isReservedTable: boolean;
  moduleKind: string;
  moduleName: string;
  riskReason?: null | string;
  routePath?: null | string;
  tableName: string;
}

export interface CodeGeneratorArtifactGovernanceResult {
  items: CodeGeneratorArtifactGovernance[];
}

export interface CodeGeneratorArtifactCleanupRequest {
  dropTable?: boolean;
}

export interface CodeGeneratorArtifactCleanupResult {
  deletedFileCount: number;
  deletedMenuCount: number;
  moduleName: string;
  tableDropped: boolean;
  tableDropMessage?: null | string;
  tableDropSkipped: boolean;
}

export interface CodeGeneratorArtifactRegisterHistoryResult {
  history: CodeGenerationHistory;
}

export interface CodeGenerationHistoryResult {
  items: CodeGenerationHistory[];
  total: number;
}

export async function getCodeGeneratorTablesApi() {
  return requestClient.get<CodeGeneratorTable[]>('/system/code-generator/tables');
}

export async function getCodeGeneratorTableApi(tableName: string) {
  return requestClient.get<CodeGeneratorTable>(
    `/system/code-generator/tables/${tableName}`,
  );
}

export async function previewCodeGeneratorApi(
  data: CodeGeneratorPreviewRequest,
) {
  return requestClient.post<CodeGeneratorPreviewResult>(
    '/system/code-generator/preview',
    data,
  );
}

export async function generateCodeApi(
  preview: CodeGeneratorPreviewRequest,
  overwrite = false,
  autoInstall = true,
) {
  return requestClient.post<CodeGenerationHistory>(
    '/system/code-generator/generate',
    {
      autoInstall,
      overwrite,
      preview,
    },
  );
}

export async function getCodeGenerationHistoryApi(params: {
  moduleName?: string;
  page?: number;
  pageSize?: number;
  status?: string;
  tableName?: string;
}) {
  return requestClient.get<CodeGenerationHistoryResult>(
    '/system/code-generator/history',
    { params },
  );
}

export async function getCodeGenerationHistoryDetailApi(id: string) {
  return requestClient.get<CodeGenerationHistoryDetail>(
    `/system/code-generator/history/${id}`,
  );
}

export async function rollbackCodeGenerationHistoryApi(
  id: string,
  data: CodeGeneratorRollbackRequest = {},
) {
  return requestClient.post<CodeGeneratorRollbackResult>(
    `/system/code-generator/history/${id}/rollback`,
    data,
  );
}

export async function getCodeGeneratorArtifactsApi() {
  return requestClient.get<CodeGeneratorArtifactGovernanceResult>(
    '/system/code-generator/artifacts',
  );
}

export async function cleanupCodeGeneratorArtifactApi(
  moduleName: string,
  data: CodeGeneratorArtifactCleanupRequest = {},
) {
  return requestClient.post<CodeGeneratorArtifactCleanupResult>(
    `/system/code-generator/artifacts/${moduleName}/cleanup`,
    data,
  );
}

export async function registerCodeGeneratorArtifactHistoryApi(moduleName: string) {
  return requestClient.post<CodeGeneratorArtifactRegisterHistoryResult>(
    `/system/code-generator/artifacts/${moduleName}/register-history`,
  );
}
