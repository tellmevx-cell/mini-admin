import { createRequire } from 'node:module';
import { mkdir, readdir, rm, unlink, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const { chromium } = require('../frontend/vue-vben-admin/node_modules/playwright');

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, '..');
const screenshotDir = path.join(
  repoRoot,
  'docs-site',
  'features',
  'screenshots',
);

const webUrl = trimEndSlash(process.env.MINIADMIN_WEB_URL || 'http://localhost:5666');
const username = process.env.MINIADMIN_USERNAME || 'admin';
const password = process.env.MINIADMIN_PASSWORD || '123456';
const tenantCode = process.env.MINIADMIN_TENANT_CODE || '';

const viewport = {
  width: Number.parseInt(process.env.MINIADMIN_SCREENSHOT_WIDTH || '1440', 10),
  height: Number.parseInt(process.env.MINIADMIN_SCREENSHOT_HEIGHT || '960', 10),
};

const featurePages = [
  {
    id: '00-login',
    group: '访问入口',
    title: '登录入口',
    description: '平台管理员和演示租户共用的登录入口。',
    path: '/auth/login',
    public: true,
  },
  {
    id: '01-dashboard',
    group: '工作台',
    title: '分析页',
    description: '后台首页、数据概览和快捷入口。',
    path: '/analytics',
  },
  {
    id: '02-workflow-center',
    group: '工作流审批',
    title: '审批中心',
    description: '待办、申请、抄送、流程实例、流程定义和业务绑定统一入口。',
    path: '/workflow/center',
  },
  {
    id: '03-message-center',
    group: '消息中心',
    title: '消息通知中心',
    description: '站内信、通知模板、通知策略、个人订阅和投递记录。',
    path: '/system/notification',
  },
  {
    id: '04-tenant',
    group: 'SaaS 多租户',
    title: '租户管理',
    description: '平台租户、租户套餐、初始化和租户状态管理。',
    path: '/platform/tenant',
  },
  {
    id: '05-user',
    group: '认证与 RBAC',
    title: '用户管理',
    description: '用户、部门、岗位、角色、状态和导入导出能力。',
    path: '/system/user',
  },
  {
    id: '06-role',
    group: '认证与 RBAC',
    title: '角色管理',
    description: '角色授权、菜单权限和数据权限配置。',
    path: '/system/role',
  },
  {
    id: '07-menu',
    group: '认证与 RBAC',
    title: '菜单管理',
    description: '动态路由、菜单层级、组件路径和权限码维护。',
    path: '/system/menu',
  },
  {
    id: '08-permission-diagnostics',
    group: '认证与 RBAC',
    title: '权限诊断',
    description: '追踪用户、角色、菜单、数据范围和缓存授权链路。',
    path: '/system/permission-diagnostics',
  },
  {
    id: '09-code-generator',
    group: '代码生成器',
    title: '代码生成器',
    description: '表结构读取、字段选择、生成预览、安装、历史和回滚。',
    path: '/system/code-generator',
  },
  {
    id: '10-sample-order',
    group: '业务示例',
    title: '示例订单',
    description: '演示普通业务模块如何接入工作流审批。',
    path: '/business/sample-order',
  },
  {
    id: '11-customer',
    group: '业务示例',
    title: '客户资料',
    description: '由代码生成器沉淀的客户资料 CRUD 示例。',
    path: '/business/customer',
  },
  {
    id: '12-file-storage',
    group: '文件存储',
    title: '文件管理',
    description: '文件上传、下载、本地存储和 MinIO 扩展入口。',
    path: '/system/file',
  },
  {
    id: '13-monitor',
    group: '监控与审计',
    title: '系统监控',
    description: '运行指标、健康状态和系统资源观察。',
    path: '/system/monitor',
  },
  {
    id: '14-alert-center',
    group: '监控与审计',
    title: '告警中心',
    description: '告警列表、告警规则和通知联动。',
    path: '/system/alert',
  },
  {
    id: '15-audit-log',
    group: '监控与审计',
    title: '操作日志',
    description: '接口审计、请求记录、实体变更和导出。',
    path: '/system/log',
  },
  {
    id: '16-login-log',
    group: '安全中心',
    title: '登录日志',
    description: '登录成功、失败、锁定和来源追踪。',
    path: '/system/login-log',
  },
  {
    id: '17-security-center',
    group: '安全中心',
    title: '安全中心',
    description: '登录安全策略、密码策略、安全事件和在线会话。',
    path: '/system/security-center',
  },
  {
    id: '18-project-runtime',
    group: '工程化',
    title: '项目运行管理',
    description: '管理本地服务、运行日志、构建记录和构建产物。',
    path: '/system/project-runtime',
  },
  {
    id: '19-scheduled-job',
    group: '工程化',
    title: '定时任务',
    description: '任务配置、手动执行、任务日志和运行状态。',
    path: '/system/scheduled-job',
  },
];

await main();

