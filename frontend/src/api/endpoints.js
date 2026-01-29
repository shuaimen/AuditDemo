import { http } from './http'

// Auth
export const AuthApi = {
  async login(username, password) {
    const { data } = await http.post('/api/auth/login', { username, password })
    return data
  },
  async me() {
    const { data } = await http.get('/api/auth/me')
    return data
  }
}

// Factories
export const FactoriesApi = {
  async list(q) {
    const { data } = await http.get('/api/factories', { params: { q: q || '' } })
    return data
  }
}

// Audits
export const AuditsApi = {
  async list(params) {
    const { data } = await http.get('/api/audits', { params })
    return data
  },
  async create(payload) {
    const { data } = await http.post('/api/audits', payload)
    return data
  },
  async get(auditId) {
    const { data } = await http.get(`/api/audits/${auditId}`)
    return data
  },
  async modules(auditId) {
    const { data } = await http.get(`/api/audits/${auditId}/modules`)
    return data
  },
  async moduleDetail(auditId, moduleId) {
    const { data } = await http.get(`/api/audits/${auditId}/modules/${moduleId}`)
    return data
  },
  async lockModule(auditId, moduleId) {
    const { data } = await http.post(`/api/audits/${auditId}/modules/${moduleId}/lock`)
    return data
  },
  async heartbeatModule(auditId, moduleId) {
    const { data } = await http.post(`/api/audits/${auditId}/modules/${moduleId}/heartbeat`)
    return data
  },
  async unlockModule(auditId, moduleId) {
    const { data } = await http.post(`/api/audits/${auditId}/modules/${moduleId}/unlock`)
    return data
  },
  async saveClause(auditId, clauseCode, payload) {
    const { data } = await http.put(`/api/audits/${auditId}/clauses/${encodeURIComponent(clauseCode)}`, payload)
    return data
  },
  async uploadClausePhoto(auditId, clauseCode, file, sortNo) {
    const fd = new FormData()
    fd.append('file', file)
    if (sortNo !== undefined && sortNo !== null) fd.append('sortNo', String(sortNo))
    const { data } = await http.post(`/api/audits/${auditId}/clauses/${encodeURIComponent(clauseCode)}/photos`, fd, {
      headers: { 'Content-Type': 'multipart/form-data' }
    })
    return data
  },
  async submitModule(auditId, moduleId) {
    const { data } = await http.post(`/api/audits/${auditId}/modules/${moduleId}/submit`)
    return data
  },
  async withdrawModule(auditId, moduleId) {
    const { data } = await http.post(`/api/audits/${auditId}/modules/${moduleId}/withdraw`)
    return data
  },
  async rate(auditId) {
    const { data } = await http.post(`/api/audits/${auditId}/rate`)
    return data
  },
  async reopen(auditId) {
    const { data } = await http.post(`/api/audits/${auditId}/reopen`)
    return data
  },
  async exportDetailExcel(auditId) {
    // open in new tab
    return `${http.defaults.baseURL || ''}/api/audits/${auditId}/export-detail`
  },
  async exportNgSummaryExcel(auditId) {
    return `${http.defaults.baseURL || ''}/api/audits/${auditId}/export-ng`
  },
  async reAudits(auditId) {
    const { data } = await http.get(`/api/reaudits/by-audit/${auditId}`)
    return data
  }
}

// CAPA
export const CapaApi = {
  async listByAudit(auditId) {
    const { data } = await http.get(`/api/capa/by-audit/${auditId}`)
    return data
  },
  async update(capaId, payload) {
    const { data } = await http.put(`/api/capa/${capaId}`, payload)
    return data
  },
  async uploadEvidence(capaId, file, sortNo) {
    const fd = new FormData()
    fd.append('file', file)
    if (sortNo !== undefined && sortNo !== null) fd.append('sortNo', String(sortNo))
    const { data } = await http.post(`/api/capa/${capaId}/evidence`, fd, {
      headers: { 'Content-Type': 'multipart/form-data' }
    })
    return data
  },
  async submitEvidence(capaId) {
    const { data } = await http.post(`/api/capa/${capaId}/submit-evidence`)
    return data
  },
  async close(capaId, reviewConclusion) {
    const { data } = await http.post(`/api/capa/${capaId}/close`, { reviewConclusion })
    return data
  }
}

// Certificates
export const CertificatesApi = {
  async list(params) {
    const { data } = await http.get('/api/certificates', { params })
    return data
  },
  async get(certId) {
    const { data } = await http.get(`/api/certificates/${certId}`)
    return data
  },
  async create(payload) {
    const { data } = await http.post('/api/certificates', payload)
    return data
  },
  async update(certId, payload) {
    const { data } = await http.put(`/api/certificates/${certId}`, payload)
    return data
  },
  async deactivate(certId) {
    const { data } = await http.post(`/api/certificates/${certId}/deactivate`)
    return data
  },
  async uploadFile(certId, file, sortNo) {
    const fd = new FormData()
    fd.append('file', file)
    if (sortNo !== undefined && sortNo !== null) fd.append('sortNo', String(sortNo))
    const { data } = await http.post(`/api/certificates/${certId}/files`, fd, {
      headers: { 'Content-Type': 'multipart/form-data' }
    })
    return data
  },
  async deleteFile(fileId) {
    const { data } = await http.delete(`/api/certificates/files/${fileId}`)
    return data
  },
  exportExpiringUrl(days, factoryId) {
    const base = http.defaults.baseURL || ''
    const params = new URLSearchParams()
    params.set('days', String(days || 60))
    if (factoryId) params.set('factoryId', factoryId)
    return `${base}/api/certificates/export-expiring?${params.toString()}`
  }
}

// Reports
export const ReportsApi = {
  async overview(params) {
    const { data } = await http.get('/api/reports/overview', { params })
    return data
  },
  async factoryHistory(factoryId) {
    const { data } = await http.get(`/api/reports/factory-history/${factoryId}`)
    return data
  }
}
