<template>
  <div class="card" style="max-width:420px;margin:30px auto">
    <h2 style="margin:0 0 10px 0">登录</h2>
    <div style="color:#666;font-size:13px;margin-bottom:10px">
      Demo 支持首次登录自动创建账号：admin/admin123 或 auditor/auditor123
    </div>
    <div class="row">
      <div class="col">
        <label>账号</label>
        <input v-model="username" placeholder="admin" />
      </div>
      <div class="col">
        <label>密码</label>
        <input v-model="password" type="password" placeholder="admin123" />
      </div>
    </div>
    <div style="margin-top:12px;display:flex;gap:10px;align-items:center">
      <button @click="doLogin" :disabled="loading">登录</button>
      <span v-if="err" class="bad">{{ err }}</span>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { http } from '../api/http'

const username = ref('admin')
const password = ref('admin123')
const loading = ref(false)
const err = ref('')

async function doLogin() {
  err.value = ''
  loading.value = true
  try {
    const r = await http.post('/api/auth/login', {
      username: username.value,
      password: password.value
    })
    localStorage.setItem('token', r.data.token)
    localStorage.setItem('userId', r.data.userId)
    localStorage.setItem('role', String(r.data.role))
    location.href = '/app/audits'
  } catch (e) {
    err.value = e?.response?.data?.message || '登录失败'
  } finally {
    loading.value = false
  }
}
</script>