async function main() {
  await assertWebReachable();
  await resetScreenshotDir();

  const browser = await chromium.launch({
    headless: process.env.MINIADMIN_SCREENSHOT_HEADLESS !== 'false',
  });
  const context = await browser.newContext({
    viewport,
    deviceScaleFactor: 1,
    locale: 'zh-CN',
    colorScheme: 'light',
  });
  const page = await context.newPage();
  page.setDefaultTimeout(20_000);

  const results = [];

  try {
    const loginPage = featurePages.find((item) => item.public);
    if (loginPage) {
      results.push(await captureFeaturePage(page, loginPage));
    }

    await login(page);

    for (const feature of featurePages.filter((item) => !item.public)) {
      results.push(await captureFeaturePage(page, feature));
    }
  } finally {
    await browser.close();
  }

  const output = {
    generatedAt: new Date().toISOString(),
    webUrl,
    total: results.length,
    succeeded: results.filter((item) => item.status === 'success').length,
    failed: results.filter((item) => item.status === 'failed').length,
    items: results,
  };

  await writeFile(
    path.join(screenshotDir, 'screenshots.json'),
    `${JSON.stringify(output, null, 2)}\n`,
    'utf8',
  );

  if (output.failed > 0) {
    console.warn(`Screenshots completed with ${output.failed} failed page(s).`);
    for (const item of results.filter((result) => result.status === 'failed')) {
      console.warn(`- ${item.id}: ${item.error}`);
    }
    process.exitCode = 1;
    return;
  }

  console.log(`Captured ${output.succeeded} feature screenshot(s).`);
}

async function assertWebReachable() {
  try {
    const response = await fetch(webUrl, { signal: AbortSignal.timeout(5000) });
    if (!response.ok && response.status >= 500) {
      throw new Error(`HTTP ${response.status}`);
    }
  } catch (error) {
    throw new Error(
      `MiniAdmin frontend is not reachable at ${webUrl}. Start the API and web app first, or set MINIADMIN_WEB_URL. ${error}`,
    );
  }
}

async function resetScreenshotDir() {
  await mkdir(screenshotDir, { recursive: true });
  const entries = await readdir(screenshotDir, { withFileTypes: true });
  await Promise.all(
    entries
      .filter((entry) => entry.isFile() && entry.name.endsWith('.png'))
      .map((entry) => unlink(path.join(screenshotDir, entry.name))),
  );
  await rm(path.join(screenshotDir, 'screenshots.json'), { force: true }).catch(
    () => {},
  );
}

async function login(page) {
  await page.goto(`${webUrl}/auth/login`, { waitUntil: 'domcontentloaded' });
  await waitForPageReady(page);

  await fillLoginForm(page);

  const loginButton = page
    .getByRole('button', { name: /登录|登 录|Login|Sign in/i })
    .first();
  await loginButton.click();

  await Promise.race([
    page.waitForURL((url) => !url.pathname.includes('/auth/login'), {
      timeout: 20_000,
    }),
    page.waitForSelector('.ant-layout, [class*="layout"]', { timeout: 20_000 }),
  ]);
  await waitForPageReady(page);
}

async function fillLoginForm(page) {
  if (tenantCode) {
    await fillFirst(page, [
      'input[placeholder*="租户"]',
      'input[aria-label*="租户"]',
    ], tenantCode);
  }

  await fillFirst(page, [
    'input[placeholder*="用户名"]',
    'input[aria-label*="用户名"]',
    'input[name="username"]',
  ], username);

  await fillFirst(page, [
    'input[type="password"]',
    'input[placeholder*="密码"]',
    'input[name="password"]',
  ], password);
}

async function fillFirst(page, selectors, value) {
  for (const selector of selectors) {
    const locator = page.locator(selector).first();
    if ((await locator.count()) === 0) {
      continue;
    }

    try {
      await locator.fill(value);
      return true;
    } catch {
      // Try the next selector. Vben form wrappers can render hidden inputs.
    }
  }

  return false;
}

async function captureFeaturePage(page, feature) {
  const fileName = `${feature.id}.png`;
  const relativePath = `./screenshots/${fileName}`;
  const absolutePath = path.join(screenshotDir, fileName);

  try {
    await page.goto(`${webUrl}${feature.path}`, { waitUntil: 'domcontentloaded' });
    await waitForPageReady(page);
    await prepareForScreenshot(page);
    await page.screenshot({
      path: absolutePath,
      fullPage: false,
    });

    console.log(`Captured ${feature.title}: ${relativePath}`);
    return {
      ...feature,
      image: relativePath,
      status: 'success',
    };
  } catch (error) {
    const fallbackPath = path.join(screenshotDir, `${feature.id}-failed.png`);
    await page.screenshot({ path: fallbackPath, fullPage: false }).catch(() => {});

    return {
      ...feature,
      image: `./screenshots/${feature.id}-failed.png`,
      status: 'failed',
      error: error instanceof Error ? error.message : String(error),
    };
  }
}

async function waitForPageReady(page) {
  await page.waitForLoadState('domcontentloaded').catch(() => {});
  await page.waitForLoadState('networkidle', { timeout: 8000 }).catch(() => {});
  await page.waitForTimeout(900);
  await page
    .locator('.ant-spin-spinning, .vben-loading, [aria-busy="true"]')
    .first()
    .waitFor({ state: 'detached', timeout: 3000 })
    .catch(() => {});
}

async function prepareForScreenshot(page) {
  await page.addStyleTag({
    content: `
      .ant-message,
      .ant-notification,
      [class*="vben-lock-screen"],
      [class*="fixed"][class*="bottom"] {
        display: none !important;
      }
      * {
        caret-color: transparent !important;
      }
    `,
  }).catch(() => {});
}

function trimEndSlash(value) {
  return value.replace(/\/+$/, '');
}
