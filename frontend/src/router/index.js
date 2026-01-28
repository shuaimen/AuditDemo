import { createRouter, createWebHistory } from 'vue-router'
import Login from '../views/Login.vue'
import Audits from '../views/Audits.vue'
import AuditDetail from '../views/AuditDetail.vue'
import ModuleEdit from '../views/ModuleEdit.vue'
import Capa from '../views/Capa.vue'
import ReAudits from '../views/ReAudits.vue'
import ReAuditDetail from '../views/ReAuditDetail.vue'
import Reports from '../views/Reports.vue'

const router = createRouter({
  history: createWebHistory('/app/'),
  routes: [
    { path: '/', redirect: '/audits' },
    { path: '/login', component: Login },
    { path: '/audits', component: Audits },
    { path: '/reports', component: Reports },
    { path: '/audits/:auditId', component: AuditDetail, props: true },
    { path: '/audits/:auditId/modules/:moduleId', component: ModuleEdit, props: true },
    { path: '/audits/:auditId/capa', component: Capa, props: true },
    { path: '/audits/:auditId/reaudits', component: ReAudits, props: true },
    { path: '/reaudits/:reAuditId', component: ReAuditDetail, props: true }
  ]
})

router.beforeEach((to) => {
  if (to.path !== '/login') {
    const token = localStorage.getItem('token')
    if (!token) return '/login'
  }
})

export default router
