import axios from 'axios'

const apiBase = import.meta.env.VITE_API_BASE || ''

export const http = axios.create({
  baseURL: apiBase,
  timeout: 60000
})

http.interceptors.request.use((config) => {
  const token = localStorage.getItem('token')
  if (token) config.headers['X-Token'] = token
  config.headers['X-Requested-With'] = 'XMLHttpRequest'
  return config
})

http.interceptors.response.use(
  (res) => res,
  (err) => {
    const status = err?.response?.status
    if (status === 401) {
      localStorage.removeItem('token')
      // let router guard redirect on next navigation
    }
    return Promise.reject(err)
  }
)

export function apiErrorMessage(err) {
  const data = err?.response?.data
  if (!data) return err?.message || '请求失败'
  if (typeof data === 'string') return data
  if (data.message) return data.message
  if (data.error) return data.error
  return JSON.stringify(data)
}
