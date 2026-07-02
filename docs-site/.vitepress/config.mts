import { defineConfig } from 'vitepress';

export default defineConfig({
  title: 'MiniAdmin',
  description: '.NET 10 + Vben Admin 企业后台二开底座',
  lang: 'zh-CN',
  cleanUrls: true,
  lastUpdated: true,
  markdown: {
    lineNumbers: true,
  },
  head: [
    ['meta', { name: 'theme-color', content: '#0f172a' }],
    ['meta', { name: 'keywords', content: 'MiniAdmin,.NET,Vben Admin,Vue,后台管理,工作流,消息中心,SaaS,代码生成器' }],
  ],
  themeConfig: {
    logo: '/logo.svg',
    siteTitle: 'MiniAdmin',
    search: {
      provider: 'local',
    },
    nav: [
      { text: '指南', link: '/guide/introduction' },
      { text: '功能', link: '/features/overview' },
      { text: '二开', link: '/developer/architecture' },
      { text: '手册', link: '/runbooks/workflow-message-center' },
      { text: 'FAQ', link: '/faq' },
    ],
    sidebar: {
      '/guide/': [
        {
          text: '开始使用',
          items: [
            { text: '项目介绍', link: '/guide/introduction' },
            { text: '快速开始', link: '/guide/quick-start' },
            { text: '一键部署', link: '/guide/docker-compose#一键部署脚本' },
            { text: 'Docker Compose', link: '/guide/docker-compose' },
            { text: '本地开发', link: '/guide/local-development' },
            { text: '部署上线', link: '/guide/deployment' },
          ],
        },
      ],
      '/features/': [
        {
          text: '功能说明',
          items: [
            { text: '能力总览', link: '/features/overview' },
            { text: '功能截图展示', link: '/features/showcase' },
            { text: '认证与 RBAC', link: '/features/auth-rbac' },
            { text: 'SaaS 租户', link: '/features/tenant' },
            { text: '工作流审批', link: '/features/workflow' },
            { text: '消息中心', link: '/features/message-center' },
            { text: '代码生成器', link: '/features/code-generator' },
            { text: '监控与审计', link: '/features/monitoring-audit' },
            { text: '文件存储', link: '/features/file-storage' },
          ],
        },
      ],
      '/developer/': [
        {
          text: '二开指南',
          items: [
            { text: '架构总览', link: '/developer/architecture' },
            { text: '后端开发', link: '/developer/backend' },
            { text: '前端开发', link: '/developer/frontend' },
            { text: '数据库与迁移', link: '/developer/database' },
            { text: '事件总线与工作单元', link: '/developer/event-bus-unit-of-work' },
            { text: '新增模块流程', link: '/developer/add-module' },
            { text: '开发约定', link: '/developer/conventions' },
          ],
        },
      ],
      '/runbooks/': [
        {
          text: '运行手册',
          items: [
            { text: '工作流与消息中心', link: '/runbooks/workflow-message-center' },
            { text: '验收清单', link: '/runbooks/acceptance' },
          ],
        },
      ],
    },
    outline: {
      level: [2, 3],
      label: '本页目录',
    },
    docFooter: {
      prev: '上一页',
      next: '下一页',
    },
    lastUpdated: {
      text: '最后更新',
      formatOptions: {
        dateStyle: 'medium',
        timeStyle: 'short',
      },
    },
    footer: {
      message: 'MiniAdmin documentation site. Built for learning, delivery, and secondary development.',
      copyright: 'Copyright © 2026 MiniAdmin',
    },
  },
});
