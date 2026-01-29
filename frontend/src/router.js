import { createRouter, createWebHashHistory } from 'vue-router'

import Login from './views/Login.vue'
import Audits from './views/Audits.vue'
import AuditDetail from './views/AuditDetail.vue'
import ModuleEdit from './views/ModuleEdit.vue'
import Capa from './views/Capa.vue'
import Certificates from './views/Certificates.vue'
import Reports from './views/Reports.vue'

const routes = [
  { path: '/', redirect: '/audits' },
  { path: '/login', component: Login, meta: { public: true } },
  { path: '/audits', component: Audits },
  { path: '/audits/:auditId', component: AuditDetail },
  { path: '/audits/:auditId/modules/:moduleId', component: ModuleEdit },
  { path: '/audits/:auditId/capa', component: Capa },
  { path: '/certificates', component: Certificates },
  { path: '/reports', component: Reports }
]

const router = createRouter({
  history: createWebHashHistory(),
  routes
})

router.beforeEach((to) => {
  if (to.meta.public) return true
  const token = localStorage.getItem('token')
  if (!token) return '/login'
  return true
})

export default router
