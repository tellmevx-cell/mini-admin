---
layout: home
hero:
  name: MiniAdmin
  text: 企业后台与二开工程底座
  tagline: 基于 .NET 10、EF Core、Vben Admin 和 Ant Design Vue，覆盖认证权限、SaaS 租户、工作流、消息中心、审计、监控、文件和代码生成。
  actions:
    - theme: brand
      text: 快速开始
      link: /guide/quick-start
    - theme: alt
      text: 二开指南
      link: /developer/architecture
features:
  - title: 后端分层清晰
    details: Domain、Contracts、Application、Infrastructure、Api 分层明确，便于替换存储、扩展服务和编写测试。
  - title: 前端工程完整
    details: 基于 Vben Admin Ant Design Vue 版本，菜单、权限、列表、表单、抽屉和动态路由已形成统一模式。
  - title: 平台能力闭环
    details: 认证、RBAC、租户、工作流、消息、审计、监控、文件、代码生成器可直接作为二开基础。
---

<div class="mini-home">
  <section class="mini-home-section">
    <div class="mini-section-kicker">WHY MINIADMIN</div>
    <h2 class="mini-section-title">不是只有页面骨架，而是一套能继续长大的后台系统。</h2>
    <p class="mini-section-copy">
      MiniAdmin 的目标不是做一个演示后台，而是把企业后台常见的基础能力先跑通：用户和权限、租户边界、流程审批、消息通知、审计追踪、运行监控、文件存储、代码生成和上线验收。你可以把它当作学习项目，也可以把它当作团队二开的起点。
    </p>
    <div class="mini-feature-grid">
      <div class="mini-feature">
        <strong>适合学习</strong>
        <span>从项目结构、认证权限、数据库迁移到前后端联调都有文档，可以按模块理解企业后台怎么搭起来。</span>
      </div>
      <div class="mini-feature">
        <strong>适合交付</strong>
        <span>常用平台能力已经闭环，管理员可以维护用户、角色、租户、流程、通知、审计和运行状态。</span>
      </div>
      <div class="mini-feature">
        <strong>适合二开</strong>
        <span>新增模块有固定套路：实体、DTO、服务、接口、菜单、页面、权限、测试和功能文档。</span>
      </div>
    </div>
  </section>

  <section class="mini-home-section">
    <div class="mini-section-kicker">CAPABILITY MAP</div>
    <h2 class="mini-section-title">从登录到审批，从消息到审计，平台能力按真实后台场景组织。</h2>
    <div class="mini-flow">
      <div class="mini-flow-step">
        <b>01 身份与权限</b>
        <p>JWT 登录、RBAC、菜单按钮权限、数据范围和权限诊断。</p>
      </div>
      <div class="mini-flow-step">
        <b>02 租户与组织</b>
        <p>SaaS 租户、租户套餐、部门岗位、用户初始化和租户状态控制。</p>
      </div>
      <div class="mini-flow-step">
        <b>03 流程与消息</b>
        <p>流程定义、发起审批、待办已办、抄送已读、模板通知和投递追踪。</p>
      </div>
      <div class="mini-flow-step">
        <b>04 运维与二开</b>
        <p>审计日志、系统监控、文件存储、代码生成器和运行手册。</p>
      </div>
    </div>
  </section>

  <section class="mini-callout">
    <h2>推荐阅读路径</h2>
    <p>
      第一次接触项目时，先看“快速开始”把系统跑起来，再看“功能总览”理解平台能力。如果你准备新增自己的模块，直接进入“架构总览”和“新增模块流程”。上线前按“验收清单”跑一遍，确认工作流、消息中心和基础权限没有回归。
    </p>
  </section>
</div>
