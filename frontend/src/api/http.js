import axios from 'axios'

export const http = axios.create({
  baseURL: '',
  timeout: 30000
})

http.interceptors.request.use((config) => {
  const token = localStorage.getItem('token')
  if (token) {
    config.headers['X-Token'] = token
  }
  return config
})

http.interceptors.response.use(
  (r) => r,
  (err) => {
    if (err?.response?.status === 401) {
      localStorage.removeItem('token')
      localStorage.removeItem('role')
      localStorage.removeItem('userId')
      if (location.pathname !== '/app/login' && location.pathname !== '/login') {
        location.href = '/app/login'
      }
    }
    return Promise.reject(err)
  }
)
