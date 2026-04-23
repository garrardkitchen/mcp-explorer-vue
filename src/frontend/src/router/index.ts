// src/router/index.ts
import { createRouter, createWebHistory } from 'vue-router'
import { devTunnelsApi } from '@/api/devtunnels'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      redirect: '/connections',
    },
    {
      path: '/connections',
      name: 'connections',
      component: () => import('@/views/ConnectionsView.vue'),
      meta: { title: 'Connections', icon: 'pi pi-server' },
    },
    {
      path: '/dev-tunnels',
      name: 'dev-tunnels',
      component: () => import('@/views/DevTunnelsView.vue'),
      meta: { title: 'Dev Tunnels', icon: 'pi pi-globe' },
    },
    {
      path: '/dev-tunnels/:id',
      name: 'dev-tunnel-inspector',
      component: () => import('@/views/DevTunnelInspectorView.vue'),
      meta: { title: 'Tunnel Inspector', icon: 'pi pi-send' },
      // Tunnel hosting requires a signed-in `devtunnel` CLI. Bounce unauthenticated
      // visitors back to the listing page with ?login=<tunnelId> so the parent view
      // can open the Connect dialog and resume the start automatically. This avoids
      // the detail page dead-ending on a red "Dev Tunnel login required" banner.
      beforeEnter: async (to) => {
        try {
          const state = await devTunnelsApi.getUserState()
          if (state?.isAvailable === false) return true
          if (state?.isLoggedIn) return true
          return {
            name: 'dev-tunnels',
            query: { login: String(to.params.id ?? '') },
          }
        } catch {
          return true
        }
      },
    },
    {
      path: '/tools',
      name: 'tools',
      component: () => import('@/views/ToolsView.vue'),
      meta: { title: 'Tools', icon: 'pi pi-wrench' },
    },
    {
      path: '/prompts',
      name: 'prompts',
      component: () => import('@/views/PromptsView.vue'),
      meta: { title: 'Prompts', icon: 'pi pi-file-edit' },
    },
    {
      path: '/resources',
      name: 'resources',
      component: () => import('@/views/ResourcesView.vue'),
      meta: { title: 'Resources', icon: 'pi pi-database' },
    },
    {
      path: '/resource-templates',
      name: 'resource-templates',
      component: () => import('@/views/ResourceTemplatesView.vue'),
      meta: { title: 'Templates', icon: 'pi pi-copy' },
    },
    {
      path: '/chat',
      name: 'chat',
      component: () => import('@/views/ChatView.vue'),
      meta: { title: 'Chat', icon: 'pi pi-comments' },
    },
    {
      path: '/workflows',
      name: 'workflows',
      component: () => import('@/views/WorkflowsView.vue'),
      meta: { title: 'Workflows', icon: 'pi pi-sitemap' },
    },
    {
      path: '/ai-models',
      name: 'ai-models',
      component: () => import('@/views/AiModelsView.vue'),
      meta: { title: 'AI Models', icon: 'pi pi-microchip-ai' },
    },
    {
      path: '/sensitive-fields',
      name: 'sensitive-fields',
      component: () => import('@/views/SensitiveFieldsView.vue'),
      meta: { title: 'Data Guard', icon: 'pi pi-shield' },
    },
    {
      path: '/elicitations',
      name: 'elicitations',
      component: () => import('@/views/ElicitationsView.vue'),
      meta: { title: 'Elicitations', icon: 'pi pi-bell' },
    },
  ],
})

router.afterEach((to) => {
  document.title = to.meta.title ? `${to.meta.title} — MCP Explorer` : 'MCP Explorer'
})

export default router
export type RouteMeta = { title: string; icon: string }
